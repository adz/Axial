# WebSocket

`WebSocket.connect` creates connection options. `WebSocket.openSession` obtains a session from the `IHasWebSocket.WebSocket` service; `WebSocket.withConnection` disposes it when the scope ends. `TestWebSocket` provides a scripted transport for application tests.

Use `WebSocket.subprotocol` and `WebSocket.maxMessageBytes` to configure negotiation and the receive limit. `WebSocket.tls` accepts a `WebSocketTls` record: `ClientCertificate` supplies a client identity, `TrustAnchors = Some roots` validates the server against **only** those roots (not the system store), and `MinimumVersion = TlsVersion.Tls13` restricts negotiation to TLS 1.3. `None` uses platform server trust. An empty custom-root list trusts no servers.

`WebSocket.connectWith` supplies a `ConnectContext -> Flow<unit, WebSocketError, Stream>` hook for transport connections. The returned stream belongs to the session. `WebSocket.onBytes` receives `BytesDirection.Sent` or `Received` and the number of bytes transferred by the transport stream. Counts include the HTTP upgrade and TLS records, not just WebSocket payloads. The default transport uses a socket when byte counting is enabled; a custom hook's stream is wrapped the same way. The callback should not throw.

Advanced TLS and transport options require .NET 8; the netstandard2.1 target supports ordinary connections only and returns `ConnectFailed` for these options. On .NET 8, test TLS connections against a local server using a test CA and `TestWebSocket` for deterministic message-level tests.
