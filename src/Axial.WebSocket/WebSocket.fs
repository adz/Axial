namespace Axial.WebSocket

open System
open System.Net.WebSockets
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
        | Text value -> $"Text({value.Length} chars)"
        | Binary bytes -> $"Binary({bytes.Length} bytes)"

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
            $"WebSocket closed ({status}, {detail})"

/// Immutable connection specification.
type WebSocketOptions = internal { Url: Uri; Subprotocols: string list; MaxMessageBytes: int }

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
        { Url = uri; Subprotocols = []; MaxMessageBytes = 1024 * 1024 }

    let subprotocol value (options: WebSocketOptions) =
        if String.IsNullOrWhiteSpace value || options.Subprotocols |> List.contains value then
            invalidArg (nameof value) "A unique nonempty subprotocol is required."
        { options with Subprotocols = options.Subprotocols @ [ value ] }

    let maxMessageBytes value (options: WebSocketOptions) =
        if value <= 0 then invalidArg (nameof value) "The message limit must be positive."
        { options with MaxMessageBytes = value }

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
                flow {
                    let! ct = Flow.cancellationToken
                    return! Flow.fromAsyncResult (async {
                        return! task {
                            // The live service owns native connections.
                            // axial-allow-effect: websocket
                            let socket = new ClientWebSocket()
                            try
                                for protocol in options.Subprotocols do socket.Options.AddSubProtocol protocol
                                do! socket.ConnectAsync(options.Url, ct)
                                let mutable closed = false
                                let send message =
                                    flow {
                                        let! token = Flow.cancellationToken
                                        if closed then return! Flow.fail(WebSocketError.Closed(None, None))
                                        let kind, bytes =
                                            match message with
                                            | WebSocketMessage.Text text -> WebSocketMessageType.Text, Encoding.UTF8.GetBytes text
                                            | WebSocketMessage.Binary bytes -> WebSocketMessageType.Binary, bytes
                                        do! Flow.fromAsync (socket.SendAsync(ArraySegment<byte>(bytes), kind, true, token) |> Async.AwaitTask)
                                    }
                                let receive () =
                                    flow {
                                        let! token = Flow.cancellationToken
                                        use output = new IO.MemoryStream()
                                        let chunk = Array.zeroCreate<byte> 8192
                                        let mutable complete = false
                                        let mutable kind = WebSocketMessageType.Binary
                                        let mutable tooLarge = false
                                        while not complete do
                                            let! part = Flow.fromAsync (socket.ReceiveAsync(ArraySegment<byte>(chunk), token) |> Async.AwaitTask)
                                            kind <- part.MessageType
                                            if kind = WebSocketMessageType.Close then complete <- true
                                            elif output.Length + int64 part.Count > int64 options.MaxMessageBytes then
                                                tooLarge <- true
                                                complete <- true
                                            else
                                                output.Write(chunk, 0, part.Count)
                                                complete <- part.EndOfMessage
                                        if tooLarge then return! Flow.fail(WebSocketError.ProtocolError "Message exceeds configured limit")
                                        elif kind = WebSocketMessageType.Close then
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
                                        if not closed then
                                            closed <- true
                                            do! Flow.fromAsync (socket.CloseAsync(enum<WebSocketCloseStatus> code, reason, token) |> Async.AwaitTask)
                                    }
                                return Ok(new WebSocketSession(send, receive, close, fun () -> socket.Dispose()))
                            with
                            | :? OperationCanceledException as error -> socket.Dispose(); return raise error
                            | :? WebSocketException as error -> socket.Dispose(); return Error(WebSocketError.ConnectFailed error.Message)
                        } |> Async.AwaitTask
                    })
                } }

/// Scripted in-memory transport for deterministic application tests.
type TestWebSocket(inbound: WebSocketMessage list) =
    let pending = Collections.Generic.Queue<WebSocketMessage>(inbound)
    let sent = ResizeArray<WebSocketMessage>()
    member _.Sent = sent :> Collections.Generic.IReadOnlyList<WebSocketMessage>
    interface IWebSocket with
        member _.Connect _ =
            Flow.succeed(new WebSocketSession(
                (fun message -> Flow.delay (fun () -> sent.Add message; Flow.succeed ())),
                (fun () -> Flow.delay (fun () -> Flow.succeed (if pending.Count = 0 then None else Some(pending.Dequeue())))),
                (fun _ _ -> Flow.succeed ()),
                ignore))
