namespace Axial.Tests

open System
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.WebSocket
open Xunit

module WebSocketTests =
    let private port () =
        use socket = new TcpListener(IPAddress.Loopback, 0)
        socket.Start()
        (socket.LocalEndpoint :?> IPEndPoint).Port

    type private Server(abortAfterEcho: bool) =
        let listener = new HttpListener()
        let closed = TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
        let mutable accepted: WebSocket = null
        let endpoint = sprintf "http://127.0.0.1:%d/" (port ())
        do listener.Prefixes.Add endpoint; listener.Start()
        let worker = task {
            try
                let! request = listener.GetContextAsync()
                let! context = request.AcceptWebSocketAsync(null)
                accepted <- context.WebSocket
                let buffer = Array.zeroCreate<byte> 8192
                let mutable running = true
                while running do
                    let! result = accepted.ReceiveAsync(ArraySegment<byte>(buffer), CancellationToken.None)
                    if result.MessageType = WebSocketMessageType.Close then
                        closed.TrySetResult true |> ignore
                        running <- false
                        do! accepted.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                    else
                        do! accepted.SendAsync(ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None)
                        if abortAfterEcho then
                            do! Task.Delay 100
                            accepted.Abort()
                            accepted.Dispose()
                            running <- false
            with _ -> ()
        }
        member _.Url = endpoint.Replace("http:", "ws:")
        member _.Closed = closed.Task
        interface IDisposable with
            member _.Dispose() =
                listener.Stop()
                listener.Close()
                if not (isNull accepted) then accepted.Dispose()
                try worker.Wait(TimeSpan.FromSeconds 2.) |> ignore with _ -> ()

    let private connect url =
        match (WebSocket.openSession (WebSocket.connect url)).RunSynchronously({ new IHasWebSocket with member _.WebSocket = WebSocket.live() }) with
        | Exit.Success session -> session
        | other -> failwithf "connect: %A" other

    [<Fact>]
    let ``live text round trip`` () =
        use server = new Server(false)
        use session = connect server.Url
        Assert.Equal(Exit.Success (), (session |> WebSocket.sendText "hello").RunSynchronously(()))
        Assert.Equal(Exit.Success [WebSocketMessage.Text "hello"], (session |> WebSocket.messages |> FlowStream.take 1 |> FlowStream.runCollect).RunSynchronously(()))

    [<Fact>]
    let ``live binary round trip`` () =
        use server = new Server(false)
        use session = connect server.Url
        let bytes = [| 0uy; 255uy; 42uy |]
        Assert.Equal(Exit.Success (), (session |> WebSocket.sendBinary bytes).RunSynchronously(()))
        Assert.Equal(Exit.Success [WebSocketMessage.Binary bytes], (session |> WebSocket.messages |> FlowStream.take 1 |> FlowStream.runCollect).RunSynchronously(()))

    [<Fact>]
    let ``client close ends receive`` () =
        use server = new Server(false)
        use session = connect server.Url
        Assert.Equal(Exit.Success (), (session |> WebSocket.close 1000 "done").RunSynchronously(()))
        Assert.True(server.Closed.Wait(TimeSpan.FromSeconds 5.))
        Assert.Equal(Exit.Success [], (session |> WebSocket.messages |> FlowStream.runCollect).RunSynchronously(()))

    [<Fact>]
    let ``aborted server produces typed connection lost`` () =
        use server = new Server(true)
        use session = connect server.Url
        Assert.Equal(Exit.Success (), (session |> WebSocket.sendText "first").RunSynchronously(()))
        Assert.Equal(Exit.Success [WebSocketMessage.Text "first"], (session |> WebSocket.messages |> FlowStream.take 1 |> FlowStream.runCollect).RunSynchronously(()))
        match session.Receive().RunSynchronously(()) with
        | Exit.Failure(Cause.Fail(WebSocketError.ConnectionLost _)) -> ()
        | other -> failwithf "expected ConnectionLost: %A" other

    [<Fact>]
    let ``closed port produces connect failed`` () =
        let url = sprintf "ws://127.0.0.1:%d/" (port ())
        match (WebSocket.openSession (WebSocket.connect url)).RunSynchronously({ new IHasWebSocket with member _.WebSocket = WebSocket.live() }) with
        | Exit.Failure(Cause.Fail(WebSocketError.ConnectFailed _)) -> ()
        | other -> failwithf "expected ConnectFailed: %A" other

    [<Fact>]
    let ``scripted connection loss is typed`` () =
        let fake = TestWebSocket([], receiveError = WebSocketError.ConnectionLost "scripted")
        use session =
            match (fake :> IWebSocket).Connect(WebSocket.connect "ws://localhost/").RunSynchronously(()) with
            | Exit.Success value -> value
            | other -> failwithf "connect: %A" other
        Assert.Equal(Exit.Failure(Cause.Fail(WebSocketError.ConnectionLost "scripted")), session.Receive().RunSynchronously(()))

    [<Fact>]
    let ``scope releases session after taking one message`` () =
        use server = new Server(false)
        let env = { new IHasWebSocket with member _.WebSocket = WebSocket.live() }
        let operation =
            WebSocket.withConnection (WebSocket.connect server.Url) (fun session ->
                session |> WebSocket.sendText "one"
                |> Flow.bind (fun () -> session |> WebSocket.messages |> FlowStream.take 1 |> FlowStream.runCollect)
                |> Flow.localEnv (fun _ -> ()))
        Assert.Equal(Exit.Success [WebSocketMessage.Text "one"], operation.RunSynchronously(env))

    [<Fact>]
    let ``scripted messages and ordered sends`` () =
        let fake = TestWebSocket([ WebSocketMessage.Text "hello"; WebSocketMessage.Binary [| 1uy |] ])
        use session =
            match (fake :> IWebSocket).Connect(WebSocket.connect "ws://localhost/").RunSynchronously(()) with
            | Exit.Success session -> session
            | other -> failwithf "connect failed: %A" other
        Assert.Equal(Exit.Success (), (session |> WebSocket.sendText "one").RunSynchronously(()))
        Assert.Equal(Exit.Success (), (session |> WebSocket.sendText "two").RunSynchronously(()))
        Assert.Equal(Exit.Success [WebSocketMessage.Text "hello"; WebSocketMessage.Binary [| 1uy |]], (session |> WebSocket.messages |> FlowStream.runCollect).RunSynchronously(()))
        Assert.Equal<WebSocketMessage list>([WebSocketMessage.Text "one"; WebSocketMessage.Text "two"], List.ofSeq fake.Sent)
