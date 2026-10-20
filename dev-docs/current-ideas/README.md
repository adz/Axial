# Current ideas

This folder contains unaccepted Axial design sketches. Promote accepted rules into `AGENTS.md` or `dev-docs/PLAN.md`, then delete the sketch. Delete rejected or superseded ideas rather than archiving stale specifications.

Active sketches:

- `flow-as-data.md` — make `Flow` a typed description run by several runtimes (ValueTask, continuations, Promise, BEAM), faster than today and with no user-facing change
- `flow-stream-proving.md` — prove resource-safe, portable `FlowStream` semantics through concrete transport slices
- `flow-transport-packages.md` — package and API direction for transports, framing, networking, serial, WebSocket, streaming HTTP/SSE, and compression
- `platform-env-class.md` — a base environment class so applications stop re-implementing every platform `IHas*` interface
- `fsharp-compiler-adapter.md` — pooled-resource combinator for `Axial.Layers`, and an `Axial.FSharpCompiler` adapter, prompted by FsLiveDocs' Axial adoption

Sketches for other projects belong in their own repositories.
