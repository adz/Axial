---
title: Guardrail diagnostics
linkTitle: Guardrail diagnostics
description: The contract of each AXG diagnostic, what it detects, what to use instead, and how to allow an intentional boundary.
---

# Guardrail diagnostics

Each `AXG` diagnostic has a narrow contract: the effect or convention it detects, the service or form that replaces
it, and the suppression directive that allows an intentional boundary. The [adoption guide](/guardrails/guardrails.html) covers
install, severity, and suppression choices; this page is the per-diagnostic reference.

## raw-effect (AXG001): Use explicit effect services

`raw-effect` detects direct calls to ambient .NET effects.

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

Each finding says what to use instead. For a service that travels in `'env`, it names the environment field to add,
the interface to implement, an example call, the live implementation and the test fake. For a platform service such as
`Clock`, it says the runtime provides it and how tests control it. `axial capabilities` lists every service and the
APIs it replaces.

No file is exempt as a whole. A file may call a capability's raw API only when its `Compile` item claims that
capability with [`AxialCapability` metadata](/guardrails/rule-authoring.html#claim-a-capability), and then only that
API. Every allowed call is counted by `axial check`.

Reads of `.Result` or `.GetAwaiter().GetResult()` on a task or value task are exempt when the same value's completion state is tested in the enclosing `if` branch or `when` guard, or when the value is the parameter of a `ContinueWith` callback. Other waits remain findings. Blocking waits outside flows can deadlock UI synchronization contexts.

`TimeZoneInfo.ConvertTimeFromUtc(utc, zone)` is allowed when `zone` is supplied explicitly; reading
`TimeZoneInfo.Local` to supply that argument is not. Pass the desired zone through configuration or `'env`.

The analyzer matches resolved `System.*` symbols, not source text. It does not flag an application type named `Random` or a local value named `now`.

### Allow an intentional raw call

First replace the ambient call with the service named by the diagnostic. An automatic fix is not offered because
introducing an explicit service usually changes the containing function's environment and often its return type.

A file that implements a service, or tests it against the real thing, claims the capability on its `Compile` item:

```xml
<Compile Include="Live/Stripe.fs" AxialCapability="payments" />
<Compile Include="FilesTests.fs" AxialCapability="filesystem" />
```

The claimed file may then call that capability's API, and nothing else raw. Your own capabilities are
[defined in `.capability.toml` files](/guardrails/rule-authoring.html#define-a-capability).

When Axial or a capability cannot do the job yet, allow one call with a comment that names the gap:

```fsharp
let pipe = new System.IO.Pipes.NamedPipeServerStream("app-control") // axial-allow-raw-effect: Axial.Process has no named pipes yet (gap: process-named-pipes)
```

For a raw call that is deliberate rather than a missing feature, say why instead:
`// axial-allow-raw-effect: (why: this test checks the real clock)`.

The allowance covers its own line or the next one. It must end with `(gap: name)` or `(why: reason)`, it cannot cover
a whole file, and a `forbid` rule cannot be allowed. Policy review lists every new raw-effect allowance first. A rule-specific allowance allows only that diagnostic.

## stale-allow (AXG002): Remove invalid suppressions

`stale-allow` validates every suppression. It reports legacy or malformed syntax, an unknown rule name, an old numbered spelling (`axial-allow-AXG001`, with the name to use), a missing reason,
a file-wide suppression outside the leading comment block, a file-wide raw-effect allowance, an raw-effect allowance that
names neither a gap nor a why, or a stale suppression that covers no finding. An raw-effect allowance is also stale when
it allows nothing: the file claims the capability or is a `host` file, so the call needs no comment, or the level is
`forbid`, which no allowance overrides. A claim on a file that makes no raw call for that capability is a warning that
names the claim to remove from the project file. swallowed-exn, cancel-none, dropped-work, guessed-wait and task-surface allowances are invalid: these rules cannot be suppressed. A raw-concurrency allowance covers one call and names its why or gap.

It also validates `axial-note` acknowledgements for guide findings (swallowed-exn and awaited-delay guessed-wait).
A note needs a non-empty reason and a live guide finding on the same or following line. For swallowed-exn put the note as
the first line inside the handler body, immediately before its first expression (not above `with` or the pattern).
Notes for block findings, unknown rule IDs, malformed notes, and stale notes warn during development and become
errors at checkpoints.
Valid notes leave their guide finding visible as Info; they never hide it.

Remove an invalid or unused directive. stale-allow offers an automatic removal when the directive occupies its own comment
line. For an intentional exception, use a rule-specific comment such as `// axial-allow-raise-in-flow: <reason>` with the
name of the rule being allowed; an raw-effect allowance also names its gap or why. Every directive is recorded in the policy
lock, so each new one is a review item.

## raise-in-flow (AXG003): Return typed failures from workflows

A `Flow<'env, 'error, 'value>` represents expected failures through its `'error` channel. Raising an exception inside `flow { }` creates an unhandled defect instead, so callers cannot handle it as an expected error.

`raise-in-flow` detects direct calls to `raise`, `failwith`, `failwithf`, `invalidOp`, `invalidArg`, and `reraise` inside `flow { }`. It also checks nested conditionals, matches, bindings, and lambdas.

Use `return! Flow.fail error` for an expected failure. Use `Flow.die` when the failure is an unrecoverable defect.

For a standalone `raise`, `failwith`, `failwithf`, or `invalidOp`, raise-in-flow offers a `return! Flow.fail ...` fix. Calls
embedded in a larger expression need a manual rewrite because inserting `return!` there would be invalid.

If a nearby `try/with` catches and translates the exception, add a reason:

```fsharp
let start (proc: System.Diagnostics.Process) =
    if not (proc.Start()) then raise (Exception "Process did not start") // axial-allow-raise-in-flow: caught and translated by caller
```

Use this directive only when the same local boundary catches and translates the exception.

## shared-fixture (AXG004): Create fresh test fixtures

A module-level `let` value is initialized once and shared by every test in the module. Mutable or stateful values can therefore make parallel xUnit tests interfere with each other.

`shared-fixture` checks modules that contain an xUnit `[<Fact>]` or `[<Theory>]`, or an FsCheck `[<Property>]`. It reports module-level values but not functions such as `let fixture () = ...`.

The analyzer excludes constants and point-free function definitions because they do not create the shared-state risk.

Create fixtures inside each test or expose a function that returns a fresh fixture. There is no automatic fix because changing a value to a function requires updating all call sites. For a safe
immutable value, add a reason:

```fsharp
let private syntheticTime = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) // axial-allow-shared-fixture: immutable timestamp
```

## dropped-cancel (AXG005): Preserve cancellation tokens

Task adapters receive Flow's cancellation token. Discarding that token prevents cancellation from stopping the underlying operation.

`dropped-cancel` checks `ColdTask`, `ColdTask.create`, `Flow.fromTask`, and `Flow.fromTaskResult`. It reports a single-argument lambda when the argument is a bare discard, such as `fun _ -> ...`.

Use the cancellation-aware overload of the wrapped operation and pass the token to it.

No automatic fix is offered because the analyzer cannot choose the foreign API's cancellation-aware overload or know
where its token belongs. If a legacy API has no such overload, add a reason:

```fsharp
/// An API with no cancellation support.
let legacyCall () : Task<int> = Task.FromResult 42

let legacy = ColdTask(fun _ -> legacyCall ()) // axial-allow-dropped-cancel: legacy API has no token overload
```

This check is omitted from the declared `test` project's default analyzer set. Test adapters often wrap completed
tasks that have no work to cancel. `AxialGuardrailsInTests=all` enables it; `IsTestProject` does not select the set.

## reflection-format (AXG006): Format without reflection

F# unions and records get a compiler-generated `ToString()` that calls `sprintf "%+A"`. `%A` walks the value with
`FSharp.Reflection`, and FSharp.Core's `option`, `voption`, `list`, `Result`, `Choice`, `Map`, and `Set` print their
contents the same way. NativeAOT and full trimming remove the metadata this needs. The call then throws or prints the
wrong text, and the build gives no specific warning because the reflection happens inside FSharp.Core.

`reflection-format` reports these formatting sites in typed code:

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
test helper, add a reason:

```fsharp
let printDiagnostics (diagnostics: Map<string, int list>) =
    printfn "%A" diagnostics // axial-allow-reflection-format: diagnostic helper never runs trimmed
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

## fiber-resource (AXG007): Keep resources acquired by a fiber

A forked fiber owns the [scoped resources](/scopes/index.html#scoped-resources) it acquires and releases them when it
settles. `Fiber.join` returns the fiber's value after that, so a resource the fiber returns is already released:

```fsharp
let connection : Resource<unit, Never, MemoryStream> =
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

`fiber-resource` reports a `Flow.fork`, `forkNamed`, `forkGraceful`, `forkDetached`, `forkReplacing`, or `forkReplacingKey` of
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
semantics. If the fiber intentionally owns and consumes the resource, use `// axial-allow-fiber-resource: <reason>`.

## guessed-wait (AXG013): Wait for facts, not time

In policy role `test`, `guessed-wait` reports `Thread.Sleep` as an error and directly awaited
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
    let! completed = Task.WhenAny(ready.Task :> Task, Task.Delay(TimeSpan.FromSeconds 30.))
    if not (obj.ReferenceEquals(completed, ready.Task)) then
        raise (TimeoutException("Worker did not become ready"))
    do! ready.Task
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
on expiry. `Axial.Tests.TestSupport.occupyThread duration` is the one approved way to simulate work by blocking a
thread (a slow handler, a loaded pool); it is never a wait for a fact. Finite waits elsewhere remain reported even
when they generate load or schedule cancellation.
The analyzer checks direct awaited delay calls, not aliases or data flow through helper functions.
`Flow.sleep` and `Clock.sleep` are out of scope: their syntax does not establish whether the environment supplies
live or controlled time. Manual-clock advance operations are also allowed. No automatic fix is offered because
only the test author knows which fact must be observed. For an intentional awaited delay, put
`// axial-note guessed-wait: <reason>` on the finding's line or the preceding line. It stays visible as Info,
including at checkpoints. `Thread.Sleep` cannot be acknowledged or suppressed.

## escaped-scope (AXG008): Keep scope-owned services inside their run

`Flow.run`, `Flow.runSync`, `Flow.RunSynchronously`, and `StartAsTask` close their scope after completion. Returning `Queue.makeScoped`, `Hub.makeScoped`, or `Hub.subscribe` directly leaves a shut-down service. Caches made by `Cache.make`, `makeBounded`, `makeBoundedWithExpiry`, or `makeRec` capture the creating runtime; closing its scope interrupts lookups. Use these services inside the flow or keep their owning scope open. `Queue.make` (and its `bounded`, `unbounded`, `dropping`, and `sliding` shorthands) and `Hub.make` do not register with the scope and may be returned. The syntax check sees direct calls and pipelines ending in a scoped constructor; the result-type check catches composed cache flows but does not flag Queue or Hub by type because it cannot distinguish scoped from unscoped construction. No automatic fix is offered because the correct lifetime depends on the caller. At an intentional boundary, declare the file and use `// axial-allow-escaped-scope: <reason>`.

## raw-concurrency (AXG009): Keep raw concurrency where it is claimed

Reports `Task`, `TaskCompletionSource`, and `CancellationTokenSource` construction, `Task.Run`,
`Task.Factory.StartNew`, `ContinueWith`, `Thread` construction, and `ThreadPool` calls. Work started that way escapes
Flow's ownership: nothing interrupts it, waits for its cleanup or reports its crash. Use Flow concurrency instead:
`Flow.fork`, `race`, `Queue`, `Deferred` and `Semaphore`.

```fsharp
// Reported: scheduling bypasses Flow ownership.
let rawWork () = System.Threading.Tasks.Task.Run(fun () -> 1)
// Fixed: the caller runs and owns the workflow.
let ownedWork () = Flow.succeed 1
```

Raw concurrency works like raw effects:

- **A claim**, for a file whose job is raw concurrency, such as Flow's own runtime or an OS bridge:
  `<Compile Include="Bridge.fs" AxialCapability="concurrency" />`. raw-concurrency, dropped-work and task-surface do not apply to it.
- **An allowance**, for one deliberate call anywhere else:
  `// axial-allow-raw-concurrency: (why: the OS callback completes it on its own thread)` on the line above, or `(gap: name)`
  for a missing Axial feature. It covers one call, never a file, and policy review lists every new one first.

Test projects, and `host` projects listed under `[exemptions] raw-concurrency`, are exempt. No role exempts a file by itself.

Mutual exclusion is not reported: F# `lock`, `Monitor.Enter` and `Exit`, `Interlocked` and `Volatile` guard shared
state and start nothing. Use `Ref` or `STM` where the state lives in a flow, but the analyzer does not require it.
Blocking coordination (`Monitor.Wait`, wait handles, `ManualResetEventSlim.Wait`, `SemaphoreSlim.Wait`,
`Thread.Join`, `CountdownEvent.Wait`, `Barrier.SignalAndWait`) is reported by raw-effect as a `blocking` wait.

## swallowed-exn (AXG010): Do not silently swallow exceptions

Reports only catch-all `try/with` handlers (`_`, a bound exception, or a type test for `exn` / `System.Exception`)
that neither use the exception nor call `raise` or `reraise`. Handlers for narrower exception types are not reported;
type abbreviations are resolved. When the exception value is irrelevant, narrow the catch to the specific type(s),
for example `| :? FileNotFoundException ->`. A narrowed catch need not bind or use an exception value; do not add
an exception to a return value merely to silence this guide. Returning or recording the bound exception counts as use, including assignments,
error-record fields, and chained calls such as `ExceptionDispatchInfo.Capture(error).Throw()`.
A guard that inspects the bound exception counts as classification. A reporting function's name alone does not
establish exception use. Wildcard handlers returning a fallback value are still reported; returning `Error` or
`Exit.Failure` alone does not prove the exception was handled.

```fsharp
let swallowed () = try failwith "failed" with _ -> ()
let narrowed () =
    try raise (System.IO.FileNotFoundException())
    with :? System.IO.FileNotFoundException -> ()
```

This is a guide warning. When an exception is intentionally irrelevant, put
`// axial-note swallowed-exn: <specific reason>` as the first line inside the handler body, immediately before its first
expression. The note must not sit above `with` or the pattern.

```fsharp
let invalid () =
    try
        failwith "failed"
    // axial-note swallowed-exn: too far from the handler body
    with _ ->
        ()

let acknowledged () =
    try
        failwith "failed"
    with _ ->
        // axial-note swallowed-exn: best-effort cleanup; the failure is reported elsewhere
        ()
```

The finding stays visible as Info with the reason. An unacknowledged finding becomes an error at a checkpoint.
Host boundary projects may keep catch-alls under an explicit `swallowed-exn` project-path declaration in `[exemptions]`.
The `host` role alone, or a capability claim, is not an exemption.

## cancel-none (AXG011): Explain uncancellable work

`CancellationToken.None` is an error everywhere except the runtime's single `Cancellation.never` definition. Pass the
owner's token; cleanup that must finish after cancellation belongs in the runtime's uninterruptible cleanup primitive.

```fsharp
let unownedToken = System.Threading.CancellationToken.None
let ownedToken (token: System.Threading.CancellationToken) = token
```

Neither `axial-allow` nor `axial-note` exempts this finding. It is already an error, so checkpoints change nothing.

## dropped-work (AXG012): Retain ownership of concurrent work

Reports discarded Task, ValueTask, and Fiber values in wildcard bindings, statements, and pipelines to `ignore`.
The typed check follows type abbreviations and catches aliased task factories. Both direct calls and pipelines
to F#'s `ignore` count; a same-named user function does not. `Async.Start` / `Async.StartImmediate` return unit and
are out of scope. Awaited `let!` / `do!` expressions and array/list elements retained in a collection are not
discarded statements. Explicit casts do not hide a discarded Task. A discarded `Flow.fork` description is cold:
without executing the returned Flow, no fiber starts, so dropped-work does not report it.
Retaining a value alone does not prove it will be
awaited: this rule detects local discards, not whole-program ownership.

```fsharp
let discarded () = System.Threading.Tasks.Task.FromResult 1 |> ignore
let retained () = System.Threading.Tasks.Task.FromResult 1
```

This is an error. Await the task or retain and join the fiber; neither `axial-allow` nor `axial-note` exempts it.

## task-surface (AXG014): Keep raw Task at the edge

`task-surface` (`TaskSurface`) is guidance, never a gate. A raw `Task` escapes Flow's cancellation, supervision and
testability: nothing owns it, cancels it or replaces it in a test. The rule nudges code toward `flow { }` and keeps
the remaining Task use in thin, one-call bridges. Its findings are Info in every mode, including Full checkpoints and
`axial check --full`, and under warnings-as-errors.

What it reports depends on the file's resolved role (`[files]` overrides `[projects]`):

| Role | Reported |
| --- | --- |
| `domain`, `services` | Every `task { }` and `backgroundTask { }` expression, and every public function, value, member or abstract slot whose return type is `Task`, `Task<'T>`, `ValueTask` or `ValueTask<'T>` |
| `capabilities`, `host` | Only a task expression that composes: more than one `let!`/`do!`/`use!`/`match!`/`return!`, a loop, or an `if`/`match` that decides what to await (it contains an await or comes before the first one) in its own body |
| a file that claims `concurrency` | Nothing: it implements Flow-level concurrency on raw tasks |
| `test` | Nothing, unless `AxialGuardrailsInTests=all`; then the bridge rule applies |

Translating the one awaited result is what a bridge is for: a `try/with` that maps its exceptions, or a `match` on
its result, does not count as composition. Nor does argument validation before the await: an `if` without `else`
that contains no await. Class `let` bindings are private to the type and are not public signatures. Nested lambdas and computation expressions are judged on their own.

Overrides and interface implementations are not reported: a .NET interface dictates that signature. Private and
internal declarations are not part of the public surface.

Reported in domain code:

```fsharp
open System.Net.Http
open System.Threading.Tasks

let fetchTitleTask (client: HttpClient) (url: string) : Task<string> =
    task {
        let! body = client.GetStringAsync url
        return body.Trim()
    }
```

Fixed: the logic is a flow, and the one .NET call is lifted with `Flow.fromTask`, which passes the flow's
cancellation token. In domain code, prefer a capability or service (`Axial.HttpClient.IHttp` here) over a raw client.

```fsharp
open System.Net.Http
open Axial

let fetchTitle (client: HttpClient) (url: string) : Flow<unit, string, string> =
    flow {
        let! body = Flow.fromTask (fun ct -> client.GetStringAsync(url, ct))
        return body.Trim()
    }
```

Reported in a capability: the bridge sequences two calls inside the task.

```fsharp
open System.Net.Http
open Axial

let readBothInOneTask (client: HttpClient) (first: string) (second: string) : Flow<unit, string, string> =
    Flow.fromTask (fun ct ->
        task {
            let! a = client.GetStringAsync(first, ct)
            let! b = client.GetStringAsync(second, ct)
            return a + b
        })
```

Fixed: each bridge is one awaited call, and the composition is a flow.

```fsharp
open System.Net.Http
open Axial

let get (client: HttpClient) (url: string) : Flow<unit, string, string> =
    Flow.fromTask (fun ct -> client.GetStringAsync(url, ct))

let readBoth (client: HttpClient) (first: string) (second: string) : Flow<unit, string, string> =
    flow {
        let! a = get client first
        let! b = get client second
        return a + b
    }
```

To hand a flow to a Task-based host, run it at the host boundary (`flow.StartAsTask(env, ct)`), not in domain code.
For an intentional Task surface, put `// axial-note task-surface: <reason>` on the finding's line or the preceding line;
the finding stays visible as acknowledged Info, logged below the minimal console verbosity. `axial-allow-task-surface` is
not accepted.

Full resolves the builder's type, so an aliased builder (`let t = task`) is reported and a shadowing `task` value
of another type is not, and it follows type abbreviations and inferred return types. Fast matches the spellings
`task`/`backgroundTask` and annotated return types that name `Task`/`ValueTask` through the file's `open`s; it skips
names shadowed in the file and does not see aliases, abbreviations or unannotated return types. A file that opens
`FSharp.Control.Tasks` (Ply, TaskBuilder.fs) may rebind `task`: Full reports only FSharp.Core's builders, and Fast
does not claim `task` there. A binding such as `let run () = task { ... }` has one finding, the expression; its
inferred Task signature is not reported again. Both apply the same role and composition rules. `axial check` prints a per-project count of Fast's task-surface findings (and of all raw task
sites), recorded by the latest build, so the trend stays visible; it never gates.

raw-concurrency, swallowed-exn, cancel-none and dropped-work exempt the `test` role unless `AxialGuardrailsInTests=all` enables full test analysis. Host-role projects are exempt only when their
repository-relative project path matches that rule's `[exemptions]` list in `Axial.Guardrails.toml`.
The table accepts only `raw-concurrency`, `swallowed-exn`, `cancel-none`, and `dropped-work`, each an array of project-path globs.
Paths must stay inside the repository and match existing files. Other roles ignore these lists;
an ordinary host with no matching declaration is not exempt. For example:

```toml
[exemptions]
raw-concurrency = ["tools/**/*.fsproj"]
swallowed-exn = ["tools/**/*.fsproj"]
dropped-work = ["tools/**/*.fsproj"]
```

These lists do not change finding severities or exempt other rules.
raw-effect through escaped-scope accept rule-specific allowances (a raw-effect allowance names its gap or why) and keep their baseline behavior.
A raw-concurrency allowance covers one call and names its why or gap; swallowed-exn, cancel-none, dropped-work and guessed-wait accept no allowances, and none of them accept baseline severity downgrades. Only guide findings (swallowed-exn, awaited-delay guessed-wait
and task-surface) accept `axial-note`; no finding approval is needed. stale-allow warns about malformed, empty, unknown, misplaced, or stale notes,
and notes on block findings. Invalid notes become errors at checkpoints. No automatic fix is offered: choosing
ownership, cancellation, or exception reporting changes behavior.

The verification build and release workflow set `AxialGuardrailsCheckpoint=true`. Ordinary builds, including
ordinary Release builds, do not enable it automatically. At checkpoints, unacknowledged swallowed-exn and guessed-wait guide findings become errors (task-surface stays Info);
acknowledged findings remain Info with their reasons. Build output shows current findings; `axial policy review`
separately lists added notes and suppressions, changed reasons, and policy loosening since the reviewed commit.
Suppressions and notes stay active pending human review. `axial check` warns on policy drift; corrupt policy still
fails, as do analyzer errors. The lock is tamper-evident, not a security boundary; enforcement is the repository's
choice. Use `axial policy review --check` at a chosen release or merge point to reject unreviewed loosening.
