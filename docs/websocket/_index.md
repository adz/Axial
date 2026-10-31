---
title: WebSocket
linkTitle: WebSocket
description: Typed WebSocket client sessions, sends, receives, and WebSocketError.
project: src/Axial.WebSocket/Axial.WebSocket.fsproj
---

# WebSocket

`Axial.WebSocket` wraps `ClientWebSocket` as an explicit service with typed messages and errors. Install it and open
`Axial.WebSocket`:

```bash
dotnet add package Axial.WebSocket
```

```fsharp
open Axial.WebSocket
```

`WebSocket.connect` creates connection options, `WebSocket.openSession` obtains a session from `IHasWebSocket`, and
`WebSocket.withConnection` disposes it when the scope ends. `TestWebSocket` supplies a scripted transport for tests.

## Guides

1. [WebSocket sessions](/websocket/websocket.html): connect, send and receive typed messages, handle errors, and test without a live server.

## Reference

The generated [WebSocket API](xref:M:Axial.WebSocket.WebSocket.connect) lists connection, session, message, and error types.
