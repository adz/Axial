Goal: Validate and deepen FsFlow v1 service integration for Core, FileSystem, and IServiceProvider boundaries.

  Objective:
  Prove the explicit service/layer model works consistently in the current service architecture, while starting the “competent .NET dev
  can find almost everything they expect” service-package effort with Core and FileSystem only.

  Scope:
  - Deepen `FsFlow.Services.Core`.
  - Deepen `FsFlow.Services.FileSystem`.
  - Validate `IServiceProvider` boundaries.
  - Keep Console, Http, and Process light for now, but align terminology and shape with the service/layer model.
  - Spin out near-complete Console, Http, Process, Network, Telemetry service expansion as later tasks.

  Work:

  1. Audit current service-package architecture.
     - Inspect Core, FileSystem, Console, Http, Process, Hosting, and docs.
     - Confirm current terminology uses:
       - service
       - service implementation
       - layer
       - provider edge
     - Remove or update current “capability” terminology outside historical/deprecated docs.

  2. Deepen `FsFlow.Services.Core`.
     - Review current services: clock, log, random, GUID, environment variables.
     - Add missing obvious .NET-oriented operations where they belong.
     - Ensure each operation is exposed through `Flow` helpers over `Service<'T>.get()`.
     - Keep contracts coherent and testable.
     - Add live implementation coverage and deterministic/fake-friendly coverage where appropriate.
     - Do not overbuild telemetry here unless it belongs to a later telemetry service/package decision.

  3. Deepen `FsFlow.Services.FileSystem`.
     - Make FileSystem a near-complete .NET filesystem service, not a token example.
     - Proxy almost the entire expected `System.IO.File`, `Directory`, `Path`, and stream/text surface where it makes sense from a Flow perspective.
     - Omit only APIs that are obsolete, legacy-only, unsafe to abstract cleanly, redundant with a better wrapped operation, or a poor fit for typed Flow error handling.
     - Preserve typed errors rather than throwing where appropriate.
     - Include sync/async choices intentionally:
       - prefer async for I/O that has first-class async .NET APIs
       - keep pure/path operations simple
       - avoid pretending truly obsolete or redundant `System.IO` methods need wrapping if they do not fit FsFlow
     - Ensure live implementation is practical.
     - Ensure fake/test implementation is possible without ambient globals.

  4. Validate `IServiceProvider` boundaries.
     - Confirm `Service<'T>.resolve()` remains edge-only.
     - Confirm provider-backed layers convert dynamic registrations into explicit services/environments.
     - Missing provider registrations should be startup/configuration typed errors when using layers, and defects only when using direct resolve
     at the edge.
     - Add tests for:
       - successful provider-backed construction
       - missing provider registration
       - mapping provider registrations into explicit record env
       - replacing live services with fake services in tests

  5. Align light service packages.
     - For Console, Http, and Process:
       - ensure naming says “service”, not “capability”
       - ensure exposed helpers use `Service<'T>.get()`
       - ensure live implementations and layers follow the same pattern
       - avoid expanding their API surfaces in this goal except for small consistency fixes

  6. Docs and backlog.
     - Update dev docs with the service-package depth strategy:
       - Core and FileSystem are first deep services
       - Console, Http, Process remain light but aligned
       - near-complete Console/Http/Process/Network/Telemetry expansion becomes future work
     - Update root TODO accordingly.
     - Keep user-facing docs changes targeted; do not rewrite broad docs until APIs stabilize.
     - Update generated reference docs if public APIs change.
     - Update `llms.txt` terminology if needed.

  Non-goals:
  - Do not deeply expand Console, Http, or Process yet.
  - Do not design Network yet.
  - Do not decide Telemetry service package shape yet.
  - Do not add automatic layer env merging, tagged services, or compatibility aliases.
  - Do not introduce a registry or ambient service runtime.

  Acceptance:
  - Core and FileSystem have meaningfully deeper, coherent service APIs; FileSystem should cover most operations a .NET developer expects unless deliberately omitted.
  - IServiceProvider edge behavior is validated by tests.
  - Console, Http, and Process remain light but terminology/API shape is aligned.
  - Current docs/code avoid “capability” terminology outside historical/deprecated material.
  - API shape/reference docs reflect any new public APIs.
  - `dotnet test` passes.
  - `dotnet build FsFlow.slnx` passes.
  - `bash scripts/generate-api-docs.sh` passes without unresolved-symbol warnings.
  - `npm run build` in `site` passes.
  - `timeout 45s bash scripts/preview-docs.sh` reaches Hugo startup.
  - Commit the completed work.
