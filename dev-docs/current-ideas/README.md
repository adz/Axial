# Current ideas

This folder contains unaccepted Axial design sketches. Promote accepted rules into `AGENTS.md` or `dev-docs/PLAN.md`, then delete the sketch. Delete rejected or superseded ideas rather than archiving stale specifications.

Active sketches:

- `flow-as-data.md` — make `Flow` a typed description run by several runtimes (ValueTask, continuations, Promise, BEAM), faster than today and with no user-facing change
- `flow-stream-proving.md` — prove resource-safe, portable `FlowStream` semantics through concrete transport slices
- `flow-transport-packages.md` — package and API direction for transports, framing, networking, serial, WebSocket, streaming HTTP/SSE, and compression
- `streaming-uploads.md` — multipart uploads in the HTTP adapters as a `FlowStream` of parts, each file a stream of byte chunks under the per-endpoint body limit
- `fsharp-compiler-adapter.md` — pooled-resource combinator for `core Axial`, and an `Axial.FSharpCompiler` adapter, prompted by FsLiveDocs' Axial adoption
- `capabilities.md` — what is still an idea after capabilities shipped: console output outside `IConsole`, exit, and finding capabilities on NuGet
- `configuration.md` — configuration, culture and time zone (T32); decided, shipping in 0.12.0
- `comparative-benchmarks.md` — Axial and FIO concurrency benchmarks side by side, as a diagnostic tool
- `fast-feedback.md` — measured edit → build → test latency and how to shorten it
- `fiber-dump-root-waits-attach.md` — fiber dumps that show the root fiber, what each fiber waits on, and late attach
- `richer-effects.md` — AlgEff and language-ext `Eff<RT, A>` compared with Axial capabilities (research)
- `schedule-expansion.md` — schedule decisions deferred past 1.0

The Guardrails sketches (fast mode, policy review, exceptionless rules, protected files) are implemented; their
lasting decisions are in `dev-docs/decisions/README.md`.

Sketches for other projects belong in their own repositories.
