# Flow Transport, Protocol, and Compression Packages

Status: active design sketch layered on [`flow-stream-proving.md`](flow-stream-proving.md). This file assumes that
document's FlowStream lifecycle, cancellation, scope, backpressure, portability, testing, and 1.0 proving decisions.
It describes only additional package and API requirements. None of this is accepted architecture until concrete proving
slices validate it.

Sequence the FlowStream proving work and these package slices immediately after the repository/package split described
in `project-split.md`.

## Outcome

Build a regular family of Flow satellite packages for byte transports, framed protocols, Network, Serial, WebSocket,
streaming HTTP/SSE, and Compression. Common operations should be short and obvious. Uncommon platform operations must
remain reachable through typed connection capabilities without forcing callers to abandon Flow, framing, or scoped
resource ownership.

The packages compress operational knowledge into:

- immutable specifications with safe defaults;
- types that distinguish byte streams, messages, and datagrams;
- scoped acquisition and `with...` helpers;
- application-oriented typed errors;
- bounded framing and decoding;
- qualified functions with predictable names;
- explicit platform availability and escape hatches.

## Package graph

Proposed dependency direction:

```text
Axial.Flow
    |
    +-- Axial.Flow.Transport
    |       +-- byte/message capabilities
    |       +-- framing
    |       +-- protocol errors
    |
    +-- Axial.Flow.Network
    |       +-- DNS/endpoints/interfaces
    |       +-- TCP/UDP/Unix sockets
    |       +-- TLS
    |
    +-- Axial.Flow.Serial
    |
    +-- Axial.Flow.HttpClient
    |       +-- streaming request/response bodies
    |       +-- SSE
    |
    +-- Axial.Flow.WebSocket
    |
    +-- Axial.Flow.Compression
```

Exact package count should follow implementation evidence. `Transport` and framing may begin together to avoid
premature package fragmentation. They should split only if pure framing becomes useful without Flow transport types or
dependency weight makes the boundary valuable.

`Axial.Flow` must not depend on any satellite. Process may later depend on the shared framing package, but framing must
not depend on Process, Network, Serial, or HttpClient.

No public package should depend on `Axial.ErrorHandling`. Each package owns its operational error types or accepts
caller-provided codec errors.

## Cross-package API grammar

Regularity is a correctness feature. Use the same grammar everywhere unless the underlying concept differs.

### Specifications

- Specifications are immutable opaque values.
- One noun-specific constructor creates a valid common configuration.
- Pipeable modifiers refine it.
- Invalid static values fail immediately with `invalidArg`.
- Operational validation remains in typed Flow errors.
- `plan` returns a redacted, serializable description where diagnostics or review benefit from it.

Examples:

```fsharp
Tcp.connect "device.local" 9000
|> Tcp.noDelay
|> Tcp.connectTimeout (TimeSpan.FromSeconds 5.0)

Serial.port "/dev/ttyUSB0"
|> Serial.baudRate 115200
|> Serial.handshake Handshake.None

WebSocket.connect "wss://central.example.com/ocpp/CP001"
|> WebSocket.subprotocol "ocpp1.6"
```

Do not use large record literals as the primary authoring API. They make defaults invisible, expose fields that should
remain constrained, and are harder to update compatibly.

### Operations

Use these verbs consistently:

```text
connect     acquire a remote connection
listen      acquire a listener
accept      acquire one inbound connection
serve       accept and handle connections with bounded concurrency
open        acquire a named local resource such as a serial port
bind        acquire a local datagram endpoint
send        send one application-level value
receive     receive one value or expose a receive FlowStream
close       perform a protocol close when meaningful
with...     acquire, use, and release one resource in a child scope
plan        inspect a redacted immutable specification
```

Avoid synonyms such as `start`, `createClient`, `openConnection`, `dial`, and `writeMessage` for the same concepts.
Platform terminology may remain where it communicates a genuinely different operation, such as TCP half-close or UDP
join-multicast-group.

### Argument order

- Required configuration precedes the subject.
- Subject is last for pipeline use.
- Data precedes connection for `send` and `write`.
- Decoder/encoder definitions precede input.
- Functions do not switch argument order between packages.

Examples:

```fsharp
connection |> Tcp.send bytes
socket |> Udp.send destination payload
session |> WebSocket.sendText text
response |> Sse.events
stream |> Compression.gzipDecompress options
```

### Safe path and advanced path

Each package provides:

1. a safe common path using specifications and scoped helpers;
2. typed capability operations for uncommon cases;
3. a narrowly named native escape hatch only where complete platform reach requires it.

The safe path appears in all introductory examples. Native escape hatches are qualified under `Native`, explicitly
platform-bound, and never required for ordinary framing, cancellation, or lifetime control.

## Shared transport model

### Distinct transport shapes

Do not erase semantic boundaries:

```text
ByteDuplex<'error>       ordered bytes; TCP, Unix stream socket, serial, pipe
MessageDuplex<'error,'message> discrete messages; WebSocket
DatagramSocket           discrete addressed packets; UDP
FlowStream               one-way sequence of received or decoded values
```

TCP, Serial, WebSocket, and UDP should not share one universal `Transport` interface. They share only concepts that
preserve their behavior.

### ByteDuplex

`ByteDuplex<'error>` is an opaque capability projected from a resource-owning connection. It does not own the original
connection unless constructed by an owning helper.

Conceptual surface:

```fsharp
[<RequireQualifiedAccess>]
type ReadResult =
    | Data of count: int
    | EndOfStream

type ByteDuplex<'error>

[<RequireQualifiedAccess>]
module ByteDuplex =
    val read:
        Memory<byte> ->
        ByteDuplex<'error> ->
        Flow<unit, 'error, ReadResult>

    val write:
        ReadOnlyMemory<byte> ->
        ByteDuplex<'error> ->
        Flow<unit, 'error, unit>

    val completeWrites:
        ByteDuplex<'error> ->
        Flow<unit, 'error, unit>

    val mapError:
        ('error -> 'nextError) ->
        ByteDuplex<'error> ->
        ByteDuplex<'nextError>
```

Rules:

- `read` fills caller-owned memory and reports the count. It never treats zero-length caller memory as EOF.
- `EndOfStream` is explicit.
- Writes complete only after the implementation has accepted all supplied bytes or return a typed failure.
- One reader may be active. A second concurrent reader is rejected deterministically.
- One reader and one writer may operate concurrently.
- Concurrent writes are serialized in call order by default so framed messages cannot interleave.
- `completeWrites` is idempotent where the platform permits; writing afterward returns a typed closed-direction error.
- Full disposal belongs to the owning connection/scope, not the projected duplex.
- `mapError` must preserve all lifecycle and concurrency invariants.

Low-level reads use caller-provided memory to avoid per-read allocation. Common framed receive APIs emit owned values.
Pipelines, native streams, and socket handles remain internal implementation options.

### MessageDuplex

WebSocket and future message transports use a separate opaque capability:

```fsharp
type MessageDuplex<'error, 'inbound, 'outbound>

[<RequireQualifiedAccess>]
module MessageDuplex =
    val receive:
        MessageDuplex<'error, 'inbound, 'outbound> ->
        FlowStream<unit, 'error, 'inbound>

    val send:
        'outbound ->
        MessageDuplex<'error, 'inbound, 'outbound> ->
        Flow<unit, 'error, unit>

    val mapError: ...
    val mapInbound: ...
    val contramapOutbound: ...
```

Do not use `ByteDuplex` for WebSocket fragments. WebSocket fragmentation is protocol transport detail; consumers
receive complete messages unless they explicitly opt into an advanced streaming-message API.

### Framing

Framing converts arbitrary byte reads into bounded logical frames and encodes outbound frames without assuming native
read boundaries.

Public definitions should be opaque and separated by direction if asymmetric protocols require it:

```fsharp
type FrameDecoder<'value, 'error>
type FrameEncoder<'value, 'error>
type Framing<'inbound, 'outbound, 'error>
type FramedDuplex<'transportError, 'codecError, 'inbound, 'outbound>
```

Common constructors:

```fsharp
Framing.bytesDelimited delimiter
Framing.linesUtf8
Framing.lines encoding
Framing.fixedSize size
Framing.lengthPrefixed16BigEndian
Framing.lengthPrefixed16LittleEndian
Framing.lengthPrefixed32BigEndian
Framing.lengthPrefixed32LittleEndian
```

Common modifiers:

```fsharp
Framing.maxFrameBytes limit
Framing.allowFinalPartialFrame
Framing.rejectFinalPartialFrame
Framing.withDecoder decode
Framing.withEncoder encode
Framing.mapInbound map
Framing.contramapOutbound map
Framing.mapError map
```

`linesUtf8` should define and document:

- LF recognition;
- optional removal of one preceding CR;
- whether the delimiter is excluded from emitted text;
- strict or replacement UTF-8 behavior;
- BOM handling only at stream start;
- empty-line preservation;
- final unterminated line policy;
- maximum encoded byte length, not only decoded character count.

Delimiter framing must support multi-byte delimiters split across reads and overlapping delimiter prefixes. Fixed-size
framing rejects non-positive sizes. Length-prefix framing validates prefix values, configured limits, integer overflow,
and premature EOF before allocating the declared payload.

Framing errors remain distinct from transport errors:

```fsharp
[<RequireQualifiedAccess>]
type ProtocolError<'transportError, 'codecError> =
    | Transport of 'transportError
    | Decode of 'codecError
    | Encode of 'codecError
    | FrameTooLarge of limit: int * observedAtLeast: int64
    | InvalidLength of value: int64 * limit: int
    | UnexpectedEnd of bufferedBytes: int
```

Do not add COBS, SLIP, HDLC, Modbus, protobuf, JSON, or OCPP framing to the foundation until a concrete package proves
the reusable boundary. They can be small protocol packages built from custom decoder and encoder definitions.

### FramedDuplex

```fsharp
[<RequireQualifiedAccess>]
module FramedDuplex =
    val create:
        Framing<'inbound, 'outbound, 'codecError> ->
        ByteDuplex<'transportError> ->
        FramedDuplex<'transportError, 'codecError, 'inbound, 'outbound>

    val receive:
        FramedDuplex<'transportError, 'codecError, 'inbound, 'outbound> ->
        FlowStream<unit, ProtocolError<'transportError, 'codecError>, 'inbound>

    val send:
        'outbound ->
        FramedDuplex<'transportError, 'codecError, 'inbound, 'outbound> ->
        Flow<unit, ProtocolError<'transportError, 'codecError>, unit>

    val completeWrites: ...
```

One receive stream owns decoder state. Requesting another receive stream from the same framed session while the first
is active fails deterministically. Sends serialize complete encoded frames, not individual byte segments.

## Network package

### Scope

`Axial.Flow.Network` owns network observation and native network transport effects:

- endpoint parsing and formatting;
- IP address and DNS resolution;
- local network interface inspection;
- TCP clients and listeners;
- UDP unicast, broadcast, and multicast;
- Unix-domain sockets where supported;
- TLS client/server upgrade over supported byte transports;
- typed access to uncommon socket controls;
- live implementations and layers.

It does not own HTTP, WebSocket, application framing, discovery protocols, message brokers, or web servers.

### Coverage boundaries

Network completeness does not mean forcing every network-shaped platform API into TCP/UDP:

- ICMP echo is a separate message operation, tentatively `Ping.send`, with permission/unsupported errors and no claim
  that success proves application reachability. Add it after TCP/UDP if diagnostics need it.
- Traceroute is not a primitive portable API; it requires repeated ICMP/UDP probes, permissions, timing, and substantial
  platform variation. Keep it outside the initial package or build it as a higher diagnostic package.
- QUIC is multiplexed connections plus streams and datagrams. Do not project an entire QUIC connection to one
  `ByteDuplex`; a future QUIC package can project each acquired bidirectional stream to `ByteDuplex` while retaining
  connection-level stream acceptance, datagrams, TLS, and migration.
- Raw IP sockets require elevated permissions and platform-specific headers. Typed common coverage is not justified;
  the native escape hatch preserves reach on .NET.
- HTTP CONNECT, SOCKS, and platform proxy discovery are higher connection-establishment adapters. Proxy credentials,
  DNS ownership, and tunneling errors must remain explicit. Do not hide proxy use in ordinary `Tcp.connect`.
- Bluetooth, USB, CAN bus, and domain-specific field buses are separate transports even when their native APIs resemble
  sockets or serial ports.
- Network change notifications may later produce FlowStreams of snapshots, but initial completeness requires reliable
  snapshot inspection rather than a cross-platform event promise.

### Service boundary

One giant `INetwork` method catalog would be shallow and hard to fake. Prefer a small acquisition/observation service
whose returned typed capabilities own uncommon operations:

```fsharp
type INetwork =
    abstract Resolve:
        DnsQuery * CancellationToken ->
        ValueTask<Result<IpAddress list, NetworkError>>

    abstract Interfaces:
        CancellationToken ->
        ValueTask<Result<NetworkInterfaceInfo list, NetworkError>>

    abstract ConnectTcp:
        TcpConnectSpec * CancellationToken ->
        ValueTask<Result<TcpConnection, NetworkError>>

    abstract ListenTcp:
        TcpListenSpec * CancellationToken ->
        ValueTask<Result<TcpListener, NetworkError>>

    abstract BindUdp:
        UdpBindSpec * CancellationToken ->
        ValueTask<Result<UdpSocket, NetworkError>>
```

Alternative split services such as `IDns`, `ITcp`, and `IUdp` should be considered if fakes or platform availability
show that callers commonly depend on only one capability. Do not split merely to mirror .NET classes. Environment
requirements must remain understandable in ordinary application records.

`live` takes every additional effect explicitly. Do not inject `IClock` unless Network itself exposes timestamps or
interprets certificate time. Flow timeout policy does not require Network to own a clock.

### Address types

Avoid `System.Net.EndPoint` in portable public specifications. Use explicit values:

```fsharp
type IpAddress

[<RequireQualifiedAccess>]
type IpFamily =
    | V4
    | V6

type IpEndpoint =
    { Address: IpAddress
      Port: int }

[<RequireQualifiedAccess>]
type NetworkHost =
    | Name of string
    | Address of IpAddress

type HostEndpoint =
    { Host: NetworkHost
      Port: int }

type UnixEndpoint
```

Constructors validate port ranges, empty host names, malformed literals, IPv6 zone identifiers, and Unix path/abstract
namespace constraints. Formatting round-trips IPv4 and bracketed IPv6 endpoints. Preserve international domain names
and define whether plans display Unicode or ASCII/punycode form.

Use the same endpoint value in errors, plans, connection metadata, and retry decisions. Never log credentials embedded
in proxy or URI-like input.

### DNS

Common API:

```fsharp
Dns.resolve "device.local"
Dns.resolveV4 "device.local"
Dns.resolveV6 "device.local"
Dns.reverse address
```

Requirements:

- preserve all returned addresses in platform order unless an explicit policy reorders them;
- return an empty successful result only when the platform can distinguish it from resolution failure;
- categorize not-found, temporary failure, malformed name, cancellation/interruption, and platform failure;
- avoid hidden process-global caching promises;
- make custom caching an explicit higher layer with capacity and TTL policy;
- allow TCP connect to perform name resolution internally while retaining the original host in diagnostics;
- support IPv4/IPv6 connection racing as an explicit `Tcp.addressSelection` policy rather than hard-coded accidental
  platform behavior.

DNS lookup is an operational effect and must remain mockable.

### Network interfaces

Expose immutable snapshots sufficient for binding and diagnostics:

```fsharp
type NetworkInterfaceInfo =
    { Id: string
      Name: string
      Description: string option
      Status: InterfaceStatus
      Kind: InterfaceKind
      SupportsMulticast: bool
      Addresses: InterfaceAddress list
      Gateways: IpAddress list
      DnsServers: IpAddress list }
```

Do not promise evented interface-change streams until platform behavior and cleanup can be made regular across .NET and
Fable targets. A later watch API should emit snapshots/deltas through FlowStream and coalesce bursts with bounded state.

### TCP client

Specification construction:

```fsharp
Tcp.connect host port
Tcp.connectEndpoint endpoint
Tcp.connectUnix endpoint

|> Tcp.connectTimeout duration
|> Tcp.addressSelection AddressSelection.SystemDefault
|> Tcp.noDelay
|> Tcp.keepAlive policy
|> Tcp.localBind endpoint
|> Tcp.sendBufferBytes size
|> Tcp.receiveBufferBytes size
|> Tcp.linger policy
```

Safe defaults:

- system address selection until a tested happy-eyeballs policy exists;
- no local bind;
- platform buffer sizes;
- `NoDelay` should follow measured common usage rather than blindly copying `TcpClient`; document chosen default;
- no configured connect timeout unless the higher-level helper supplies one;
- no keepalive unless explicitly requested or a strong cross-platform default is accepted.

Execution:

```fsharp
Network.connect specification

Tcp.withConnection specification (fun connection -> ...)
```

`Network.connect` acquires a scope-owned `TcpConnection`. `Tcp.withConnection` is the common safe path and creates the
smallest useful child scope. Configured connect timeout is interpreted with Flow timing around native acquisition.

Connection API:

```fsharp
Tcp.duplex connection
Tcp.localEndpoint connection
Tcp.remoteEndpoint connection
Tcp.completeWrites connection
Tcp.close connection
Tcp.abort connection
Tcp.setNoDelay enabled connection
Tcp.setKeepAlive policy connection
Tcp.getOption option connection
Tcp.setOption option value connection
```

Prefer explicit typed operations for common socket controls. A typed `SocketOption<'value>` catalog can expose uncommon
portable options without leaking .NET enums. Unsupported options return `Unsupported` with the option and platform.

Edge cases:

- DNS succeeds with several addresses but some connection attempts fail;
- IPv6-only and dual-stack hosts;
- connection refusal versus unreachable route versus timeout;
- cancellation during resolution and during connect;
- connection succeeds at the same instant a timeout/interruption wins;
- partial native initialization before failure;
- peer reset during read or write;
- local half-close followed by continued receiving;
- peer graceful EOF;
- zero-byte application writes;
- socket close while another operation is pending;
- concurrent send callers;
- disposing twice;
- platform-specific socket option support.

The acquisition implementation must ensure a connection that loses a timeout/cancellation race is closed, even if the
native completion arrives afterward.

### TCP listener and serving

Specification:

```fsharp
Tcp.listen endpoint
Tcp.listenAny port
Tcp.listenLoopback port
Tcp.listenUnix endpoint

|> Tcp.backlog count
|> Tcp.dualMode
|> Tcp.reuseAddress
|> Tcp.exclusiveAddressUse
|> Tcp.acceptNoDelay
|> Tcp.acceptKeepAlive policy
```

Raw capability:

```fsharp
Network.listen specification
listener |> Tcp.accept
listener |> Tcp.connections
```

Common server helper:

```fsharp
Tcp.serve
    specification
    (Server.maxConcurrent 256)
    handleConnection
```

`serve`:

- owns listener in its scope;
- stops accepting when canceled or a fatal listener error occurs;
- creates one child scope per accepted connection;
- applies accepted-connection options before invoking handler;
- never exceeds configured handler count;
- stops accepting while capacity is exhausted;
- cancels active handlers during server interruption and awaits cleanup;
- defines whether one handler typed failure stops server, is reported to a callback, or follows an explicit supervision
  policy;
- does not silently retry bind/accept failures;
- removes Unix socket filesystem entries it created, without deleting an unrelated pre-existing path.

Supervision should use a small explicit policy rather than callbacks hidden in optional arguments:

```fsharp
[<RequireQualifiedAccess>]
type ServerFailurePolicy<'error> =
    | Stop
    | Continue of report: ('error -> Flow<_, Never, unit>)
```

Only add restart/backoff after a real long-running service demonstrates the need. Flow retry policy remains the base.

### UDP

UDP remains datagram-native:

```fsharp
type UdpDatagram =
    { Payload: byte array
      RemoteEndpoint: IpEndpoint
      LocalEndpoint: IpEndpoint option
      Truncated: bool }

Udp.bind endpoint
Udp.bindAny port
Udp.connect endpoint

Network.bindUdp specification
Udp.receive socket
Udp.datagrams socket
Udp.send destination payload socket
Udp.sendConnected payload socket
```

`connect` for UDP sets the default peer/filter; it does not imply TCP-style handshake or delivery.

Options:

```fsharp
Udp.broadcast
Udp.receiveBufferBytes
Udp.sendBufferBytes
Udp.dontFragment
Udp.timeToLive
Udp.multicastTimeToLive
Udp.multicastLoopback
Udp.joinMulticastGroup
Udp.leaveMulticastGroup
```

Requirements:

- preserve one datagram per emitted value;
- report original remote endpoint;
- report truncation instead of silently treating truncated payload as complete;
- allow caller-configured maximum datagram bytes with a safe valid default;
- distinguish connected and destination-supplied send APIs;
- preserve zero-length datagrams;
- do not promise delivery, ordering, uniqueness, or backpressure from the peer;
- serialize configuration mutations relative to disposal;
- model multicast interface selection explicitly for IPv4 and IPv6;
- treat ICMP-derived connection reset behavior consistently where platforms differ;
- keep receive buffering bounded by FlowStream pull semantics and explicit prefetch only.

UDP server-style concurrency belongs in application handling of `Udp.datagrams`; it does not create per-datagram child
connections.

### Unix-domain sockets

Unix stream sockets project to the same byte duplex as TCP while retaining Unix metadata and errors. Support path and
abstract namespace addresses only where the target supports them.

Required edge cases:

- path length limits;
- stale socket path;
- ownership of path deletion;
- permission failure;
- peer credentials where supported;
- unsupported targets;
- no fake TCP fallback.

Expose peer credentials through a qualified capability operation. Do not put Unix-only fields into every TCP
connection record.

### TLS

TLS is an explicit upgrade or connect step, never inferred from port number:

```fsharp
Tls.client serverName
|> Tls.protocols [ TlsProtocol.Tls13; TlsProtocol.Tls12 ]
|> Tls.applicationProtocols [ "ocpp1.6" ]
|> Tls.certificatePolicy CertificatePolicy.systemTrust

Tls.upgradeClient specification duplex
Tcp.withTlsConnection tcpSpec tlsSpec use
```

Server-side TLS may follow when Network serving needs it:

```fsharp
Tls.server certificateSource
Tls.upgradeServer specification duplex
```

Certificate policy must be explicit and mockable. It should support:

- system trust;
- pinned public key/certificate;
- custom validation represented by a named service or explicit callback dependency;
- client certificate selection;
- hostname validation;
- revocation policy;
- ALPN;
- protocol bounds.

Do not provide `acceptAnyCertificate` in the common module. If an unsafe test-only policy exists, place it under an
obviously unsafe qualified module.

TLS errors distinguish handshake, trust, hostname, protocol, alert, EOF, and underlying transport failures without
exposing secrets. Certificate validity checks require an explicit clock if Axial performs them; otherwise the TLS
service contract must make platform trust behavior explicit rather than pretending it is deterministic.

TLS record framing remains internal. The result is another `ByteDuplex<TlsError<NetworkError>>` plus negotiated
metadata. Closing attempts `close_notify` within the owner scope but cannot block cleanup indefinitely.

### Network errors

Errors should support application decisions while retaining safe native detail:

```fsharp
[<RequireQualifiedAccess>]
type NetworkOperation =
    | Resolve
    | InspectInterfaces
    | Connect
    | Bind
    | Listen
    | Accept
    | Read
    | Write
    | Shutdown
    | Configure

[<RequireQualifiedAccess>]
type NetworkError =
    | InvalidEndpoint of message: string
    | NameNotFound of host: string
    | NameResolutionFailed of host: string * message: string
    | ConnectionRefused of endpoint: string
    | ConnectionReset of endpoint: string option
    | Unreachable of endpoint: string
    | AddressInUse of endpoint: string
    | PermissionDenied of operation: NetworkOperation * endpoint: string option
    | TimedOut of operation: NetworkOperation * endpoint: string option * timeout: TimeSpan
    | Closed of operation: NetworkOperation
    | Unsupported of operation: NetworkOperation * detail: string
    | IoFailed of operation: NetworkOperation * endpoint: string option * message: string
```

Caller interruption should remain Flow interruption under the decision in `flow-stream-proving.md`; do not add a
typed `Canceled` merely because a native API throws an operation-canceled exception.

Provide:

```fsharp
NetworkError.describe
NetworkError.operation
NetworkError.isTransient
NetworkError.tryEndpoint
NetworkError.transientPolicy
```

Retry helpers should not retry invalid endpoints, permission failures, address-in-use, unsupported operations, local
closed-state errors, or interruption. Connect retry does not imply replaying application writes.

### Platform support

.NET implementation is default and may use sockets and pipelines internally. Do not suffix the normal package with
`.DotNet`.

Browser Fable does not expose raw DNS, TCP, UDP, interface inspection, Unix sockets, or arbitrary TLS. Those modules
should be absent from browser-compatible builds/packages rather than return `Unsupported` for every operation. If one
NuGet/npm artifact must contain shared declarations, target availability must still be compile-time obvious.

Node, Deno, Bun, or future Fable targets may implement subsets through target-specific internals or integration
packages. Do not contort the portable public Network contract around browser limitations.

Native escape hatch on .NET:

```fsharp
Network.Native.socket connection
Network.Native.listenerSocket listener
```

Its presence makes complete platform access possible while typed Axial operations should cover common and uncommon
portable cases. Using it explicitly accepts platform coupling; it does not transfer resource ownership.

## Serial package

### Scope

`Axial.Flow.Serial` owns:

- port enumeration and immutable port metadata;
- serial configuration;
- scoped open/close;
- byte reads/writes through `ByteDuplex`;
- flush/discard operations;
- modem control outputs;
- modem status and change events where reliable;
- break signaling;
- typed platform errors;
- live implementation and layer.

It does not own device protocols, line parsing, Modbus, AT commands, reconnect loops, or USB device discovery beyond
metadata the serial platform reliably supplies.

### Service boundary

```fsharp
type ISerial =
    abstract Ports:
        CancellationToken ->
        ValueTask<Result<SerialPortInfo list, SerialError>>

    abstract Open:
        SerialSpec * CancellationToken ->
        ValueTask<Result<SerialConnection, SerialError>>
```

`SerialConnection` is opaque, scope-owned, and projects to `ByteDuplex<SerialError>`.

### Port enumeration

```fsharp
Serial.ports
Serial.tryFindPort predicate
```

Metadata should be optional where platforms cannot supply it:

```fsharp
type SerialPortInfo =
    { Name: string
      Description: string option
      HardwareId: string option
      VendorId: uint16 option
      ProductId: uint16 option
      SerialNumber: string option }
```

Never use friendly description as stable identity. Port names can change after reconnect. Device matching by VID/PID
may still be ambiguous; return all matches and force callers to decide.

Port watching is a later FlowStream feature built from native notifications or bounded polling with explicit clock and
interval dependencies. Do not imply reliable hot-plug events on platforms that cannot provide them.

### Configuration

Primary authoring path:

```fsharp
Serial.port "/dev/ttyUSB0"
|> Serial.baudRate 115200
|> Serial.dataBits 8
|> Serial.parity Parity.None
|> Serial.stopBits StopBits.One
|> Serial.handshake Handshake.None
|> Serial.dtr false
|> Serial.rts false
|> Serial.readBufferBytes 4096
|> Serial.writeBufferBytes 4096
|> Serial.openTimeout (TimeSpan.FromSeconds 3.0)
```

Constructor defaults are conventional 8-N-1, no handshake. DTR/RTS defaults must be documented because opening some
devices toggles reset lines. If cross-platform behavior cannot be made safe, require explicit control-line policy
rather than surprising devices.

Validate:

- non-empty port name;
- positive supported baud rate representation without assuming a short fixed catalog;
- data-bit range;
- stop-bit combinations;
- handshake/control-line conflicts;
- positive buffer sizes;
- non-negative timeouts;
- encoding only in higher text framing, never in SerialSpec.

Do not put read delimiters or newline strings in SerialSpec. Framing can change during one session, such as an ASCII
handshake followed by binary payloads.

### Acquisition and common use

```fsharp
Serial.open specification

Serial.withPort specification (fun port ->
    port
    |> Serial.duplex
    |> FramedDuplex.create framing
    |> DeviceProtocol.run)
```

Configured open timeout is Flow policy around acquisition. Closing the scope disposes the native port and interrupts
pending reads/writes using the best platform mechanism.

Partial-open cleanup covers failures after the native handle opens but before buffers, control lines, or stream state
finish configuring.

### Connection operations

```fsharp
Serial.duplex port
Serial.info port
Serial.flush port
Serial.discardInput port
Serial.discardOutput port
Serial.setDtr enabled port
Serial.setRts enabled port
Serial.setBreak enabled port
Serial.signals port
Serial.close port
```

`flush` semantics must be named precisely. If the platform distinguishes flushing managed buffers from waiting for
physical transmission, expose distinct operations instead of one ambiguous verb.

`discardInput` and `discardOutput` are destructive, connection-local operations. They should be explicit Flow effects,
never automatic recovery behavior.

`signals` emits meaningful changes only:

```fsharp
type SerialSignals =
    { CarrierDetect: bool
      ClearToSend: bool
      DataSetReady: bool
      RingIndicator: bool }
```

If reliable change notification is unavailable, expose snapshot reads first. A polling implementation requires an
explicit clock/schedule and must not be hidden in `ISerial`.

Platform-specific serial modes such as RS-485 driver-enable timing should receive typed qualified operations when a
real device requires them and more than one platform can support a coherent meaning. Until then, the native escape
hatch preserves access without adding misleading portable fields to every `SerialSpec`.

### Read/write behavior

- Do not drive primary reads from `.NET SerialPort.DataReceived`; event timing and byte counts are unreliable.
- Read bytes according to consumer demand.
- Preserve partial reads as ordinary byte-stream behavior.
- Never infer message boundaries from idle gaps in the base transport.
- Permit one reader and one serialized writer lane.
- Treat device removal, hangup, and native invalid-handle errors as disconnection where identifiable.
- Do not silently reconnect. Reconnection may reopen a different physical device under the same port name.
- Do not retry partial writes without knowing how many bytes the native layer accepted.
- Treat control-line changes during disposal deterministically.
- Ensure a blocked read terminates when the Flow scope closes, including platforms where cancellation tokens are
  ignored by the serial implementation.

### Serial errors

```fsharp
[<RequireQualifiedAccess>]
type SerialOperation =
    | Enumerate
    | Open
    | Read
    | Write
    | Flush
    | Configure
    | Control
    | Close

[<RequireQualifiedAccess>]
type SerialError =
    | InvalidConfiguration of message: string
    | PortNotFound of port: string
    | PortBusy of port: string
    | PermissionDenied of port: string * operation: SerialOperation
    | Disconnected of port: string
    | TimedOut of port: string * operation: SerialOperation * timeout: TimeSpan
    | Closed of operation: SerialOperation
    | Unsupported of operation: SerialOperation * message: string
    | IoFailed of port: string * operation: SerialOperation * message: string
```

Provide `describe`, `operation`, `isTransient`, and `tryPort`. `PortBusy` may be transient; invalid configuration and
permission failure normally are not. Caller interruption remains Flow interruption.

### Native reach and platform support

.NET remains default. `System.IO.Ports` may be an explicit package dependency of `Axial.Flow.Serial`; its version and
platform limitations must not leak into Flow core.

Expose a .NET native handle only under:

```fsharp
Serial.Native.serialPort connection
```

This supports uncommon platform functionality without making `SerialPort` the normal API. Ownership remains with the
Axial scope.

Browser Fable has no general serial support except Web Serial, which has user-gesture, permissions, stream, and browser
availability semantics unlike desktop serial. Treat Web Serial as a future target-specific implementation or package,
not a transparent fallback. It may still project an acquired browser port to the same `ByteDuplex` after explicit
permission acquisition.

### Testing

Beyond FlowStream laws:

- scripted arbitrary fragmentation/coalescing;
- disconnect during acquisition, read, write, flush, and close;
- blocked read interrupted by scope closure;
- port busy and permission mapping;
- partial initialization cleanup;
- control signal transitions;
- zero-byte and large writes;
- concurrent sends preserve frame ordering;
- pseudo-terminal integration on supported CI;
- fake port supports deterministic device protocol tests without physical hardware.

## WebSocket package

### Scope and placement

`Axial.Flow.WebSocket` owns WebSocket client connections on .NET and Fable. Server upgrade integration belongs with a
future web-server adapter and is outside this plan, although connection/message types should not prevent it.

WebSocket depends on Flow and shared message transport concepts. It may depend on Network/TLS on .NET only if doing so
does not force browser implementations through unavailable raw transports. Prefer a service boundary that allows
native `ClientWebSocket` and browser `WebSocket` implementations behind one application-facing API.

### Service boundary

```fsharp
type IWebSocket =
    abstract Connect:
        WebSocketSpec * CancellationToken ->
        ValueTask<Result<WebSocketConnection, WebSocketError>>
```

The connection is opaque, scope-owned, and exposes messages plus WebSocket-specific state and close operations.

### Specification

```fsharp
WebSocket.connect "wss://central.example.com/ocpp/CP001"
|> WebSocket.subprotocol "ocpp1.6"
|> WebSocket.header "Authorization" value
|> WebSocket.secretHeader "Authorization" value
|> WebSocket.connectTimeout duration
|> WebSocket.maxMessageBytes (1024 * 1024)
|> WebSocket.keepAlive policy
```

Validate URI scheme, absolute URI, subprotocol grammar, duplicate subprotocols, maximum size, and timeout values.
Plans redact secret headers and URI credentials/query values explicitly marked secret.

Browser WebSocket cannot set arbitrary request headers or inspect the complete handshake response. Such modifiers must
either be unavailable for browser-targeted code or produce a specification capability error before connection; do not
silently ignore them. Query tokens and cookies have different security implications and should not be substituted
automatically.

### Messages

```fsharp
[<RequireQualifiedAccess>]
type WebSocketMessage =
    | Text of string
    | Binary of byte array

WebSocket.messages connection
WebSocket.send message connection
WebSocket.sendText text connection
WebSocket.sendBinary bytes connection
```

Default receive emits complete owned messages. Native fragments are accumulated with a configured maximum. Text uses
strict UTF-8 and fails invalid messages at protocol level. Empty text and binary messages are preserved.

Advanced very-large-message streaming may later expose message metadata plus a scoped fragment stream. It must not
complicate the common complete-message API before an actual consumer requires it.

WebSocket compression extensions such as `permessage-deflate` are negotiated connection behavior, not application use
of `Axial.Flow.Compression`. Expose a negotiation policy and negotiated metadata where native platforms permit it.
Never manually compress application payloads while also claiming WebSocket extension semantics, and enforce decoded
message limits after decompression.

One receive stream may be active. Sends are serialized by message. Implementations must not interleave fragments from
different sends. One receive and one send may run concurrently.

### Close and control frames

```fsharp
WebSocket.close status reason connection
WebSocket.abort connection
WebSocket.state connection
WebSocket.negotiatedSubprotocol connection
```

Requirements:

- distinguish graceful local close, graceful remote close, abnormal EOF, protocol error, and scope interruption;
- validate close status and UTF-8 reason length before sending;
- respond to native ping/pong requirements according to platform behavior;
- keep ping/pong frames out of ordinary application messages;
- expose explicit ping only where platform APIs support it regularly;
- bound graceful close waiting so scope cleanup cannot hang forever;
- make `close` idempotent;
- reject sends after close starts;
- allow receive to observe remote close metadata in a final result/error without inventing an ordinary fake message.

The precise receive completion representation needs proving. Normal remote close may end `messages` and make close
metadata queryable. Policy-violating or abnormal close should return a typed error. Avoid requiring every consumer to
pattern-match a `Closed` element after every message.

### OCPP-shaped use

WebSocket must make this natural without containing OCPP itself:

```fsharp
WebSocket.withConnection specification (fun connection ->
    flow {
        let incoming = connection |> WebSocket.messages
        let! receiver = incoming |> FlowStream.runForEachFlow handleOcppMessage |> Flow.fork
        do! sendBootNotification connection
        do! heartbeatLoop connection
        return! Flow.join receiver
    })
```

Higher OCPP code owns correlation IDs, CALL/CALLRESULT/CALLERROR decoding, outstanding request limits, heartbeat
messages, reconnect/backoff, and resumption. WebSocket supplies reliable message/lifetime semantics.

Reconnect is not part of one `WebSocketConnection`. A higher `WebSocket.run` helper may repeatedly acquire sessions
using Flow retry/schedule policy, but it must expose session boundaries and never replay sent messages implicitly.

### Errors

```fsharp
[<RequireQualifiedAccess>]
type WebSocketError =
    | InvalidRequest of message: string
    | HandshakeFailed of request: string * status: int option * message: string
    | SubprotocolMismatch of expected: string list * actual: string option
    | MessageTooLarge of limit: int * observedAtLeast: int64
    | InvalidText of message: string
    | ProtocolFailed of message: string
    | ClosedAbnormally of status: int option * reason: string option
    | TimedOut of operation: string * timeout: TimeSpan
    | Unsupported of message: string
    | IoFailed of operation: string * message: string
```

Provide `describe`, `isTransient`, `tryCloseStatus`, and `tryHandshakeStatus`. Never include secret headers or full
credential-bearing URLs in errors.

### Platform behavior

.NET may expose richer handshake, proxy, certificate, buffer, and keepalive controls. Browser behavior is constrained by
the browser. Same operation should have the same semantic result where both platforms support it; unsupported
configuration must be explicit.

Fable tests cover queued browser events arriving before the first pull, bounded buffering, event-listener removal,
close/error event races, and `AbortSignal`-driven scope interruption.

## Streaming HTTP and SSE

### HttpClient evolution

Current HttpClient buffers the complete response. Preserve that ergonomic path while adding explicit streaming:

```fsharp
Http.send request          // buffered HttpResponse
Http.stream request        // scoped StreamingHttpResponse
Http.withResponse request use
```

Do not change `Http.send` to return a streaming body. Buffered response remains correct for common JSON/text requests.

Conceptual streaming response:

```fsharp
type StreamingHttpResponse

Response.statusCode response
Response.reasonPhrase response
Response.headers response
Response.body response
```

`Response.body` returns a single-consumer `FlowStream<unit, HttpError, byte array>`. Headers are available after the
response arrives, before body completion. The response owns the native body resource.

Common safe form:

```fsharp
Http.withResponse request (fun response ->
    response
    |> Response.body
    |> FlowStream.runForEachFlow consumeChunk)
```

If `Http.stream` returns a scope-owned response directly, docs must show the required surrounding Flow scope. Prefer
`withResponse` for examples and LLM guidance.

### Streaming request bodies

Add only after response streaming is correct:

```fsharp
Request.streamBody contentType body
```

where `body` is a FlowStream of owned byte chunks or a scoped byte-source capability. Requirements:

- propagate server/request cancellation upstream;
- do not pre-buffer the entire body;
- serialize chunks in order;
- define content length versus chunked/streaming transfer;
- reject retry policies that would replay a non-replayable body unless caller provides a fresh-body factory;
- release body resources if request construction, connection, or response fails;
- expose upload progress as an optional mapped stream/callback without a hidden unbounded queue.

### Response policies

Streaming response must preserve existing status expectation behavior. Status failure may carry headers and a bounded
diagnostic body preview, but must not buffer an unbounded error body. Caller can opt into accepting any status and
streaming it.

Automatic decompression interacts with Compression:

- either native Http implementation decodes declared content encoding and removes/updates headers consistently;
- or caller explicitly composes `Response.body |> Compression.decodeContentEncoding headers`;
- never decode twice;
- expose original versus effective headers if diagnostics need both;
- protect against decompression bombs with explicit decoded-byte limits.

Redirects, authentication retries, and transient retries cannot blindly replay streaming request bodies. Policy must
know whether body construction is replayable.

### SSE parsing

SSE is a high-level interpretation of a successful `text/event-stream` response:

```fsharp
type SseEvent =
    { EventType: string option
      Data: string
      Id: string option
      Retry: TimeSpan option }

Sse.connect request
Sse.events response
```

Preferred common use:

```fsharp
Http.get url
|> Request.bearer token
|> Request.accept "text/event-stream"
|> Sse.connect
|> FlowStream.runForEachFlow handleEvent
```

Parser requirements:

- require/validate successful status according to request expectation;
- validate media type while allowing parameters such as charset where specification permits;
- decode UTF-8 incrementally across arbitrary byte chunks;
- remove one initial UTF-8 BOM;
- handle CRLF, CR, and LF line endings across chunk boundaries;
- ignore comment lines while allowing them to keep the transport active;
- concatenate multiple `data:` lines with LF and remove the final inserted LF;
- preserve empty `data:` values;
- use the first colon as field separator and remove at most one leading space from value;
- ignore unknown fields;
- update last-event ID according to empty/non-empty `id` rules and reject forbidden null characters;
- parse non-negative decimal `retry` safely with overflow handling;
- dispatch on blank line;
- define EOF behavior for a pending unterminated event according to the SSE specification;
- bound line bytes, event data bytes, and retained last-event ID;
- never emit comments as fake application events unless a separate diagnostics/heartbeat API is requested.

### SSE reconnection

Parsing one connection and reconnecting are separate layers:

```fsharp
Sse.connect request

Sse.run
    reconnectPolicy
    requestFactory
    handleEvent
```

Reconnect behavior:

- starts from caller delay and accepts valid server `retry` updates;
- sends `Last-Event-ID` after receiving an ID;
- does not reconnect after caller interruption;
- categorizes statuses that permit or forbid reconnect;
- uses Flow schedule/clock rather than private timers;
- exposes session failures to a caller policy;
- does not duplicate application side effects under an exactly-once claim;
- bounds repeated immediate failures;
- reconstructs a fresh request for every attempt so auth can refresh;
- redacts event IDs if caller marks them sensitive.

Keep `Sse.connect` simple and composable. `Sse.run` is optional until a real consumer proves policy shape.

### SSE platform behavior

Browser `EventSource` is convenient but restricts headers, methods, body, and error detail. Prefer implementing the
regular API over streaming `fetch` where supported. A separate native `EventSource` adapter can serve simple public GET
streams if it can preserve cancellation and error semantics.

.NET uses streaming `HttpClient` completion mode and must not dispose response content before FlowStream finishes.

## Compression package

### Scope

`Axial.Flow.Compression` provides bounded, streaming transforms and archive traversal over FlowStream/byte capabilities.
Compression algorithms are deterministic computation; they do not need an environment service merely because .NET
uses disposable stream objects internally. File access remains an explicit FileSystem effect outside Compression.

Initial formats:

- gzip;
- zlib/deflate with explicitly named wrapper semantics;
- Brotli;
- zip archive reading/writing where streaming constraints are honest;
- tar archive reading/writing, possibly separate if archive scope grows.

Do not label raw DEFLATE and zlib-wrapped DEFLATE both as `deflate` without distinction.

### Streaming transforms

Common API:

```fsharp
source |> Compression.gzipDecompress options
source |> Compression.gzipCompress options
source |> Compression.brotliDecompress options
source |> Compression.brotliCompress options
source |> Compression.zlibDecompress options
source |> Compression.zlibCompress options
source |> Compression.rawDeflateDecompress options
source |> Compression.rawDeflateCompress options
```

Input and output are FlowStreams of owned byte chunks. Options are immutable and pipeable:

```fsharp
Compression.defaults
|> Compression.inputBufferBytes 32768
|> Compression.outputChunkBytes 32768
|> Compression.maxOutputBytes (64L * 1024L * 1024L)
|> Compression.compressionLevel CompressionLevel.Balanced
```

Convenience operations for bounded materialization:

```fsharp
Compression.gzipDecompressBytes options bytes
Compression.gzipCompressBytes options bytes
```

Names must state format and direction. Avoid one `transform` function taking several loosely related flags.

### Transform behavior

- Pull compressed input only when decoder needs it.
- Emit bounded owned output chunks.
- Release codec state when downstream stops early.
- Propagate upstream typed errors without wrapping them as compression format errors.
- Distinguish upstream error, invalid compressed data, checksum failure, unsupported feature, and configured output
  limit.
- Detect truncated streams at EOF.
- Handle empty valid streams.
- Support concatenated gzip members according to an explicit policy.
- Validate gzip header optional-field sizes to avoid unbounded retention.
- Do not trust declared uncompressed sizes.
- Track total decoded bytes with overflow-safe arithmetic.
- Stop before emitting bytes beyond `maxOutputBytes`.
- Define trailing-data policy: reject, ignore, or return remainder through an advanced API.
- Ensure flush/final block behavior produces a complete stream when compression source ends.
- Avoid emitting meaningless empty chunks.
- Maintain stack safety for tiny input chunks.

Error composition should preserve upstream errors:

```fsharp
[<RequireQualifiedAccess>]
type CompressionError<'sourceError> =
    | Source of 'sourceError
    | InvalidData of format: CompressionFormat * message: string
    | ChecksumFailed of format: CompressionFormat
    | Truncated of format: CompressionFormat
    | OutputLimitExceeded of limit: int64
    | Unsupported of format: CompressionFormat * feature: string
```

### Compression levels

Use a small portable vocabulary:

```fsharp
[<RequireQualifiedAccess>]
type CompressionLevel =
    | Fastest
    | Balanced
    | Smallest
    | PlatformDefault
```

Format-specific expert settings belong in qualified option modules. Do not expose numeric Brotli quality/window knobs
as if they applied to gzip. Platform implementations may map portable levels differently; document that compressed
bytes are not promised identical across runtimes unless deterministic mode is explicitly provided and tested.

### Archive reading

Archive APIs need stronger path and ownership guardrails than raw compression:

```fsharp
type ArchiveEntry =
    { Path: string
      Kind: ArchiveEntryKind
      CompressedBytes: int64 option
      UncompressedBytes: int64 option
      ModifiedAt: DateTimeOffset option }

Zip.entries source
Tar.entries source
```

Each entry should expose a scoped content stream through a handler rather than a freely escaping stream tied to mutable
archive position:

```fsharp
Zip.runEntries options source (fun entry content -> ...)
```

Requirements:

- consume or explicitly skip current entry before advancing;
- prevent concurrent entry-content reads from one sequential archive;
- bound entry count, individual expanded size, and total expanded size;
- validate checksums where format supports them;
- reject encrypted entries unless explicitly supported;
- distinguish unsupported compression method;
- handle data descriptors and non-seekable sources;
- make seek requirements explicit for any random-access API;
- preserve duplicate entry names as separate entries;
- avoid treating archive paths as filesystem paths until extraction.

### Safe extraction

Extraction combines Compression with an explicit `IFileSystem` dependency and belongs in a higher helper, not pure
archive parsing.

Guardrails:

- reject absolute paths;
- reject drive-qualified and UNC paths;
- normalize separators before validation;
- reject `..` traversal after normalization;
- prevent symlink/hardlink escape from destination root;
- define overwrite policy explicitly;
- cap entry count and total bytes;
- clean up partial output according to an explicit policy;
- preserve or ignore permissions/timestamps explicitly;
- avoid creating special device files;
- never follow archive-provided paths outside destination;
- surface which entry failed without leaking unrelated host paths.

Potential common API:

```fsharp
Zip.extract
    (Archive.destination directory
     |> Archive.overwrite Overwrite.Never
     |> Archive.maxEntries 10_000
     |> Archive.maxTotalBytes maxBytes)
    source
```

Because extraction performs filesystem effects, its Flow environment must expose `IFileSystem`. Pure compression and
entry iteration require no filesystem environment.

### Compression and native streams

.NET implementation may adapt FlowStream to internal `Stream`/pipeline machinery. JavaScript may use
`CompressionStream`/`DecompressionStream` where available or a library fallback. Public behavior, limits, and errors
remain regular.

Unsupported browser formats must be explicit. Do not silently buffer entire inputs because a native streaming codec is
unavailable. A buffering fallback must have a strict configured maximum and be named/documented as such.

### Compression tests

- official and cross-tool format vectors;
- empty streams;
- one-byte input chunks;
- one-byte output chunks where supported;
- truncated headers and payloads;
- corrupt checksum;
- concatenated members;
- optional gzip fields at limits;
- maximum-output boundary exactly and one byte over;
- downstream early termination releases codec/upstream;
- upstream failure retains original error;
- archive traversal and duplicate entries;
- zip-slip, symlink escape, absolute path, drive path, and separator variants;
- compression/decompression round trips across levels and runtimes;
- bounded allocation under high expansion ratios.

## Process integration

Process remains its own package and application abstraction. Share only proven lower concepts:

- incremental line/delimiter decoder;
- bounded frame retention;
- possibly byte-source/sink adapters;
- FlowStream lifecycle and backpressure.

Do not replace `ProcessEvent`, stage attribution, timestamps, exit results, or topology with generic transport events.

Split current `OutputFraming` responsibilities when migration proves the shape:

```fsharp
type OutputEvents =
    | Chunks
    | Lines

type MergeBoundary =
    | Chunks
    | Lines
```

Read/event framing and fan-in write atomicity are different policies even when both use line boundaries. A future
framing value may implement both internally, but public Process helpers should remain simple and Process-specific.

Process specifications continue to describe timeouts. Flow interprets timeout and cancellation. Native process code
owns start, tree termination, partial-start cleanup, and native I/O adaptation only.

## Layer and environment conventions

Every operational package follows current service conventions:

```fsharp
Network.live ...
Network.layer ...

Serial.live ...
Serial.layer ...

WebSocket.live ...
WebSocket.layer ...
```

Operations require `IHas<'service>` only while resolving/acquiring the named service. Acquired connections are explicit
capabilities whose operations normally use `Flow<unit, ...>` because dependency is already captured.

Additional effects remain visible:

- explicit clock for package-owned timestamp/certificate-time behavior;
- explicit filesystem for archive extraction or Unix path management delegated outside Network;
- explicit randomness for protocol features Axial generates itself;
- explicit log/telemetry callback if a helper reports failures;
- no ambient environment variables, console, filesystem, clock, random, GUID, or service provider lookup.

Avoid injecting every platform service preemptively. A package with no timestamps does not need `IClock`.

## Plans, diagnostics, and redaction

Long-lived transports should not capture complete transcripts. Provide lightweight immutable plans and metadata:

```fsharp
Tcp.plan specification
Udp.plan specification
Serial.plan specification
WebSocket.plan specification
```

Plans contain configuration safe for logging and review. Redact:

- credentials in URLs;
- explicitly secret headers/query fields;
- proxy credentials;
- certificate private-key material;
- serial data and transport payloads;
- application frames.

Errors carry operation, safe endpoint/port identity, stable category, and bounded native message. Do not embed arbitrary
payload previews by default. Provide `describe` consistently, with structured inspection functions for program logic.

## Guardrails for humans and LLMs

### Type-level constraints

- Separate TCP streams, UDP datagrams, WebSocket messages, and Serial ports.
- Make resource-owning connection types opaque.
- Make specifications opaque and valid after construction.
- Require maximum frame/message/output sizes in constructors or provide conservative documented defaults.
- Represent EOF separately from empty data.
- Represent normal stream completion separately from transport/protocol failure.
- Keep inbound and outbound framed types independently typed.
- Make replayability explicit for streaming HTTP request bodies.
- Make unsafe certificate policy and native handles conspicuously qualified.

### API-shape constraints

- One canonical path per common action.
- No duplicate aliases retained pre-1.0.
- No overload forest differentiated only by native buffer type.
- No optional boolean clusters; use pipeable named modifiers or closed DUs.
- No callbacks for lifecycle when a scoped `with...` helper expresses ownership.
- No generic `Options` record containing unrelated transport switches.
- No method named `run` when `connect`, `listen`, `serve`, `send`, or `receive` communicates intent.
- No automatic retry/reconnect/replay hidden inside basic operations.
- No full buffering hidden behind a streaming name.

### Documentation examples

Every package should teach in this order:

1. scoped common operation;
2. streaming receive/send;
3. typed error mapping at application boundary;
4. uncommon typed capability operation;
5. native escape hatch last.

Examples must include realistic size bounds. Avoid examples that materialize infinite/long-lived streams. Use the same
variable names across packages: `specification`, `connection`, `session`, `messages`, `events`.

Compile end-user examples in tests. Public API tests are copyable documentation and use the intended pipeline form.

## Cross-cutting edge cases

Every package review must answer:

- Who owns each native handle and buffer?
- What closes on normal completion, failure, interruption, and early downstream termination?
- What happens if acquisition succeeds after cancellation wins?
- Which timeout is being specified: acquisition, complete operation, idle, or graceful close?
- Can native work continue after Flow has returned?
- Is partial write progress knowable, and is retry safe?
- Can data be duplicated, reordered, truncated, or coalesced?
- What is maximum retained memory under a malicious peer/input?
- Can one slow consumer cause unbounded native event buffering?
- Are concurrent read/write/configuration operations legal?
- Does a close race return stable outcomes?
- Are diagnostics bounded and redacted?
- Are unsupported target features visible at compile/configuration time?
- Does Fable implementation preserve semantics despite different native event models?
- Can fake services reproduce every application-relevant outcome without native types?

## Delivery phases

### Before 1.0: proving only

Follow `flow-stream-proving.md` and implement narrow internal/public-preview slices:

1. shared byte-duplex and three framing modes;
2. TCP client connection;
3. Serial port connection;
4. WebSocket OCPP-shaped message session;
5. streaming HTTP response and SSE parser;
6. gzip decompression stream;
7. Process migration to shared framing/lifecycle where proven.

These slices establish whether abstractions are deep enough. Do not promise complete satellite stability merely because
their tests exist in the repository.

### After Flow 1.0: package completion

1. Complete TCP listener/serve and connection options.
2. Complete DNS, address selection, interface inspection, and Unix sockets.
3. Complete UDP unicast/broadcast/multicast.
4. Complete TLS client then server upgrade.
5. Complete Serial controls, metadata, signals, and platform matrix.
6. Complete WebSocket platform options, graceful close, reconnect helper if proven by OCPP.
7. Complete streaming HTTP upload/download and SSE reconnection.
8. Complete gzip/zlib/raw-deflate/Brotli transforms.
9. Add archive reading, then guarded extraction.
10. Add target-specific native adapters only when demanded.

Package order can change based on the first real consumer. FlowStream semantics and shared framing must remain ahead of
satellite breadth.

## Acceptance criteria

The package family is coherent when:

- TCP and Serial use the same byte-duplex/framing concepts without losing their specific controls;
- WebSocket preserves message semantics rather than pretending to be a byte stream;
- UDP preserves datagram boundaries and addressing;
- SSE is an ordinary decoded FlowStream over a scoped streaming HTTP response;
- Compression composes as bounded stream transformation without becoming an operational service unnecessarily;
- Process shares framing mechanics without losing process-specific events/results;
- all timeouts are specifications interpreted with Flow timing;
- all acquired resources use Flow scopes and `acquireRelease` behavior;
- common APIs follow the same construction/acquisition/send/receive/with grammar;
- uncommon portable operations remain typed and discoverable;
- complete native access remains possible through explicit platform escape hatches;
- .NET and Fable implementations preserve common semantics while exposing target limitations honestly;
- generated/common code naturally follows safe ownership, size bounds, and cancellation behavior.
