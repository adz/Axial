# Axial Plan

Axial is an asynchronous F# workflow library. Its public model is `Flow<'env, 'error, 'value>` with shorter aliases for common channel combinations. Dependencies are explicit in `'env`; typed expected failures are explicit in `'error`; executor mechanics remain in the closed runtime.

## Package direction

- `Axial` — workflows, exits, causes, fibers, structured concurrency, schedules, streams, STM, layers, and application lifetime
- `Axial.PlatformService` — explicit clock, logging, randomness, GUID, and environment services
- `Axial.Console`, `Axial.FileSystem`, `Axial.HttpClient`, `Axial.Process` — mockable operational services
- `Axial.Hosting`, `Axial.Hosting.Node`, `Axial.Hosting.Browser` — application lifecycle integrations
- `Axial.Telemetry`, `Axial.Telemetry.JavaScript` — tracing and runtime observability
- `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp` — optional adapters that execute Reified HTTP contracts

Core `Axial` has no Reified dependency. Host adapters are the only library projects allowed to reference Reified. The retained reference application is integration evidence, not part of the core product; it should move to a separate examples repository when both package families are published.

## Architecture

- Application and operational dependencies are explicit services in `'env`.
- Ambient runtime state is limited to cancellation, scopes, scheduling, interruption, annotations, and trace mechanics.
- Process and HTTP implementations inject clocks and other additional effects explicitly.
- `App` owns a root workflow lifetime; host packages translate native lifecycle events at the edge.
- The core remains reflection-free, trimming-safe, AOT-friendly, and Fable-compatible.
- Pre-1.0 APIs are replaced directly; do not preserve obsolete aliases.

## Documentation

Teach from the smallest useful `Flow` shape, then introduce environment and typed failure channels. The public site is Flow-only. Reified appears only where an integration example genuinely crosses the repository boundary.

Durable decisions remain in `dev-docs/decisions/README.md`; current work belongs in `dev-docs/TASKS.md`.

## Axial.Elmish

- One runtime per program owns one root scope for commands, subscriptions, and releases.
- All dispatch goes through the host's `Post`; defects go to `OnDefect`.
- Command keys provide per-runtime latest-wins cancellation.
- Subscriptions are streams; their `Dispose` requests interruption without blocking the UI thread.

## Axial.Process

- `ProcessCommand` owns command settings; `ProcessSpec` owns connections and topology policy. Public facades adapt
  either statically. Command settings must be applied before connection, or through `mapFinalCommand` for one final
  command. `mapFinalCommand` preserves argument redaction and rejects executable/argument replacement. Connection
  conflicts are rejected rather than silently replacing routes or stdin.
- `stageDefaults` resolves behind explicit command settings and ahead of built-ins, per field and environment key.
  Explicit environment removals are tombstones. Destination defaults win composition conflicts; later defaults
  update the combined topology policy and apply to future stages.
- Routing and retention are independent. Batch execution captures full stdout; streaming captures a 64 KiB stdout
  tail. Readable unmerged stderr uses separate 64 KiB diagnostic tails. Stdout helpers affect stdout only; stderr has
  named helpers. `mergeStderr` is explicit. Connected and inherited channels have unavailable capture metadata.
- `IProcess.Execute` is the one mockable effect, with awaited output observation and final completion. The live
  implementation injects clock, filesystem, and console services. Timers and durations use the injected clock;
  stage deadlines start at individual native launch and end at native exit, overall deadlines cover drain and
  completion-aware callbacks, and drain deadlines start when all directly managed processes exit.
- `ProcessStream` wraps FlowStream with completion tracking and uses Axial's existing supervised runtime. Its
  constructors require `IHasProcess` and `IHasClock`. Natural completion is `Completed result`; owned prefix stopping
  awaits cleanup and returns `StoppedEarly`; caller cancellation stays interruption. Effectful terminals preserve
  process and consumer errors separately, including simultaneous observed failures. `toFlowStream` deliberately
  discards completion; application callback deadlines then belong to the enclosing Flow.
- Line streams remove LF/CRLF delimiters, preserve blank and final unterminated lines, and enforce a default
  1,048,576 UTF-16-unit line limit with `LineTooLong`. Structured output distinguishes observed and original
  channels and uses plan stage indices. Stage and overall timeout failures are distinct; drain expiry is `IoFailed`.
- `run` and `runWith` return the value and raise `ProcessRunException` on failure; `tryRun` and `tryRunWith` return
  the complete `Exit`. All four accept a command, a spec, or a Flow, and keep batch defaults for direct command and
  spec inputs.
- Native stream and text-writer adapters remain caller-owned. Decoders and capture state are fresh per execution.
  Mid-pipeline tee nodes and detached process lifetimes remain out of scope; compose an OS tee command when needed.
