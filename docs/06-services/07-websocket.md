---
title: WebSocket
linkTitle: WebSocket
description: Typed WebSocket client sessions, sends, receives, and WebSocketError as a first-use guide.
---

# WebSocket

Install `Axial.WebSocket` and open `Axial.WebSocket`:

```bash
dotnet add package Axial.WebSocket
```

`WebSocket.connect` creates connection options. `WebSocket.openSession` obtains a session from the
`IHasWebSocket.WebSocket` service; `WebSocket.withConnection` disposes it when the scope ends. `TestWebSocket`
provides a scripted transport for application tests.

## First use

A scripted transport replies with two messages and records what the application sends, so a first exchange needs no
live server:

```fsharp no-check reason="Axial.WebSocket is not referenced by the documentation example compiler; the same API is compiled in Axial.WebSocket.Tests"
open Axial
open Axial.WebSocket

let fake = TestWebSocket([ WebSocketMessage.Text "hello"; WebSocketMessage.Binary [| 1uy |] ])
let env = { new IHasWebSocket with member _.WebSocket = fake }

let exchange =
    WebSocket.withConnection (WebSocket.connect "ws://localhost/") (fun session ->
        session
        |> WebSocket.sendText "one"
        |> Flow.bind (fun () ->
            session |> WebSocket.messages |> FlowStream.take 2 |> FlowStream.runCollect)
        |> Flow.localEnv (fun _ -> ()))

// exchange |> Flow.run env
// -> Exit.Success [ WebSocketMessage.Text "hello"; WebSocketMessage.Binary [| 1uy |] ]
// fake.Sent -> [ WebSocketMessage.Text "one" ]
```

`WebSocket.withConnection` scopes the session: it is disposed when the exchange finishes, fails, or is interrupted.
A transport failure or a scripted error arrives as a typed `WebSocketError`, not an exception:

```fsharp no-check reason="Axial.WebSocket is not referenced by the documentation example compiler; the same API is compiled in Axial.WebSocket.Tests"
let lost = TestWebSocket([], receiveError = WebSocketError.ConnectionLost "scripted")
let lostEnv = { new IHasWebSocket with member _.WebSocket = lost }

let readOne =
    WebSocket.withConnection (WebSocket.connect "ws://localhost/") (fun session ->
        session |> WebSocket.messages |> FlowStream.take 1 |> FlowStream.runCollect
        |> Flow.localEnv (fun _ -> ()))

// readOne |> Flow.run lostEnv
// -> Exit.Failure (Cause.Fail (WebSocketError.ConnectionLost "scripted"))
```

For a live connection, supply `WebSocket.live()` as the `IWebSocket` implementation, or test message-level
behavior with `TestWebSocket` as above and keep TLS/transport concerns in a separate integration test.

Use `WebSocket.subprotocol` and `WebSocket.maxMessageBytes` to configure negotiation and the receive limit. `WebSocket.tls` accepts a `WebSocketTls` record: `ClientCertificate` supplies a client identity, `TrustAnchors = Some roots` validates the server against **only** those roots (not the system store), and `MinimumVersion = TlsVersion.Tls13` restricts negotiation to TLS 1.3. `None` uses platform server trust. An empty custom-root list trusts no servers.

`WebSocket.connectWith` supplies a `ConnectContext -> Flow<unit, WebSocketError, Stream>` hook for transport connections. The returned stream belongs to the session. `WebSocket.onBytes` receives `BytesDirection.Sent` or `Received` and the number of bytes transferred by the transport stream. Counts include the HTTP upgrade and TLS records, not just WebSocket payloads. The default transport uses a socket when byte counting is enabled; a custom hook's stream is wrapped the same way. The callback should not throw.

Advanced TLS and transport options require .NET 8; the netstandard2.1 target supports ordinary connections only and returns `ConnectFailed` for these options. On .NET 8, test TLS connections against a local server using a test CA and `TestWebSocket` for deterministic message-level tests.
