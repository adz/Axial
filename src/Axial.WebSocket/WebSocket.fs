namespace Axial.WebSocket

open System
open System.Net.WebSockets
open System.Net.Http
open System.Net.Security
open System.Security.Authentication
open System.Security.Cryptography.X509Certificates
open System.IO
open System.Net.Sockets
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial

[<RequireQualifiedAccess>]
type WebSocketMessage =
    | Text of string
    | Binary of byte array
    override this.ToString() =
        match this with
        | Text value -> "Text(" + string value.Length + " chars)"
        | Binary bytes -> "Binary(" + string bytes.Length + " bytes)"

[<RequireQualifiedAccess>]
type WebSocketError =
    | ConnectFailed of string
    | ConnectionLost of string
    | ProtocolError of string
    | Closed of code: int option * reason: string option
    override this.ToString() =
        match this with
        | ConnectFailed message -> $"WebSocket connection failed: {message}"
        | ConnectionLost message -> $"WebSocket connection lost: {message}"
        | ProtocolError message -> $"WebSocket protocol error: {message}"
        | Closed(code, reason) ->
            let status = code |> Option.map string |> Option.defaultValue "unknown"
            let detail = reason |> Option.defaultValue ""
            "WebSocket closed (" + status + ", " + detail + ")"

[<RequireQualifiedAccess>]
type TlsVersion = | Default | Tls12 | Tls13

type WebSocketTls = { ClientCertificate: X509Certificate2 option; TrustAnchors: X509Certificate2 list option; MinimumVersion: TlsVersion }

[<RequireQualifiedAccess>]
type BytesDirection = | Sent | Received

type ConnectContext = { Host: string; Port: int }

/// Immutable connection specification. Advanced transport options require net8.0.
type WebSocketOptions = internal {
    Url: Uri; Subprotocols: string list; MaxMessageBytes: int
    Tls: WebSocketTls; Connect: (ConnectContext -> Flow<unit, WebSocketError, Stream>) option
    OnBytes: (BytesDirection -> int -> unit) option
    Invoker: HttpMessageInvoker option }

#if !NETSTANDARD2_1
type private CountingStream(inner: Stream, notify: BytesDirection -> int -> unit) =
    inherit Stream()
    override _.CanRead = inner.CanRead
    override _.CanWrite = inner.CanWrite
    override _.CanSeek = false
    override _.Length = raise (NotSupportedException())
    override _.Position with get () = raise (NotSupportedException()) and set _ = raise (NotSupportedException())
    override _.Flush() = inner.Flush()
    override _.FlushAsync(ct) = inner.FlushAsync(ct)
    override _.Read(buffer, offset, count) =
        let n = inner.Read(buffer, offset, count)
        notify BytesDirection.Received n
        n
    override _.ReadByte() =
        let value = inner.ReadByte()
        if value >= 0 then notify BytesDirection.Received 1
        value
    override _.ReadAsync(buffer: byte[], offset, count, ct) = (task {
        let! n = inner.ReadAsync(buffer, offset, count, ct)
        notify BytesDirection.Received n
        return n
    })
    override _.ReadAsync(buffer: Memory<byte>, ct) = ValueTask<int>(task {
        let! n = inner.ReadAsync(buffer, ct)
        notify BytesDirection.Received n
        return n
    })
    override _.Write(buffer, offset, count) =
        inner.Write(buffer, offset, count)
        notify BytesDirection.Sent count
    override _.WriteByte(value) =
        inner.WriteByte(value)
        notify BytesDirection.Sent 1
    override _.WriteAsync(buffer: byte[], offset, count, ct) = (task {
        do! inner.WriteAsync(buffer, offset, count, ct)
        notify BytesDirection.Sent count
    } :> Task)
    override _.WriteAsync(buffer: ReadOnlyMemory<byte>, ct) = ValueTask(task {
        do! inner.WriteAsync(buffer, ct)
        notify BytesDirection.Sent buffer.Length
    })
    override _.Seek(_, _) = raise (NotSupportedException())
    override _.SetLength(_) = raise (NotSupportedException())
    override _.Dispose(disposing) =
        if disposing then inner.Dispose()
        base.Dispose(disposing)
#endif

/// A session owns its connection until disposed or released by the owning scope.
type WebSocketSession internal
    (send: WebSocketMessage -> Flow<unit, WebSocketError, unit>,
     receive: unit -> Flow<unit, WebSocketError, WebSocketMessage option>,
     close: int -> string -> Flow<unit, WebSocketError, unit>,
     dispose: unit -> unit) =
    member _.Send message = send message
    member _.Receive() = receive ()
    member _.Close code reason = close code reason
    interface IDisposable with member _.Dispose() = dispose ()

type IWebSocket =
    abstract Connect: WebSocketOptions -> Flow<unit, WebSocketError, WebSocketSession>

type IHasWebSocket =
    abstract WebSocket: IWebSocket

[<RequireQualifiedAccess>]
module WebSocket =
    let connect (url: string) =
        let mutable uri = Unchecked.defaultof<Uri>
        if not (Uri.TryCreate(url, UriKind.Absolute, &uri)) || (uri.Scheme <> "ws" && uri.Scheme <> "wss") then
            invalidArg (nameof url) "An absolute ws or wss URL is required."
        { Url = uri; Subprotocols = []; MaxMessageBytes = 1024 * 1024; Tls = { ClientCertificate = None; TrustAnchors = None; MinimumVersion = TlsVersion.Default }; Connect = None; OnBytes = None; Invoker = None }

    let subprotocol value (options: WebSocketOptions) =
        if String.IsNullOrWhiteSpace value || options.Subprotocols |> List.contains value then
            invalidArg (nameof value) "A unique nonempty subprotocol is required."
        { options with Subprotocols = options.Subprotocols @ [ value ] }

    let maxMessageBytes value (options: WebSocketOptions) =
        if value <= 0 then invalidArg (nameof value) "The message limit must be positive."
        { options with MaxMessageBytes = value }

    let tls value options = { options with Tls = value }
    let connectWith value options = { options with Connect = Some value }
    let onBytes value options = { options with OnBytes = Some value }

    /// Uses a caller-owned HTTP invoker for the WebSocket upgrade. The caller must keep it alive through the session.
    /// This cannot be combined with `tls` or `connectWith`; `onBytes` is not called because the invoker owns transport.
    let invoker value options = { options with Invoker = Some value }

    let service<'env, 'error when 'env :> IHasWebSocket> : Flow<'env, 'error, IWebSocket> = Flow.envWith _.WebSocket

    let openSession<'env when 'env :> IHasWebSocket> options : Flow<'env, WebSocketError, WebSocketSession> =
        flow {
            let! socket = service
            return! socket.Connect options |> Flow.localEnv (fun _ -> ())
        }

    let withConnection<'env, 'value when 'env :> IHasWebSocket>
        (options: WebSocketOptions) (useSession: WebSocketSession -> Flow<'env, WebSocketError, 'value>) =
        flow {
            let! session = openSession options
            do! Flow.scopeFinalizer (Flow.delay (fun () -> (session :> IDisposable).Dispose(); Flow.succeed ()))
            return! useSession session
        } |> Flow.scoped

    let send message (session: WebSocketSession) = session.Send message
    let sendText text session = send (WebSocketMessage.Text text) session
    let sendBinary bytes session = send (WebSocketMessage.Binary bytes) session
    let messages (session: WebSocketSession) =
        FlowStream.unfoldFlow (fun () -> session.Receive() |> Flow.map (Option.map (fun value -> value, ()))) ()
    let close code reason (session: WebSocketSession) = session.Close code reason

    let live () : IWebSocket =
        { new IWebSocket with
            member _.Connect options =
                let defaultTls = { ClientCertificate = None; TrustAnchors = None; MinimumVersion = TlsVersion.Default }
                if options.Invoker.IsSome && options.Tls <> defaultTls then
                    Flow.fail(WebSocketError.ConnectFailed "A caller-supplied HTTP invoker cannot be combined with WebSocket.tls.")
                elif options.Invoker.IsSome && options.Connect.IsSome then
                    Flow.fail(WebSocketError.ConnectFailed "A caller-supplied HTTP invoker cannot be combined with WebSocket.connectWith.")
                else flow {
                    let! ct = Flow.cancellationToken
                    return! Flow.fromAsyncResult (async {
                        return! task {
                            // The live service owns native connections.
                            // axial-allow-AXG001: intentional websocket host or live-service boundary
                            let socket = new ClientWebSocket()
                            let mutable connectDefect: exn option = None
                            try
                                for protocol in options.Subprotocols do socket.Options.AddSubProtocol protocol
#if NETSTANDARD2_1
                                if options.Connect.IsSome || options.OnBytes.IsSome || options.Invoker.IsSome || options.Tls <> defaultTls then
                                    invalidOp "TLS and transport hooks require net8.0."
                                do! socket.ConnectAsync(options.Url, ct)
#else
                                match options.Invoker with
                                | Some invoker -> do! socket.ConnectAsync(options.Url, invoker, ct)
                                | None ->
                                    use handler = new SocketsHttpHandler()
                                    match options.Tls.MinimumVersion with
                                    | TlsVersion.Tls13 -> handler.SslOptions.EnabledSslProtocols <- SslProtocols.Tls13
                                    | TlsVersion.Tls12 -> handler.SslOptions.EnabledSslProtocols <- SslProtocols.Tls12 ||| SslProtocols.Tls13
                                    | TlsVersion.Default -> ()
                                    options.Tls.ClientCertificate |> Option.iter (fun cert ->
                                        handler.SslOptions.ClientCertificates <- X509CertificateCollection()
                                        handler.SslOptions.ClientCertificates.Add cert |> ignore)
                                    options.Tls.TrustAnchors |> Option.iter (fun roots ->
                                        handler.SslOptions.RemoteCertificateValidationCallback <- RemoteCertificateValidationCallback(fun _ cert _ errors ->
                                            if isNull cert || errors.HasFlag SslPolicyErrors.RemoteCertificateNameMismatch then false else
                                            use peer = new X509Certificate2(cert)
                                            let name = peer.GetNameInfo(X509NameType.DnsName, false)
                                            let host = options.Url.IdnHost
                                            let validName =
                                                String.Equals(name, host, StringComparison.OrdinalIgnoreCase)
                                                || (name.StartsWith("*.", StringComparison.Ordinal)
                                                    && host.EndsWith(name.Substring(1), StringComparison.OrdinalIgnoreCase)
                                                    && host.Length > name.Length - 1
                                                    && not (host.Substring(0, host.Length - name.Length + 1).Contains('.')))
                                            if not validName then false else
                                            use chain = new X509Chain()
                                            chain.ChainPolicy.TrustMode <- X509ChainTrustMode.CustomRootTrust
                                            chain.ChainPolicy.RevocationMode <- X509RevocationMode.NoCheck
                                            for root in roots do chain.ChainPolicy.CustomTrustStore.Add root |> ignore
                                            chain.Build(peer)))
                                    if options.Connect.IsSome || options.OnBytes.IsSome then
                                        handler.ConnectCallback <- Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>(fun context token ->
                                            ValueTask<Stream>(task {
                                                let! transport = task {
                                                    match options.Connect with
                                                    | Some hook ->
                                                        match (hook { Host = context.DnsEndPoint.Host; Port = context.DnsEndPoint.Port }).RunSynchronously((), cancellationToken = token) with
                                                        | Exit.Success stream -> return stream
                                                        | Exit.Failure(Cause.Fail error) -> return raise (IOException("Custom connect failed: " + error.ToString()))
                                                        | Exit.Failure(Cause.Die error) ->
                                                            connectDefect <- Some error
                                                            return raise error
                                                        | _ -> return raise (OperationCanceledException("Custom connect interrupted.", token))
                                                    | None ->
                                                        let client = new Socket(SocketType.Stream, ProtocolType.Tcp)
                                                        try
                                                            do! client.ConnectAsync(context.DnsEndPoint, token)
                                                            return new NetworkStream(client, true) :> Stream
                                                        with error -> client.Dispose(); return raise error
                                                }
                                                return match options.OnBytes with
                                                       | Some notify -> new CountingStream(transport, notify) :> Stream
                                                       | None -> transport
                                            }))
                                    use invoker = new HttpMessageInvoker(handler, false)
                                    do! socket.ConnectAsync(options.Url, invoker, ct)
#endif
                                let mutable closed = false
                                let sendGate = new SemaphoreSlim(1, 1)
                                let mutable disposed = 0
                                let dispose () =
                                    if Interlocked.Exchange(&disposed, 1) = 0 then // axial-allow-AXG009: Transport close and scope cleanup can both dispose the native socket; only one may dispose its send gate.
                                        socket.Dispose()
                                        sendGate.Dispose()
                                let transport (operation: CancellationToken -> Task<'a>) =
                                    Flow.fromTaskResult (fun token -> task {
                                        try
                                            let! value = operation token
                                            return Ok value
                                        with
                                        | :? WebSocketException as error -> return Error(WebSocketError.ConnectionLost error.Message)
                                        | :? IOException as error -> return Error(WebSocketError.ConnectionLost error.Message)
                                        | :? SocketException as error -> return Error(WebSocketError.ConnectionLost error.Message)
                                        | :? AuthenticationException as error -> return Error(WebSocketError.ConnectionLost error.Message)
                                    })
                                let transportUnit (operation: CancellationToken -> Task) =
                                    transport (fun token -> task { do! operation token })
                                let receivePart token chunk =
                                    transport (fun _ -> socket.ReceiveAsync(ArraySegment<byte>(chunk), token))
                                let send message =
                                    flow {
                                        let! token = Flow.cancellationToken
                                        do! Flow.fromTask (fun _ -> task { do! sendGate.WaitAsync(token) })
                                        try
                                            if closed then return! Flow.fail(WebSocketError.Closed(None, None))
                                            let kind, bytes =
                                                match message with
                                                | WebSocketMessage.Text text -> WebSocketMessageType.Text, Encoding.UTF8.GetBytes text
                                                | WebSocketMessage.Binary bytes -> WebSocketMessageType.Binary, bytes
                                            do! transportUnit (fun _ -> socket.SendAsync(ArraySegment<byte>(bytes), kind, true, token))
                                        finally sendGate.Release() |> ignore
                                    }
                                let receive () =
                                    flow {
                                        let! token = Flow.cancellationToken
                                        use output = new IO.MemoryStream()
                                        let chunk = Array.zeroCreate<byte> 8192
                                        let mutable complete = false
                                        let mutable kind = WebSocketMessageType.Binary
                                        let mutable tooLarge = false
                                        while not complete && not closed do
                                            let! part = receivePart token chunk
                                            kind <- part.MessageType
                                            if kind = WebSocketMessageType.Close then complete <- true
                                            elif output.Length + int64 part.Count > int64 options.MaxMessageBytes then
                                                tooLarge <- true
                                                complete <- true
                                            else
                                                output.Write(chunk, 0, part.Count)
                                                complete <- part.EndOfMessage
                                        if tooLarge then
                                            // The unread fragments cannot be treated as a new message.
                                            closed <- true
                                            socket.Abort()
                                            return! Flow.fail(WebSocketError.ProtocolError "Message exceeds configured limit")
                                        elif closed || kind = WebSocketMessageType.Close then
                                            closed <- true
                                            return None
                                        elif kind = WebSocketMessageType.Text then
                                            try return Some(WebSocketMessage.Text((UTF8Encoding(false, true)).GetString(output.ToArray())))
                                            with :? DecoderFallbackException as error -> return! Flow.fail(WebSocketError.ProtocolError error.Message)
                                        else return Some(WebSocketMessage.Binary(output.ToArray()))
                                    }
                                let close code reason =
                                    flow {
                                        let! token = Flow.cancellationToken
                                        do! Flow.fromTask (fun _ -> task { do! sendGate.WaitAsync(token) })
                                        try
                                            if not closed then
                                                closed <- true
                                                do! transportUnit (fun _ -> socket.CloseAsync(enum<WebSocketCloseStatus> code, reason, token))
                                        finally sendGate.Release() |> ignore
                                    }
                                return Ok(new WebSocketSession(send, receive, close, dispose))
                            with
                            | :? OperationCanceledException as error -> socket.Dispose(); return raise error
                            | :? WebSocketException as error ->
                                socket.Dispose()
                                match connectDefect with
                                | Some defect -> return raise defect
                                | None -> return Error(WebSocketError.ConnectFailed error.Message)
                            | :? IOException as error ->
                                socket.Dispose()
                                match connectDefect with
                                | Some defect -> return raise defect
                                | None -> return Error(WebSocketError.ConnectFailed error.Message)
                            | :? SocketException as error -> socket.Dispose(); return Error(WebSocketError.ConnectFailed error.Message)
                            | :? AuthenticationException as error -> socket.Dispose(); return Error(WebSocketError.ConnectFailed error.Message)
                            | error -> socket.Dispose(); return raise error
                        } |> Async.AwaitTask
                    })
                } }

/// Scripted in-memory transport for deterministic application tests.
type TestWebSocket(inbound: WebSocketMessage list, ?receiveError: WebSocketError) =
    let pending = Collections.Generic.Queue<WebSocketMessage>(inbound)
    let sent = ResizeArray<WebSocketMessage>()
    member _.Sent = sent :> Collections.Generic.IReadOnlyList<WebSocketMessage>
    interface IWebSocket with
        member _.Connect _ =
            Flow.succeed(new WebSocketSession(
                (fun message -> Flow.delay (fun () -> sent.Add message; Flow.succeed ())),
                (fun () -> Flow.delay (fun () ->
                    if pending.Count > 0 then Flow.succeed (Some(pending.Dequeue()))
                    else match receiveError with
                         | Some error -> Flow.fail error
                         | None -> Flow.succeed None)),
                (fun _ _ -> Flow.succeed ()),
                ignore))
