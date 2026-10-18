namespace Axial.Tests

open Axial
open Axial.WebSocket
open Xunit

module WebSocketTests =
    [<Fact>]
    let ``scripted messages are delivered and sends recorded`` () =
        let fake = TestWebSocket([ WebSocketMessage.Text "hello"; WebSocketMessage.Binary [| 1uy |] ])
        let session =
            match (fake :> IWebSocket).Connect(WebSocket.connect "ws://localhost/").RunSynchronously(()) with
            | Exit.Success session -> session
            | _ -> failwith "connect failed"
        match (session |> WebSocket.sendText "reply").RunSynchronously(()) with
        | Exit.Success () -> ()
        | _ -> failwith "send failed"
        match (session |> WebSocket.messages |> FlowStream.runCollect).RunSynchronously(()) with
        | Exit.Success messages -> Assert.Equal<WebSocketMessage list>([ WebSocketMessage.Text "hello"; WebSocketMessage.Binary [| 1uy |] ], messages)
        | _ -> failwith "receive failed"
        Assert.Equal(1, fake.Sent.Count)
        (session :> System.IDisposable).Dispose()
