# Process output streaming surface

Status: revised implementation spec. The decisions below resolve the routing and streaming questions.
Implement this surface before folding durable rules into `dev-docs/PLAN.md` and deleting this file.
This spec describes proposed APIs, not APIs already available in the working tree.

## Purpose and decisions

Make the common case a pipeline whose consumer receives stdout lines. Keep structured output and
completion results available without mixing them in one stream element type.

- Streams contain `ProcessOutput` or projected line strings; never a completion event.
- Completion is a `Flow` result. Delete `ProcessEvent`, its `Completed` case, and `Process.stream`.
- Functions taking commands or specifications live in `Process`; functions taking `FlowStream` live
  in `FlowStream`. Functions taking the new completion-aware process stream live in `ProcessStream`.
- Routing teaches channel-prefixed shortcuts first — `stdoutToConsole`, `stdoutToFile`,
  `stderrToFile`, `stdinFromText`, and the like — with `stdoutTo`/`stderrTo`/`stdinFrom` as the
  general forms for tees, callbacks, streams, and custom targets. Stream constructors use
  `streamOutputs`, `streamStdout`, `streamStderr`, `streamStdoutLines`, and `streamStderrLines`.
- Effectful terminals use the `Flow` suffix, consistent with `FlowStream.runForEachFlow`.
- Routing, observation, and capture are independent. `toFlow` captures full stdout by default;
  streaming defaults to a bounded stdout tail. Stderr is separate diagnostic data unless merged
  explicitly. Explicit capture settings override execution-mode defaults.
- A command and a connected topology have different public types. Configure a command before
  connecting it. Shared fallback configuration is a typed `StageDefaults` value.
- Final-stage `mergeStderr` changes the effective channel to stdout, while retaining the original
  channel separately. Intermediate stderr is not merged by this operation.
- Outcome runners return values and causes; integer host runners have explicit runExitCode names.
  Implement the complete revised surface in this work, rather than deferring capabilities to a
  later release. Incremental implementation commits do not change that scope.

## Commands, topologies, and configuration

```fsharp
// Immutable, inspectable through plan/render; constructors remain private.
type ProcessCommand
type ProcessSpec

module Process =
    val command : FormattableString -> ProcessCommand
    val commandText : string -> ProcessCommand
    val commandArgs : string -> string list -> ProcessCommand
    val ofCommand : ProcessCommand -> ProcessSpec
    val pipe : next: ProcessSpec -> source: ProcessSpec -> ProcessSpec
    val pipeStdoutAndStderr : next: ProcessSpec -> source: ProcessSpec -> ProcessSpec
    val mergeStdout : ProcessSpec seq -> ProcessSpec

    val stdinFrom : InputSource -> ProcessCommand -> ProcessCommand
    val stdoutTo : OutputTarget -> ProcessCommand -> ProcessCommand
    val stderrTo : OutputTarget -> ProcessCommand -> ProcessCommand
    val workingDirectory : string -> ProcessCommand -> ProcessCommand
    val environment : string -> string -> ProcessCommand -> ProcessCommand
    val removeEnvironment : string -> ProcessCommand -> ProcessCommand
    val encoding : Encoding -> ProcessCommand -> ProcessCommand
    val successCodes : seq<int> -> ProcessCommand -> ProcessCommand
    val timeout : TimeSpan -> ProcessCommand -> ProcessCommand
    val withoutTimeout : ProcessCommand -> ProcessCommand
```

`arg` and `secretArg` also take `ProcessCommand`. Connected specs have no direct command-setting
overloads: `pipeline |> stdoutTo target` is a compile-time error. Configure commands before connecting,
or transform the final command explicitly:

```fsharp
pipeline |> Process.mapFinalCommand Process.stdoutToConsole
pipeline |> Process.mapFinalCommand (Process.stdoutToFile "build.log")
```

`mapFinalCommand : (ProcessCommand -> ProcessCommand) -> ProcessSpec -> ProcessSpec` requires exactly
one final command. Reject ambiguous topologies with invalidArg and validate connection conflicts
again after transformation. The transform changes settings, not command identity or graph shape.

Execution, streaming, inspection, capture helpers, and applicable topology-setting boundaries accept
either ProcessCommand or ProcessSpec through static adaptation, in both qualified and DSL APIs.
A single command therefore needs no explicit ofCommand:

```fsharp
Process.command $"build-tool" |> Process.streamStdoutLines
```

`ofCommand` remains the explicit conversion for callers who need a ProcessSpec value. No API converts
a connected topology back into one command. `pipe`/`pipeStdoutAndStderr` accept either input shape as well;
mergeStdout accepts a homogeneous sequence of either shape. All adaptation is static, reflection-free,
AOT-safe, and available under Fable. Signatures below use ProcessSpec to show the common underlying
shape; public inline facades also accept ProcessCommand without requiring extra aliases.

`pipe`, `pipeStdoutAndStderr`, `mergeStdout`, `mergeStderr`, `framing`, `maxLineLength`, `stageDefaults`,
`overallTimeout`, `drainTimeout`, `plan`, and `render` operate on topologies. Existing fan-in validation
still applies. `plan` and `render` inspect either shape without execution or dynamic lookup.

### Command parsing and topology names

command/cmd parse a command template without invoking a shell; each interpolation hole is one native
argument. commandText/cmdText parse a fixed literal executable-plus-arguments string, also without
shell expansion. commandArgs accepts an executable and an already-separated argument list. Shell
syntax requires bash/sh/pwsh explicitly. Their interpolated forms pass values out of band as shell
arguments rather than inserting raw source; *Text forms accept raw shell programs. secret/secretArg
redact arguments in plans and diagnostics, not child output or captured secrets.

pipe/pipeTo connect stdout. pipeStdoutAndStderr/pipeStdoutAndStderrTo name both connected channels
explicitly; remove pipeBoth/pipeBothTo instead of keeping renamed aliases. mergeStdout replaces merge:
it constructs concurrent stdout producers for fan-in, never merges stderr into stdout and never
executes during construction. All producers must feed one downstream command before execution in
this revision; reject an unconnected multi-final topology before launching anything. Producers default
to line-framed fan-in to avoid mixing partial lines; explicit chunk framing permits nondeterministic
chunk interleaving. The DSL mergeBytes helper becomes mergeStdoutBytes. Any unsuccessful producer
fails the run under its configured successCodes, terminating and awaiting the remaining topology.

Repeated command settings replace prior scalar settings. arg appends; successCodes replaces the
accepted set, which defaults to { 0 } and must be nonempty. Repeated routes replace prior routes;
Output.tee explicitly requests fan-out. A default timeout is disabled by withoutTimeout, not merely
removed from the command's local settings. Shared defaults resolve per field/key as described below.

Every command records explicit settings separately from resolved values. An explicit setting equal
to the built-in default still overrides a shared default. Unset stdin resolves to empty for a root.
Unset external stdout/stderr routes resolve to discard in batch and streaming modes and Console in
direct runExitCode host mode. A pipe connection supplies the connected channel instead. Resolve
execution-mode defaults only after checking explicit settings and topology defaults. Observation
and capture still read discarded channels.

Reject a connection if it consumes a channel with an explicit conflicting route:

- Piped stdout cannot also have an explicit stdout target, including explicit discard.
- `pipeStdoutAndStderr` cannot also consume explicitly targeted stderr.
- A pipe-fed command cannot have an explicit stdin source.
- Final mergeStderr conflicts with an explicit final stderr destination: configure stdout instead.
- Implicit built-in routes do not conflict. Validate all fan-in edges as well as ordinary pipes.

Reject these invalid descriptions during topology construction with `invalidArg` identifying the
command and channel. Do not silently overwrite settings or interpret the conflict as a tee.

### Stage defaults

```fsharp
type StageDefaults // opaque immutable fallback settings

module StageDefaults =
    val empty : StageDefaults
    val workingDirectory : string -> StageDefaults -> StageDefaults
    val environment : string -> string -> StageDefaults -> StageDefaults
    val removeEnvironment : string -> StageDefaults -> StageDefaults
    val encoding : Encoding -> StageDefaults -> StageDefaults
    val successCodes : seq<int> -> StageDefaults -> StageDefaults
    val timeout : TimeSpan -> StageDefaults -> StageDefaults
    val stdoutCapture : OutputRetention -> StageDefaults -> StageDefaults
    val stderrCapture : OutputRetention -> StageDefaults -> StageDefaults

module Process =
    val stageDefaults : StageDefaults -> ProcessSpec -> ProcessSpec
```

`stageDefaults` supplies a fallback policy for the topology, including commands connected later.
Explicit inline command settings always win, regardless of application order. Environment resolution
is per key: explicit removal is a tombstone. `withoutTimeout` overrides a default stage timeout.

Resolve each field/key in this order: explicit command setting, the topology-wide `stageDefaults`
policy, built-in default. Connecting two specs combines their defaults per field and per environment
key, with the destination's defaults winning conflicts; a later `stageDefaults` application updates
the combined policy. Explicit command settings always win. Environment-removal tombstones still
override a default that supplies the variable. Store defaults as policy metadata rather than copying
values into explicit command fields. `plan` shows resolved values and provenance. There is no
everyStage transformation API.

```fsharp
let shared =
    StageDefaults.empty
    |> StageDefaults.environment "CI" "true"
    |> StageDefaults.workingDirectory repo
    |> StageDefaults.timeout (TimeSpan.FromSeconds 30.)

let specification =
    (cmd $"a" |> stderrTo (Output.file "a.err") |> cwd "/other")
    => (cmd $"b" |> stderrTo (Output.file "b.err"))
    |> stageDefaults shared
// a uses /other; b uses repo. Both receive CI and the default stage timeout.
```

## Routing, capture, and observation

Routing chooses where exact bytes go; capture chooses what ProcessResult remembers; observation
publishes decoded text. Console/file routes do not disable capture. Axial reads redirected output
once and can forward, capture, and observe the same bytes. Tee is for multiple external destinations.
Inherited handles bypass Axial and cannot be captured or observed.

### Discoverable routing helpers

Teach the channel-prefixed shortcuts as the canonical forms for common destinations; typing the
channel prefix in autocomplete surfaces them. `stdoutTo`/`stderrTo`/`stdinFrom` remain the general
forms for tees, callbacks, streams, and custom targets:

```fsharp
module Process =
    val stdoutToConsole : ProcessCommand -> ProcessCommand
    val stdoutToFile : string -> ProcessCommand -> ProcessCommand
    val stdoutToAppendFile : string -> ProcessCommand -> ProcessCommand
    val stdoutToDiscard : ProcessCommand -> ProcessCommand
    val stdoutToInheritHandles : ProcessCommand -> ProcessCommand
    val stderrToConsole : ProcessCommand -> ProcessCommand
    val stderrToFile : string -> ProcessCommand -> ProcessCommand
    val stderrToAppendFile : string -> ProcessCommand -> ProcessCommand
    val stderrToDiscard : ProcessCommand -> ProcessCommand
    val stderrToInheritHandles : ProcessCommand -> ProcessCommand
    val stdinFromText : string -> ProcessCommand -> ProcessCommand
    val stdinFromBytes : byte array -> ProcessCommand -> ProcessCommand
    val stdinFromFile : string -> ProcessCommand -> ProcessCommand
    val stdinFromInheritHandles : ProcessCommand -> ProcessCommand
```

Every shortcut behaves identically to its general form, with one implementation. Callbacks, streams,
and tees use general forms. No routing helper executes. Do not keep console/writeTo/appendTo
near-synonyms; migrate to the channel-prefixed names. Output/Input remain the target/source factories.

### Capture defaults and explicit settings

Teach this sentence before the helper list: **captureAll/captureTail/captureNothing configure stdout;
stderr is separate unless mergeStderr is requested.** Both cold reviewers initially interpreted
captureAll as both channels. Keep the agreed short stdout-first family, but put its scope in the
first sentence of every helper's XML comment and completion description. Show captureAll followed
by captureStderrAll whenever the example asks for a complete two-channel transcript. Do not silently
broaden captureAll to both channels or couple capture to routing.

```fsharp
[<RequireQualifiedAccess>]
type OutputRetention = Nothing | Tail of maxBytes: int | All

module Process =
    // These helpers accept either command or topology through static adaptation.
    val captureAll : ProcessSpec -> ProcessSpec
    val captureTail : maxBytes: int -> ProcessSpec -> ProcessSpec
    val captureNothing : ProcessSpec -> ProcessSpec
    val captureStderrAll : ProcessSpec -> ProcessSpec
    val captureStderrTail : maxBytes: int -> ProcessSpec -> ProcessSpec
    val captureStderrNothing : ProcessSpec -> ProcessSpec
```

Unqualified capture means effective stdout, never implicit stderr. captureAll retains full final
stdout, including final stderr explicitly merged into it. captureTail keeps the last specified bytes;
captureNothing keeps none. Explicit stderr helpers configure the readable unmerged stderr channels.
On a command the setting travels with that command; on a topology stdout helpers target its unique
final command and stderr helpers set every currently readable unmerged stderr channel. Ambiguous
final stdout is invalid. Settings are explicit, last explicit setting wins, and defaults cannot
override them. Capture Nothing does not stop draining, observation, or external routing.

Remove Capture/CaptureTail from OutputTarget and their Output helpers; capture is no longer an
external destination. Remove the bare capture helper. StageDefaults stores optional stdoutCapture
and stderrCapture policies for shared fallback configuration; the public helpers above are canonical.

| Execution | Implicit stdout capture | Implicit unmerged stderr capture |
| --- | --- | --- |
| toFlow and direct command/spec run/runWith | All final stdout | 64 KiB diagnostic tail per readable stage |
| Direct command/spec runExitCode/runExitCodeWith | Nothing; unset route forwards to console | 64 KiB diagnostic tail per readable stage; unset route forwards to console |
| Process.stream* and every ProcessStream terminal/conversion | 64 KiB final stdout tail | 64 KiB diagnostic tail per readable stage |
| Explicit command/topology/default policy | Selected policy | Selected policy |

Choose execution-mode defaults only when no explicit or stage-default policy exists. Stream terminal
choice never changes the policy selected at construction. Stdout streaming is the default taught
path; streamOutputs and streamStderr are explicit requests for other channels.

```fsharp
cmd $"git status" |> toFlow                 // full stdout, separate stderr diagnostics
cmd $"build-tool" |> mergeStderr |> toFlow // full combined final stdout
cmd $"build-tool" |> stdoutToConsole |> toFlow // console forwarding plus full stdout capture
cmd $"build-tool" |> captureAll |> streamStdoutLines // live lines plus full stdout transcript
cmd $"build-tool" |> captureNothing |> streamStdoutLines // live lines, no stdout transcript
```

Full capture is ordinary batch behavior and can use unbounded memory. captureAll is usually only
needed to override streaming's bounded default. captureTail opts a large batch run into bounded
capture. captureNothing is useful when routing/consumption and exit status suffice; it does not
remove the separate stderr diagnostic tail. Use captureStderrNothing to opt out of that too.

Connected intermediate stdout and stderr consumed by pipeStdoutAndStderr cannot independently be observed or
captured. Capturing all stdout does not add intermediate tee nodes. Negative tail sizes are invalid;
zero captures no bytes. Diagnostic stderr tails share captured storage instead of duplicating it.

ProcessResult.ExitCode is the final command's exit code; ExitCodes gives every stage's code in plan
order. The default success policy is pipefail: any stage outside successCodes fails the Flow, even
if the final stage exits zero. On failure StageFailed carries the failing stage and available result.
The singular code is never an invented aggregate code. Retain this typed-failure policy rather than
turning unsuccessful exits into implicit successful values.

ProcessResult retains exit codes, timings, and stages. StdOut and StdOutCapture describe final
effective stdout. StdErr and StdErrCapture are explicitly separate diagnostic/captured stderr,
never concatenated into StdOut without mergeStderr. Stage results expose per-channel capture and
truncation. Aggregate stderr is a separate 64 KiB tail by default; when every contributing channel
has explicit All capture it is complete, and mixed policies produce a partial aggregate marked
truncated. Keep aggregate buffering bounded when stage policies are bounded. Decode per stage before
combining text; do not decode mixed encodings with the final command encoding.

StdOutCapture/StdErrCapture are CapturedOutput records containing Text, exact Bytes, and Truncated,
not capture-mode settings. StdOut/StdErr are convenience views of those records, not duplicate
buffers. Capture policies are visible in plan. Inherited capture is explicitly unavailable in result
metadata; an empty string alone must not imply that an inherited channel produced no output. Add
StdOutCaptureAvailable/StdErrCaptureAvailable derived properties, with aggregate stderr unavailable
if any contributing stderr channel was inherited. Nothing remains an available, deliberately
empty capture policy, with truncation indicating omitted output.

Truncation must be prominent in result documentation and inspection: pair the convenience text fields
with StdOutTruncated/StdErrTruncated properties and existing capture metadata. Nothing with nonempty
output is truncated. A byte tail may start midway through a Unicode code point; use replacement
fallback for its text while preserving exact retained Bytes. Plan exposes resolved policies and limits.

Inherited channels resolve implicit capture to Nothing. Explicit non-Nothing capture on an inherited
channel is invalid. Explicit streamStdout*/streamStderr* of an inherited requested channel fails with
ObservationUnavailable before launching any stage; streamOutputs includes readable channels only.
Never silently redirect inherited handles. Tee cannot contain Inherit.

### Input/output adapter contracts

Input.readOnce (InputSource.ReadOnce) invokes a callback once at execution to obtain one byte block,
then closes stdin; it is not a pull-until-EOF reader. Input.produce invokes a push producer with an
awaited write callback, and closes stdin when the producer returns. Input.stream adapts a caller-owned
.NET Stream into repeated backpressured reads. Empty means close stdin without bytes; inheritHandles
leaves input under child control. Text uses the resolved command encoding; bytes remain exact.

Output.callback receives exact byte chunks, not ProcessOutput or framed lines. Await writes in order
per source channel, with the execution token available through Async.CancellationToken. Output.stream
and textWriter do not dispose caller-owned resources; finish/flush on successful completion and
observe cancellation during shutdown. Create fresh incremental decoder state per execution for
textWriter adapters, so reuse of the command does not share a mutable decoder between runs. Files
opened by Axial are owned/disposed by Axial. Routing callback effects are explicit in their supplied
callbacks; live adapters still obey the service effect boundary.

### Framing and memory bounds

All decoded streams use incremental decoders. Chunk boundaries are arbitrary and are not lines.
Line constructors apply line framing before decoded output is delivered to
the consumer, overriding a previously selected chunk framing for that execution only.

Remove exactly one LF or CRLF terminator, preserve all other whitespace, emit empty lines, and emit
the final unterminated line once at EOF. A trailing terminator does not create an extra empty line.
Bare CR is data. Limits exclude the terminator.

`Process.maxLineLength : int -> ProcessSpec -> ProcessSpec` defaults to 1,048,576 UTF-16 code units
per line/channel. Require a positive limit. Exceeding it fails with
`ProcessError.LineTooLong` carrying stage, source channel, and limit; terminate and await the topology.
There is no unlimited line setting in this revision. Process chunks remain fixed-size; line assembly
must check the limit incrementally and avoid repeatedly copying the entire pending line.

With streaming defaults, memory is bounded by stage count, capture limits, line limits, fixed pipe
buffers, and bounded handoff. Batch toFlow defaults to unlimited stdout capture. User callbacks can
allocate freely; explicit All and runCollect intentionally remove the corresponding streaming bound.
State these exceptions alongside the backpressure guarantee. Destination callbacks remain awaited and backpressured.

## Completion-aware process streams

```fsharp
// Opaque cold stream plus the lifetime/completion of its process run.
type ProcessStream<'env, 'value>

[<RequireQualifiedAccess>]
type ProcessCompletion = Completed of ProcessResult | StoppedEarly

[<RequireQualifiedAccess>]
type ProcessStreamError<'error> =
    | ProcessFailed of ProcessError
    | ConsumerFailed of 'error

module Process =
    val streamOutputs : ProcessSpec -> ProcessStream<'env, ProcessOutput> when 'env :> IHasProcess and 'env :> IHasClock
    val streamStdout : ProcessSpec -> ProcessStream<'env, ProcessOutput> when 'env :> IHasProcess and 'env :> IHasClock
    val streamStderr : ProcessSpec -> ProcessStream<'env, ProcessOutput> when 'env :> IHasProcess and 'env :> IHasClock
    val streamStdoutLines : ProcessSpec -> ProcessStream<'env, string> when 'env :> IHasProcess and 'env :> IHasClock
    val streamStderrLines : ProcessSpec -> ProcessStream<'env, string> when 'env :> IHasProcess and 'env :> IHasClock
    val toFlow : ProcessSpec -> Flow<'env, ProcessError, ProcessResult> when 'env :> IHasProcess

module ProcessStream =
    val map : ('a -> 'b) -> ProcessStream<'env, 'a> -> ProcessStream<'env, 'b>
    val filter : ('a -> bool) -> ProcessStream<'env, 'a> -> ProcessStream<'env, 'a>
    val take : int -> ProcessStream<'env, 'a> -> ProcessStream<'env, 'a>
    val takeWhile : ('a -> bool) -> ProcessStream<'env, 'a> -> ProcessStream<'env, 'a>
    val runFold : ('state -> 'a -> 'state) -> 'state -> ProcessStream<'env, 'a> -> Flow<'env, ProcessError, 'state * ProcessCompletion> when 'env :> IHasClock
    val runCollect : ProcessStream<'env, 'a> -> Flow<'env, ProcessError, 'a list * ProcessCompletion> when 'env :> IHasClock
    val runDrain : ProcessStream<'env, 'a> -> Flow<'env, ProcessError, ProcessCompletion> when 'env :> IHasClock
    val runForEach : ('a -> unit) -> ProcessStream<'env, 'a> -> Flow<'env, ProcessError, ProcessCompletion> when 'env :> IHasClock
    val runFoldFlow : ('state -> 'a -> Flow<'env, 'error, 'state>) -> 'state -> ProcessStream<'env, 'a> -> Flow<'env, ProcessStreamError<'error>, 'state * ProcessCompletion> when 'env :> IHasClock
    val runForEachFlow : ('a -> Flow<'env, 'error, unit>) -> ProcessStream<'env, 'a> -> Flow<'env, ProcessStreamError<'error>, ProcessCompletion> when 'env :> IHasClock
    val toFlowStream : ProcessStream<'env, 'a> -> FlowStream<'env, ProcessError, 'a>
```

There are no separate Process.runFoldFlow/runStdoutLinesFlow terminals. Constructors take commands
or specs in Process; transformations and terminals take ProcessStream in ProcessStream. DSL mirrors
constructor names; use qualified ProcessStream terminals rather than ambiguous bare terminal names.
Implement this small surface over existing FlowStream machinery with a private scoped completion
protocol, not a second streaming runtime. Pure callbacks follow FlowStream conventions; effectful
callbacks use the Flow suffix and preserve application errors via ProcessStreamError. Map only typed
failures into that union; defects and interruption retain their original Cause structure.

`map` and `filter` live on `ProcessStream` deliberately: they thread the completion protocol, so a
transformed stream still returns its `ProcessResult` (or `StoppedEarly`) at the terminal. Round-tripping
through `toFlowStream` would discard completion, so it cannot replace them. They delegate to existing
`FlowStream` machinery with a minimal wrapper; `take`/`takeWhile` additionally record the owned
early-stop decision.

`runForEach` takes an ordinary synchronous callback; the signature does not enforce purity. Teach
`runForEachFlow` for service operations such as `Console.writeLine`, so dependencies, failures, and
cancellation remain represented in the workflow. Do not describe `runForEach` as inherently
effect-safe.

streamStdoutLines is the taught default. streamOutputs explicitly includes readable unconnected
stderr and final stdout; streamStderr selects effective stderr. Accepted successCodes determine
successful exits, including explicitly accepted nonzero values. Process failures remain typed errors,
not completion values or elements. ProcessCompletion is returned only by successful terminals.

```fsharp
type ProcessOutput =
    { Stage: int
      ObservedChannel: OutputChannel
      OriginalChannel: OutputChannel
      Text: string
      Timestamp: DateTimeOffset }
```

ObservedChannel is effective; OriginalChannel is the originating stdout/stderr channel. Stage is a
zero-based index into the executed plan, not a stable identity across topology edits. Replace the
previous Channel field with ObservedChannel. Final mergeStderr changes stderr to effective stdout,
so stdout streams include it and stderr streams exclude it. Preserve original provenance. Serialize
observation and preserve order
within each source channel. Cross-channel/stage order is observed delivery order, not OS write order.

### One pipeline for transformations and completion

```fsharp
let warnings =
    cmd $"build-tool"
    |> streamStdoutLines
    |> ProcessStream.filter (fun line -> line.Contains "warning")
    |> ProcessStream.runCollect
// Flow<..., ProcessError, string list * ProcessCompletion>
// Completed result after normal success; no second process run to obtain completion.

let firstAlerts =
    cmd $"tail -f /var/log/system.log"
    => cmd $"grep --line-buffered ERROR"
    |> streamStdoutLines
    |> ProcessStream.filter (fun line -> line.Contains "disk")
    |> ProcessStream.take 10
    |> ProcessStream.runCollect
// Ten matching lines plus StoppedEarly when consuming that prefix stops the producer.
```

Completed requires observing natural end through the process completion protocol; otherwise an
intentional prefix stop reports StoppedEarly. Do not guess completion from whether an OS process
has already exited: unread output or in-flight callbacks may remain. take 0 does not launch and
reports StoppedEarly. StoppedEarly is the owned stopping decision, not caller cancellation converted
to a value. Caller cancellation still yields Cause.Interrupt. Successful early stopping waits cleanup.

runCollect explicitly accumulates every selected element, independently of transcript capture.
runForEachFlow returns completion after all callbacks; application errors need no mapping into
ProcessError. Default stdout observation still drains/routes/captures stderr diagnostics separately.

Use explicit conversion when the wider FlowStream API is needed and completion is irrelevant:

```fsharp
cmd $"build-tool"
|> streamStdoutLines
|> ProcessStream.toFlowStream
|> FlowStream.runForEachFlow Console.writeLine
```

The conversion preserves bounded capture, backpressure, failures, and cleanup but discards the
successful completion value. After conversion, application callback deadlines belong to the enclosing
Flow; use Flow.timeout to bound them. ProcessStream terminals include callbacks in the process overall
timeout. Each execution starts one fresh run. Running a stream and then toFlow
starts two processes, never retrieves the first result. Infinite streams require owned early stopping
or caller cancellation; cancellation cannot produce Completed.

## Service and lifetime protocol

```fsharp
type IProcess =
    abstract Execute :
        specification: ProcessSpec * observe: (ProcessOutput -> Async<unit>) ->
        Flow<unit, ProcessError, ProcessResult>
```

One mockable Execute member. It awaits observers serially using the execution cancellation token.
toFlow passes `(fun _ -> async.Return ())`, not ignore (which has the wrong return type). Observer
exceptions are defects unless cancellation was requested by the execution token, classified with
ForeignCancellation.isOurs. Do not convert arbitrary callback exceptions into IoFailed; classify
actual process I/O separately. A private optional observer optimization is acceptable if measurements
justify it; do not restore the union.
Additional operational effects remain explicit dependencies of `Process.live`, including the clock,
filesystem, and console. Ambient cancellation is runtime mechanics, not an additional service.

Streams fork Execute and publish into a bounded rendezvous. Success and failure are private protocol
messages, never public stream elements. ProcessStream terminals use the same lifecycle with a consumer
whose callbacks run in the caller's application environment.

- Construction is cold; first pull or terminal execution starts the topology. Re-execution starts
  a fresh run, with fresh decoder and retention state.
- Successful completion requires process exits, output drain, and successful completion of every
  published callback. Never return ProcessResult while the final callback is still running.
- A consumer failure, including a defect, interrupts and awaits the producer, terminates the tree,
  and preserves the consumer's cause. Cleanup failures compose through the usual runtime rules.
- A producer failure stops further production and terminates the tree. Consume already-published
  output before reporting that failure unless the consumer fails or cancellation is requested.
  If that consumer fails while draining, preserve its cause too using the runtime's cause composition.
- Normal early stopping (`take`, `takeWhile`) interrupts and awaits only the owned producer during
  cleanup. The consumer succeeds with its prefix and StoppedEarly; no ProcessResult is manufactured.
  Conversion to FlowStream discards that completion value.
- Cancellation of the caller's runtime token yields Cause.Interrupt. Foreign cancellation follows
  ForeignCancellation.isOurs and does not become requested interruption.
- Start, I/O, framing, and stage failures stop siblings and await cleanup. StageFailed includes the
  failed stage and full result under the selected retention policies, not necessarily full output.
- Stage timeout runs from successful start to direct process exit. Downstream backpressure can
  consume that budget. StageTimedOut names the stage and configured duration.
- OverallTimedOut covers launch, execution, drain, and callbacks. Its budget begins before launch.
  The overall timer must wrap the terminal consumer as well as Run; it interrupts both on expiry.
- drainTimeout bounds remaining I/O after all directly managed processes exit. Expiry is an I/O
  failure. Slow callbacks awaiting output contribute to that drain interval.
- `ProcessError.TimedOut` is removed; `StageTimedOut` and `OverallTimedOut` replace it. A
  `TimedOut { Specification; Timeout }` match becomes `StageTimedOut` (per command) or
  `OverallTimedOut` (whole topology) as appropriate; `ProcessError.exitCode` returns 124 for both.
- All timers use the injected clock through Axial scheduling. No ambient timers outside named live
  implementations. If timeout races another failure, preserve already-observed causes through the
  runtime's normal composition rather than hiding a consumer failure behind cleanup interruption.

### Host environment and tests

```fsharp
module ProcessHostEnvironment =
    val live : ProcessHostEnvironment
    // Builds live clock/filesystem/process services using the supplied console.
    val liveWithConsole : IConsole -> ProcessHostEnvironment
    val ofParts : IProcess -> IConsole -> IClock -> ProcessHostEnvironment
    val test : unit -> TestProcessHostEnvironment

type TestProcessHostEnvironment =
    member Environment : ProcessHostEnvironment
    member Process : TestProcess
    member Console : TestConsole
    member Clock : ManualClock
```

Streaming requires `IHasClock` as well as `IHasProcess`: Axial supervises the producer
with clock-aware fibers, and stream terminals race the consumer against the overall deadline.
`ProcessHostEnvironment` supplies both services. `ofParts` takes the scheduling clock explicitly;
use the same clock that was injected into the process implementation.

`liveWithConsole` wires one console into both process routing and workflow console operations. `live` uses the
same construction. `ofParts` trusts supplied services; it cannot verify an opaque IProcess's internal
console. `test` exposes configurable concrete doubles and their composed environment. This is a
local package design, not a new mandatory rule for every host package in AGENTS.md.

TestProcess records specs, exposes a convenient scripted output list/result, and supports an optional
full handler of shape `ProcessSpec -> (ProcessOutput -> Async<unit>) -> Flow<unit, ProcessError, ProcessResult>`.
The full handler replaces convenience scripting for that run and can block, fail after output,
observe cancellation, and coordinate races. Fresh test bundles are created per test. The fake must
await observers and follow the same completion ordering as the live service.

## DSL and shell comparisons

### Run returns the outcome; runExitCode is the script boundary

`Process.run` and DSL `run` mean execute and return the complete Exit. Never infer an integer return
from run, and never change its return shape when capture settings change:

- ProcessCommand or ProcessSpec -> Exit<ProcessResult, ProcessError>.
- Flow<ProcessHostEnvironment, 'error, 'value> -> Exit<'value, 'error>.

Commands/specs adapt through toFlow and use batch defaults: full final effective stdout and separate
bounded stderr diagnostic tails. Run does not print or replace routes. Explicit stdoutToConsole
forwards live output as configured while the captured result remains accessible. Existing Flows
execute directly, preserving their error/value types, routes, capture policy, and lifetime behavior.
Success, typed failure, defects, and caller interruption remain visible through Exit/Cause.

`Process.runWith environment` and DSL `runWith environment` return the same Exit shapes using only
the supplied environment. Command/spec inputs require IHasProcess. Existing Flow inputs require
exactly their own environment type, with no extra IHasConsole requirement: returning an Exit needs
no error-reporting console. Run supplies ProcessHostEnvironment.live for its supported live Flow
shape. Other application environments use runWith. Both use static adaptation without reflection.

```fsharp
let captured = cmd $"git status" |> run
match captured with
| Exit.Success result -> result.StdOut
| Exit.Failure cause -> failwithf "%A" cause

// An explicit capture request always has a readable result; no hidden discarded transcript.
let complete = cmd $"build-tool" |> captureAll |> captureStderrAll |> run

// Routes and capture are independent: show stdout live and also return the batch result.
let shownAndCaptured = cmd $"build-tool" |> stdoutToConsole |> run

let supplied = cmd $"git status" |> runWith appEnvironment
```

`Process.runExitCode` and DSL `runExitCode` are the command-line boundary and return int. Their
`runExitCodeWith environment` variants use supplied services. All accept command/spec or an existing
Flow whose typed error is ProcessError. For direct commands/specs, fill unset externally readable
stdout/stderr routes with Console and unset stdout capture with Nothing; retain the separate 64 KiB
stderr diagnostic tail. Explicit routes, capture settings, stage defaults, and pipe connections win.
This host mode does not adapt blindly through batch toFlow. Existing Flows are never reconfigured;
the runner reports their failure and maps their outcome to an exit code, discarding success values.

```fsharp
// In a program, mark this EntryPoint. Output is visible without full stdout buffering.
let main _ = cmd $"build-tool" |> runExitCode

// A file route stays a file route, and explicit capture still takes priority.
let writeLog () = cmd $"build-tool" |> stdoutToFile "build.log" |> runExitCode

// Application composition still explicitly constructs a lazy Flow.
let status =
    flow {
        let! result = cmd $"git status" |> toFlow
        return result.StdOut
    }

let result = status |> runWith appEnvironment
```

Command/spec runExitCodeWith inputs require IHasProcess and IHasConsole; existing ProcessError Flows
require IHasConsole for reporting in addition to their own environment requirements. ProcessError
exit mapping remains: accepted completion 0, failed stage's code, timeout 124, other failure 1.
Never duplicate forwarded stderr tails in error reporting; print a redacted summary without replaying
already-forwarded diagnostics. Explicit Nothing capture does not disable failure status reporting.
Explicit All capture with runExitCode is honored even though the host discards that transcript;
users wanting to read it should use run instead.

ProcessStream is not an implicit runner input: choose a terminal first. Generic consumer errors are
supported by outcome runners, without lossy mapping into ProcessError:

```fsharp
let liveWarnings =
    cmd $"build-tool"
    |> streamStdoutLines
    |> ProcessStream.runForEachFlow Console.writeLine

let outcome = liveWarnings |> runWith appEnvironment
// Exit<ProcessCompletion, ProcessStreamError<the console operation's error type>>.
// The host explicitly handles ProcessFailed/ConsumerFailed if it needs a program exit code.
```

Running a Flow built from a process stream preserves streaming defaults, including with runExitCode
where the Flow has a compatible error type. Routing/capture helpers remain lazy configuration;
toFlow is lazy conversion. Run and runExitCode are the immediate host execution boundaries.

`cmd`/`cmdText` return ProcessCommand. Command setters retain their qualified names, with existing
`cwd`/`env` shorthand. `=>`, `pipeTo`, and `pipeStdoutAndStderrTo` adapt either command or spec operands into
specs. `toFlow` and stream constructors adapt commands or specs. ProcessStream terminals take process
streams. Mirror constructor/configuration names exactly, including capture helpers, routing shortcuts,
stageDefaults, and overallTimeout; qualify stream terminals with ProcessStream. Implement adaptation
with the existing static connector pattern; no reflection or dynamic service lookup.

Connecting to an OutputTarget must no longer execute implicitly. A command-to-target connection
returns a configured command. A connected topology-to-target connection is unsupported: configure
its final command explicitly. Remove writeTo/appendTo in favor of stdoutToFile/stdoutToAppendFile,
with an explicit execution terminal required. Assembled pipelines can use mapFinalCommand. Update their
documentation and all copied examples together.

The table compares useful patterns, not exact POSIX shell semantics. Shell status selection,
substitution whitespace, SIGPIPE, and shell control flow differ. All runnable rows produce a Flow;
handler and path values are supplied by the containing example.

| Shell pattern | Axial |
| --- | --- |
| a \| b \| c | `cmd $"a" => cmd $"b" => cmd $"c" \|> toFlow` |
| a 2>&1 \| b | `cmd $"a" \|> pipeStdoutAndStderrTo (cmd $"b") \|> toFlow` |
| a < f | `cmd $"a" \|> stdinFromFile "f" \|> toFlow` |
| feed text / here-doc | `cmd $"a" \|> stdinFromText text \|> toFlow` |
| a > f | `cmd $"a" \|> stdoutToFile "f" \|> toFlow` |
| a >> f | `cmd $"a" \|> stdoutToAppendFile "f" \|> toFlow` |
| a 2> f | `cmd $"a" \|> stderrToFile "f" \|> toFlow` |
| a > f 2>&1 | `cmd $"a" \|> stdoutToFile "f" \|> mergeStderr \|> toFlow` |
| a > /dev/null | `cmd $"a" \|> stdoutToDiscard \|> captureNothing \|> toFlow` |
| a \| tee f | `cmd $"a" \|> stdoutTo (Output.tee [ Output.file "f"; Output.console ]) \|> toFlow` |
| a \| tee f \| b | `cmd $"a" => cmd $"tee f" => cmd $"b" \|> toFlow` (requires OS tee) |
| a && b | `flow { let! _ = toFlow (cmd $"a"); return! toFlow (cmd $"b") }` |
| a; b (continue after expected exit failure) | `sequential` below: handle StageFailed, then execute b |
| timeout 5 a | `cmd $"a" \|> timeout (TimeSpan.FromSeconds 5.) \|> toFlow` |
| env X=y a | `cmd $"a" \|> env "X" "y" \|> toFlow` |
| cd d && a | `cmd $"a" \|> cwd d \|> toFlow` |
| capture stdout | `cmd $"a" \|> toFlow \|> Flow.map _.StdOut` |
| a \| wc -l, then inspect its exit status | `cmd $"a" => cmd $"wc -l" \|> toFlow \|> Flow.map (fun r -> r.StdOut, r.ExitCode)` |
| consume stdout lines | `cmd $"a" \|> streamStdoutLines \|> ProcessStream.runForEachFlow handleLine` |
| read one line then stop the producer | `cmd $"a" \|> streamStdoutLines \|> ProcessStream.take 1 \|> ProcessStream.runCollect` |
| feed generated bytes | `cmd $"a" \|> stdinFrom (Input.produce producer) \|> toFlow` |

For continue-after-exit-failure, use this shape:

```fsharp
let sequential =
    flow {
        do! toFlow (cmd $"a")
            |> Flow.map ignore
            |> Flow.orElseWith (function
                | ProcessError.StageFailed _ -> Flow.ok ()
                | error -> Flow.fail error)
        return! toFlow (cmd $"b")
    }
```

This deliberately handles only expected unsuccessful exit codes. Cancellation, defects, and other
process errors stop the sequence. A general shell `;` policy requires deciding which other failures
the application owns. Accepted successCodes can replace recovery when all relevant codes are known.

## Deferred capabilities

Per-stage stderr routing is included now. Mid-pipeline tee graph nodes are deferred; use OS tee where
available. Do not constrain a future tee implementation to Hub until subscriber, backpressure, and
lifetime semantics have been designed. Detach remains unsupported: Axial owns the topology lifetime.
Spawn-per-line composes streams with effectful consumers and bounded workflow concurrency; avoid
runCollect for unbounded input. Independent stdin writers require one coordinating Input.produce.

## Cold API inference review

The baseline is commit 56c3de0d. Two fresh subagents reviewed the remaining API from names/signatures
alone, without repository access or conversation history. Their predictions are qualitative design
feedback, not evidence of universal model behavior. A follow-up comparison against Flow, FlowStream,
and platform services confirmed run returning Exit, mapFinalCommand, Execute, liveWithConsole, and
the exact FlowStream terminal spellings align with Axial. Keep typed test bundle service names
Process/Console consistent with TestRuntime rather than renaming them to mocks. Both correctly inferred commands, topology,
line streams, route/capture independence, early-stop cleanup, and most settings; their runner defaults
differed from the earlier isolated runner review. Give each runner a distinct return contract rather
than relying on an assumed universal meaning of run.

| Family | Decision after review |
| --- | --- |
| Command builders and shell DSL | Keep names; explicitly distinguish parsed commands from shell source and argument redaction from output redaction. |
| Pipe/fan-in | Name both channels and stdout fan-in; reject unconnected fan-in before launch. |
| Command settings and defaults | Keep three-level precedence, per-key removals, scalar replacement, and explicit timeout disabling. |
| Routing shortcuts | Keep canonical autocomplete names; repeated assignments replace; tee is explicit. |
| Capture | Preserve agreed stdout-first short names despite reviewers' both-channel guess; make scope explicit in signatures' comments and examples. |
| Final command transformation | mapFinalCommand makes editing rather than lookup apparent. |
| Framing and limits | Keep line constructors and configured limits; specify units, terminators, and overflow failure. |
| Input/output callbacks and adapters | readOnce distinguishes one block from a producer; specify byte shape, ownership, reuse, backpressure, and exception classification. |
| ProcessStream transformations | Keep completion-preserving wrappers; conversion explicitly loses successful completion. |
| StoppedEarly | Keep the bare case and awaited cleanup; defer a partial-stage result type rather than inventing exit status for stopped processes. |
| Result fields and exit codes | Explain capture records, availability/truncation, final versus per-stage codes, and typed unsuccessful exits. |
| Channel provenance | ObservedChannel/OriginalChannel explicitly distinguish merged from originating channels; Stage is a plan index. |
| Service/environment/test helpers | Execute names the service effect; liveWithConsole exposes live composition; test bundle keeps typed doubles visible. |
| Runners | run/runWith return Exit with values and errors intact; runExitCode variants implement console host policy and integer exit mapping. |

Both reviewers expected batch capture to include stderr and stream capture to be absent. Keep the
user-agreed batch-full-stdout/stream-tail defaults and separate stderr tails; teach the mode table
prominently and require assertions for truncation. Do not hide those choices behind a generic
claim that all defaults are guessable. Retain existing runWith naming consistent with Axial's
explicit environment convention, rather than expanding only this package to runWithEnvironment.

## Migration and acceptance

- Introduce ProcessCommand and update command construction, DSL connectors, static adaptation, mapFinalCommand, plan/render, piping,
  fan-in, examples, service fakes, and both .NET/Fable interpreters.
- Move per-command routing and explicit settings onto commands. Add StageDefaults resolution.
- Rename finalCommand to mapFinalCommand; pipeBoth/pipeBothTo to
  pipeStdoutAndStderr/pipeStdoutAndStderrTo; merge/mergeBytes to mergeStdout/mergeStdoutBytes.
  Remove renamed spellings instead of retaining aliases. Keep commandText/cmdText, but document
  their no-shell parsing explicitly.
- Split capture from OutputTarget; add stdout-first captureAll/captureTail/captureNothing and
  explicit stderr counterparts, mode defaults, and routing shortcuts; migrate removed helpers.
- State the breaking change: `toFlow` now captures unmerged stderr as a 64 KiB diagnostic tail per
  readable stage (previously full capture). `captureStderrAll` restores full stderr transcripts.
- Delete ProcessEvent/Process.stream. Add Process.stream* constructors and the completion-aware
  ProcessStream type, combinators, terminals, conversion, and combined consumer errors above.
- Rename ProcessOutput.Channel to ObservedChannel and add OriginalChannel. Add framing limits,
  observation errors, capture-availability metadata, and StageTimedOut/OverallTimedOut,
  which replace and remove ProcessError.TimedOut.
- Replace IProcess.Run with IProcess.Execute and ProcessHostEnvironment.create with liveWithConsole.
  Rename Input.read/InputSource.Read to readOnce/ReadOnce. Add host helpers and configurable test
  bundles. Preserve explicit live service dependencies.
- Change run/runWith to return Exit and support command/spec and generic-error Flows. Add
  runExitCode/runExitCodeWith for integer script boundaries; migrate EntryPoints and existing
  integer-run callers to those names. Keep toFlow in application composition and generic consumer
  errors intact in outcome runners. Direct integer runners use console/no-stdout-capture defaults.
- Update docs/process/{01-composition,02-output-capture,03-streaming,04-failures-and-transcripts,
  05-scripts,06-fable,07-worked-examples,08-running-in-an-application-flow,_index}.md and
  docs/02-how-it-compares/02-task-vs-flow-scenarios.md, source comments, and reference enrichment.
- Update examples/Axial.Comparisons/OutputPipeline.fs and its README. Leave historical release notes.

Tests must cover bounded retention on long runs, aggregate bounds, no hidden duplicate capture,
Unicode tail decoding, truncation, empty/CRLF/final lines, oversized lines, merged channel provenance,
per-stage routing, connection conflicts, explicit defaults and removals, default propagation through
composition and later connection, mode-dependent capture defaults and explicit overrides,
inherited-channel observation, accepted nonzero exit codes, failures after output,
slow/failing final callbacks, both sibling cancellation directions, early successful take, caller
cancellation, foreign cancellation, stage/overall/drain timeout boundaries, and re-execution. Verify transformed streams retain completion, StoppedEarly/take 0,
consumer error wrapping, conversion cleanup, capture helper scope, and mapFinalCommand validation.
Verify run/runWith return full Exits, execute exactly once, preserve batch defaults/explicit
settings, support consumer error types, and require no reporting console for supplied outcome runs.
Verify runExitCode/runExitCodeWith console/capture defaults, explicit-route precedence, supplied
services, no duplicate diagnostics, streaming-Flow policies, and failure/exit-code mapping. Compile
all three input forms in both qualified and DSL APIs, including minimal environments.
Verify renamed helpers, pre-launch observation/fan-in validation, observer defect classification,
caller-owned adapter lifetime, fresh text decoders, and capture-availability result metadata.
Add compile-time API probes showing that topology-to-command setters are rejected. Add torture
scenarios for each new public runtime/concurrency operation required by the coverage rules.

Implementation acceptance: solution build and tests, Guardrails, Fable surface and runtime checks,
AOT probe, and `dotnet livedocs test --warn-as-error` at the phase boundary. Compile the examples and
runnable shell comparison rows in tests or verified documentation; prose policy rows need an
explicit worked example. No ProcessEvent references remain in src, tests, examples, or docs/process.
Planning edits to this file alone do not require those implementation checks.
