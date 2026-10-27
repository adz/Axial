---
title: Guardrail diagnostics
linkTitle: Guardrail diagnostics
description: The contract of each AXG diagnostic, what it detects, what to use instead, and how to allow an intentional boundary.
---

# Guardrail diagnostics

Each `AXG` diagnostic has a narrow contract: the effect or convention it detects, the service or form that replaces
it, and the suppression directive that allows an intentional boundary. The [adoption guide](guardrails.html) covers
install, severity, and suppression choices; this page is the per-diagnostic reference.

## AXG001: Use explicit effect services

`AXG001` detects direct calls to ambient .NET effects.

| Category | Detects | Use instead |
| --- | --- | --- |
| `random` | Construction of `System.Random` | `IRandom`, `Random.service`, or `Random.nextDouble` |
| `guid` | `Guid.NewGuid()` | `IGuid`, `Guid.service`, or `Guid.newGuid` |
| `clock` | Ambient date and time properties, `Stopwatch`, and `Task.Delay` | `IClock`, `Clock.service`, `Clock.utcNow`, `Clock.timed`, `Flow.sleep`, or `Schedule` |
| `environment` | Ambient environment variables, machine or process properties, and `TimeZoneInfo.Local` | `IEnvironment` or a value passed through `'env`; an explicit `TimeZoneInfo` for conversions; `Parallelism.ofProcessors` for `ProcessorCount` |
| `console` | Any `System.Console` member | `Axial.Console.IConsole` |
| `filesystem` | Any `System.IO.File` or `System.IO.Directory` member | `Axial.FileSystem.IFileSystem` |
| `process` | `System.Diagnostics.Process.Start` | `Axial.Process.IProcess` |
| `sleep` | `Thread.Sleep` | `Flow.sleep` or `Schedule` |
| `http` | Construction of `HttpClient` | `Axial.HttpClient.IHttp`, with `Http.live` over one shared client |
| `blocking` | `Async.RunSynchronously`, `Task.Wait()`/`Wait(timeout)`, task/value-task `.Result`, and `.GetAwaiter().GetResult()` outside flows; task `.Result` and `.GetAwaiter().GetResult()` inside flows | Await asynchronously outside flows; `let!` on the task inside flows |
| `scope` | A direct `Cache.make*`, `Hub.make`/`bounded`, or `Queue.bounded`/`unbounded` returned by a run | Use the service before the scope closes |

Reads of `.Result` or `.GetAwaiter().GetResult()` on a task or value task are exempt when the same value's completion state is tested in the enclosing `if` branch or `when` guard, or when the value is the parameter of a `ContinueWith` callback. Other waits remain findings. Blocking waits outside flows can deadlock UI synchronization contexts.

`TimeZoneInfo.ConvertTimeFromUtc(utc, zone)` is allowed when `zone` is supplied explicitly; reading
`TimeZoneInfo.Local` to supply that argument is not. Pass the desired zone through configuration or `'env`.

The analyzer matches resolved `System.*` symbols, not source text. It does not flag an application type named `Random` or a local value named `now`.

### Allow an intentional effect boundary

First replace the ambient call with the service named by the diagnostic. An automatic fix is not offered because
introducing an explicit service usually changes the containing function's environment and often its return type.

A normal live implementation belongs in a file assigned the `capabilities` role and needs no suppression:

```toml
[files]
capabilities = ["src/App/Capabilities/**"]
```

For an exceptional one-off boundary outside that role, declare the exact file in policy and give each suppression a
non-empty reason:

```toml
[boundaries]
files = ["src/App/LiveClock.fs"]
```

```fsharp
let live : IClock =
    let stopwatch = System.Diagnostics.Stopwatch.StartNew() // axial-allow-AXG001: live clock implementation

    { new IClock with
        member _.UtcNow() = DateTimeOffset.UtcNow // axial-allow-AXG001: live clock implementation
        member _.Elapsed() = stopwatch.Elapsed
        member _.Sleep(delay, token) = Task.Delay(delay, token) }
```

The file-wide form belongs in the leading comment block:

```fsharp
// axial-allow-file-AXG001: this file provides the live console implementation
```

A rule-specific suppression allows only that diagnostic. It does not allow other Guardrails findings in the file.
Capability-role globs are the normal home for raw I/O. `[boundaries].files` contains exact exceptional files; it is
not a second capability-folder mechanism. A `forbid` rule cannot be suppressed even in a declared boundary.

## AXG002: Remove invalid suppressions

`AXG002` validates every suppression. It reports legacy or malformed syntax, an unknown rule ID, a missing reason,
a file-wide suppression outside the leading comment block, a suppression outside a TOML-declared boundary file,
or a stale suppression that covers no finding. AXG009–013 allowances are invalid: these rules cannot be suppressed.

It also validates `axial-note` acknowledgements for guide findings (AXG010 and awaited-delay AXG013).
A note needs a non-empty reason and a live guide finding on the same or following line. Notes for block findings,
unknown rule IDs, malformed notes, and stale notes warn during development and become errors at checkpoints.
Valid notes leave their guide finding visible as Info; they never hide it.

Remove an invalid or unused directive. AXG002 offers an automatic removal when the directive occupies its own comment
line. For an intentional boundary, declare the file and use a rule-specific comment such as
`// axial-allow-AXG001: <reason>` with the ID of the finding being allowed.

## AXG003: Return typed failures from workflows

A `Flow<'env, 'error, 'value>` represents expected failures through its `'error` channel. Raising an exception inside `flow { }` creates an unhandled defect instead, so callers cannot handle it as an expected error.

`AXG003` detects direct calls to `raise`, `failwith`, `failwithf`, `invalidOp`, `invalidArg`, and `reraise` inside `flow { }`. It also checks nested conditionals, matches, bindings, and lambdas.

Use `return! Flow.fail error` for an expected failure. Use `Flow.die` when the failure is an unrecoverable defect.

For a standalone `raise`, `failwith`, `failwithf`, or `invalidOp`, AXG003 offers a `return! Flow.fail ...` fix. Calls
embedded in a larger expression need a manual rewrite because inserting `return!` there would be invalid.

If a nearby `try/with` catches and translates the exception, declare the file as a boundary and add a reason:

```fsharp
let start (proc: System.Diagnostics.Process) =
    if not (proc.Start()) then raise (Exception "Process did not start") // axial-allow-AXG003: caught and translated by caller
```

Use this directive only when the same local boundary catches and translates the exception.

## AXG004: Create fresh test fixtures

A module-level `let` value is initialized once and shared by every test in the module. Mutable or stateful values can therefore make parallel xUnit tests interfere with each other.

`AXG004` checks modules that contain an xUnit `[<Fact>]` or `[<Theory>]`, or an FsCheck `[<Property>]`. It reports module-level values but not functions such as `let fixture () = ...`.

The analyzer excludes constants and point-free function definitions because they do not create the shared-state risk.

Create fixtures inside each test or expose a function that returns a fresh fixture. There is no automatic fix because changing a value to a function requires updating all call sites. For a safe
immutable value in a declared boundary test file, add a reason:

```fsharp
let private syntheticTime = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) // axial-allow-AXG004: immutable timestamp
```

## AXG005: Preserve cancellation tokens

Task adapters receive Flow's cancellation token. Discarding that token prevents cancellation from stopping the underlying operation.

`AXG005` checks `ColdTask`, `ColdTask.create`, `Flow.fromTask`, and `Flow.fromTaskResult`. It reports a single-argument lambda when the argument is a bare discard, such as `fun _ -> ...`.

Use the cancellation-aware overload of the wrapped operation and pass the token to it.

No automatic fix is offered because the analyzer cannot choose the foreign API's cancellation-aware overload or know
where its token belongs. If a legacy API has no such overload, declare the adapter file as a boundary and add a reason:

```fsharp
/// An API with no cancellation support.
let legacyCall () : Task<int> = Task.FromResult 42

let legacy = ColdTask(fun _ -> legacyCall ()) // axial-allow-AXG005: legacy API has no token overload
```

This check does not run in test projects. Test adapters often wrap completed tasks that have no work to cancel.

## AXG006: Format without reflection

F# unions and records get a compiler-generated `ToString()` that calls `sprintf "%+A"`. `%A` walks the value with
`FSharp.Reflection`, and FSharp.Core's `option`, `voption`, `list`, `Result`, `Choice`, `Map`, and `Set` print their
contents the same way. NativeAOT and full trimming remove the metadata this needs. The call then throws or prints the
wrong text, and the build gives no specific warning because the reflection happens inside FSharp.Core.

`AXG006` reports these formatting sites in typed code:

- `x.ToString()`, `string x`, and interpolation holes such as `$"{x}"` when `x` is an F# union, record, anonymous
  record, exception, or one of the FSharp.Core types above, and the type has no hand-written `ToString` override
- the same forms on a type parameter, whose substituted type may be a union or record
- `%A` on any value other than a primitive, even when its type overrides `ToString`
- a boxed value of those types passed to `String.Format`, `String.Concat`, `StringBuilder.Append`,
  `TextWriter.Write`, `Console.Write`, `Trace.WriteLine`, or `Debug.WriteLine`

Render the value explicitly instead. Match on the cases, call a `describe` function, or give the type a hand-written
override:

```fsharp
type OrderError =
    | OutOfStock of sku: string
    | InvalidQuantity of int

    override this.ToString() =
        match this with
        | OutOfStock sku -> $"Out of stock: {sku}"
        | InvalidQuantity quantity -> $"Invalid quantity: {quantity}"
```

With that override, `$"{error}"`, `string error`, and `%O` are safe. `%A` is not.

Axial's own public unions render themselves this way, including `Exit`, `Cause`, `FiberStatus`, `FiberId`, and the
`Process`, `HttpClient`, `FileSystem`, and `PlatformService` error types. A payload inside `Exit` or `Cause` is still
rendered with its own `ToString`, so give your error types an override too. `Cause.prettyPrint` also accepts an
explicit error renderer.

No automatic fix is offered because rendering is domain-specific. If code never runs trimmed, such as a script or a
test helper, declare the file as a boundary and add a reason:

```fsharp
let printDiagnostics (diagnostics: Map<string, int list>) =
    printfn "%A" diagnostics // axial-allow-AXG006: diagnostic helper never runs trimmed
```

Axial's packable libraries are compiled with F#'s `--reflectionfree` option. This prevents the compiler from
emitting reflection-based structural `ToString()` implementations and rejects `%A` in those libraries. A NativeAOT
application can apply the same option:

```xml
<PropertyGroup>
  <OtherFlags>$(OtherFlags) --reflectionfree</OtherFlags>
</PropertyGroup>
```

The option does not make FSharp.Core's `printf` implementation reflection-free. An application that uses `printf`,
including formats without `%A`, can still produce the aggregate `IL2104` and `IL3053` warnings for `FSharp.Core` and
can fail at run time after trimming. Use explicit rendering with `System.Console.WriteLine` or another non-`printf`
output API on NativeAOT paths.

`scripts/run-aot-probe.sh` publishes a NativeAOT probe that renders these types and dumps a fiber registry, so the
release checks catch a regression that the analyzer cannot see. The probe itself uses `printf` to label checks, so
its publish currently reports the two aggregate FSharp.Core warnings above; it reports no such warning for an Axial
assembly.

## AXG007: Keep resources acquired by a fiber

A forked fiber owns the [scoped resources](/scopes/index.html#scoped-resources) it acquires and releases them when it
settles. `Fiber.join` returns the fiber's value after that, so a resource the fiber returns is already released:

```fsharp
let connection : Resource<ClockEnvironment, Never, MemoryStream> =
    Resource.create
        (Flow.ok (new MemoryStream()))
        (fun stream -> Flow.delay (fun () -> stream.Dispose(); Flow.ok ()))

/// Reported: the connection is released before join returns it.
let releasedBeforeUse =
    flow {
        let! fiber = Flow.scopeResource connection |> Flow.fork
        return! Fiber.join fiber
    }
```

The code compiles and reads like ZIO, where a forked fiber shares its parent's `Scope`. In Axial the forked
fiber's own scope closes when it settles, so the release runs before `Fiber.join` returns: the stream is
already disposed, and the first use after the join throws `ObjectDisposedException`.

`AXG007` reports a `Flow.fork`, `forkNamed`, `forkGraceful`, `forkDetached`, `forkReplacing`, or `forkReplacingKey` of
a flow that is a `Flow.scopeResource` or `Flow.scopeAcquireRelease` call, or a `flow { }` that returns what it acquired
with one of them. Acquire the resource with `Flow.forkResource` to keep it in the caller's scope, or with `Flow.zipPar`
or `Flow.sequencePar` to acquire several in parallel:

```fsharp
let keptForUse =
    flow {
        let! connecting = Flow.forkResource connection
        return! Fiber.join connecting
    }
```

[Concurrency and scoped resources](/scopes/index.html#concurrency-and-scoped-resources) explains which scope owns a
resource in each concurrent combinator.

No automatic fix is offered because `forkResource`, `zipPar`, and keeping ownership inside the fiber have different
semantics. If the fiber intentionally owns and consumes the resource, declare the file as a boundary and use
`// axial-allow-AXG007: <reason>`.

## AXG013: Wait for facts, not time

In policy role `test`, `AXG013` reports `Thread.Sleep` as an error and directly awaited
`Task.Delay` or `Async.Sleep` (`do!` or `let!`) as guide warnings. The legacy `test.wait-for-facts`
level does not change these tiers. A guessed pause does not prove
that background work reached the point an assertion needs.

Reported:

```fsharp
open System.Threading.Tasks

let guessedWait (assertReady: unit -> unit) = task {
    do! Task.Delay 10
    assertReady ()
}
```

Fixed: the worker completes the gate at the point the test needs to observe. The timeout is only a failure deadline.

```fsharp
open System
open System.Threading.Tasks

let waitForReady (ready: TaskCompletionSource<unit>) (assertReady: unit -> unit) = task {
    do! ready.Task.WaitAsync(TimeSpan.FromSeconds 30.)
    assertReady ()
}
```

A `Deferred` completed by the worker serves the same purpose. For scheduling behavior, step a test clock instead.
`Task.WhenAny(work, Task.Delay duration)` (including array arms or a locally bound deadline), fact-based
`Wait(timeout)` / `WaitAsync(timeout)`, and `SpinWait.SpinUntil(predicate, timeout)` are allowed.
An infinite `Async.Sleep`, or `Task.Delay` with an explicit cancellation-token parameter, is also allowed
(`Timeout.Infinite`, `Timeout.InfiniteTimeSpan`, or literal `-1`). Async workflows supply their own cancellation
token. A tokenless infinite `Task.Delay` and any `Thread.Sleep`, even an infinite one, remain reported. A bare sleep inside a `while` or `for` loop is still
reported: counting iterations is a guessed duration, and unbounded polling can hang. In Axial's own tests,
`Axial.Tests.TestSupport.waitUntil condition deadline` is the approved bounded polling helper; only its
implementation may contain polling sleeps. Its `TimeSpan` deadline is a failure bound and it returns `false`
on expiry. Finite waits elsewhere remain reported even when they simulate slow work, generate load, or
schedule cancellation.
The analyzer checks direct awaited delay calls, not aliases or data flow through helper functions.
`Flow.sleep` and `Clock.sleep` are out of scope: their syntax does not establish whether the environment supplies
live or controlled time. Manual-clock advance operations are also allowed. No automatic fix is offered because
only the test author knows which fact must be observed. For an intentional awaited delay, put
`// axial-note AXG013: <reason>` on the finding's line or the preceding line. It stays visible as Info,
including at checkpoints. `Thread.Sleep` cannot be acknowledged or suppressed.

## AXG008: Keep scope-owned services inside their run

`Flow.run`, `Flow.runSync`, `Flow.RunSynchronously`, and `StartAsTask` close their scope after completion. Returning `Queue.makeScoped`, `Hub.makeScoped`, or `Hub.subscribe` directly leaves a shut-down service. Caches made by `Cache.make`, `makeBounded`, `makeBoundedWithExpiry`, or `makeRec` capture the creating runtime; closing its scope interrupts lookups. Use these services inside the flow or keep their owning scope open. `Queue.make` (and its `bounded`, `unbounded`, `dropping`, and `sliding` shorthands) and `Hub.make` do not register with the scope and may be returned. The syntax check sees direct calls and pipelines ending in a scoped constructor; the result-type check catches composed cache flows but does not flag Queue or Hub by type because it cannot distinguish scoped from unscoped construction. No automatic fix is offered because the correct lifetime depends on the caller. At an intentional boundary, declare the file and use `// axial-allow-AXG008: <reason>`.

## AXG009: Keep raw concurrency inside the runtime

Reports `Task`, `TaskCompletionSource`, and `CancellationTokenSource` construction, `Task.Run`,
`Task.Factory.StartNew`, `ContinueWith`, `Thread` construction, and `ThreadPool` calls outside runtime files
and the `capabilities` role. Prefer Flow concurrency; raw coordination belongs at an OS bridge, not throughout
an adapter. `Interlocked`, F# `lock`, and `Monitor` are reported as warnings outside declared runtime files,
including in capability, host-tooling, and test code.

```fsharp
// Reported: scheduling bypasses Flow ownership.
let rawWork () = System.Threading.Tasks.Task.Run(fun () -> 1)
// Fixed: the caller runs and owns the workflow.
let ownedWork () = Flow.succeed 1
```

Raw task findings are errors and cannot be acknowledged or suppressed; move bridges into capability-role modules.
Synchronization findings remain warnings until runtime consolidation; notes do not downgrade them.
Exact repository-relative paths in `[runtime].files` exempt runtime implementations from AXG009 only.
This approval-sensitive list is protected by the policy lock; `[boundaries].files` does not exempt raw concurrency.

## AXG010: Do not silently swallow exceptions

Reports only catch-all `try/with` handlers (`_`, a bound exception, or a type test for `exn` / `System.Exception`)
that neither use the exception nor call `raise` or `reraise`. Handlers for narrower exception types are not reported;
type abbreviations are resolved. Returning or recording the bound exception counts as use, including assignments,
error-record fields, and chained calls such as `ExceptionDispatchInfo.Capture(error).Throw()`.
A guard that inspects the bound exception counts as classification. A reporting function's name alone does not
establish exception use. Wildcard handlers returning a fallback value are still reported; returning `Error` or
`Exit.Failure` alone does not prove the exception was handled.

```fsharp
let swallowed () = try failwith "failed" with _ -> ()
let propagated () = try failwith "failed" with _ -> reraise ()
```

This is a guide warning. When an exception is intentionally irrelevant, put
`// axial-note AXG010: <specific reason>` on the handler body's first line or the preceding line.
The finding stays visible as Info with the reason. An unacknowledged finding becomes an error at a checkpoint.

## AXG011: Explain uncancellable work

`CancellationToken.None` is temporarily a warning, including inside runtime files. Pass the owner's token;
cleanup that must finish after cancellation belongs in the runtime's uninterruptible cleanup primitive.
It is intended to become an error after that consolidation, with one exact runtime primitive exempt.

```fsharp
let unownedToken = System.Threading.CancellationToken.None
let ownedToken (token: System.Threading.CancellationToken) = token
```

Neither `axial-allow` nor `axial-note` exempts this finding. Checkpoints do not promote this temporary warning.

## AXG012: Retain ownership of concurrent work

Reports discarded Task, ValueTask, and Fiber values in wildcard bindings, statements, and pipelines to `ignore`.
The typed check follows type abbreviations and catches aliased task factories. Both direct calls and pipelines
to F#'s `ignore` count; a same-named user function does not. `Async.Start` / `Async.StartImmediate` return unit and
are out of scope. Awaited `let!` / `do!` expressions and array/list elements retained in a collection are not
discarded statements. Explicit casts do not hide a discarded Task. A discarded `Flow.fork` description is cold:
without executing the returned Flow, no fiber starts, so AXG012 does not report it.
Retaining a value alone does not prove it will be
awaited: this rule detects local discards, not whole-program ownership.

```fsharp
let discarded () = System.Threading.Tasks.Task.FromResult 1 |> ignore
let retained () = System.Threading.Tasks.Task.FromResult 1
```

This is an error. Await the task or retain and join the fiber; neither `axial-allow` nor `axial-note` exempts it.

Except for AXG009 synchronization findings, AXG009–AXG012 exempt the `test` role and host-role tooling
projects under `src/Axial.Guardrails` or `src/Axial.Tool*`; an ordinary host is not exempt.
AXG001–008 retain their boundary-allowance and baseline behavior. AXG009–013 do not accept allowances or baseline
severity downgrades. Only guide findings (AXG010 and awaited-delay AXG013) accept `axial-note`; no boundary
membership or finding approval is needed. AXG002 warns about malformed, empty, unknown, misplaced, or stale notes,
and notes on block findings. Invalid notes become errors at checkpoints. No automatic fix is offered: choosing
ownership, cancellation, or exception reporting changes behavior.

The verification build and release workflow set `AxialGuardrailsCheckpoint=true`. Ordinary builds, including
ordinary Release builds, do not enable it automatically. At checkpoints, unacknowledged guide findings become errors;
acknowledged findings remain Info with their reasons. Build output is the review list; there is no separate snapshot
or review manifest. The existing policy lock still protects roles and enforcement configuration.
