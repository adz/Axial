# FsFlow Capability Environment Design

## Status

Locked direction for the next development phase.

This document is the implementation reference for the CAPS backlog in
[`dev-docs/TASKS.md`](TASKS.md). The consumer-facing target shape is sketched in
[`CAPS_INTENDED_USER_GUIDE.md`](CAPS_INTENDED_USER_GUIDE.md), and the discarded alternatives are
kept under [`caps-research/`](caps-research/).

Use:

- `Needs<'dep>` as the fine-grained capability contract.
- `Env<'dep>` as the computation-expression request for a dependency.
- `Env<'dep, 'value>` as the projected dependency request.
- Named cap-set interfaces for meaningful dependency bundles.
- Interface default implementations to avoid repeating `Needs<'dep>` implementations on every container.
- Concrete runtime/container records that simply implement the relevant cap-set interface.
- Optional tooling later for diagnostics/suggestions, not core semantics.

Do **not** use SRTP as the main public model.
Do **not** rely on inferred anonymous/intersection capability sets.
Do **not** require helper modules for every dependency.

---

## Goals

The design should make this kind of flow pleasant:

```fsharp
let chooseTodo : TaskFlow<ChooseTodoCaps, TodoError, string option> =
    taskFlow {
        let! todos = Env<ITodoStore> _.Load

        if List.isEmpty todos then
            return None
        else
            let! index =
                Env<IRandom> (fun random ->
                    random.NextInt 0 todos.Length)

            return Some todos[index]
    }

And make the required capabilities explicit:

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

The user should see:

ChooseTodoCaps = TodoStore + Random

without needing to understand a complicated encoding.


---

Core Model

Fine-grained dependency requirement

type Needs<'dep> =
    abstract Dep : 'dep

This says:

An environment that satisfies Needs<IClock> can supply IClock.

It is intentionally generic and tiny.


---

Environment Requests

Get the dependency itself

[<Struct>]
type Env<'dep> = Env

Usage:

taskFlow {
    let! clock = Env<IClock>

    let now = clock.UtcNow()
    return now
}

This avoids awkward forms like:

Env.get<IClock> id

Get/project from the dependency

[<Struct>]
type Env<'dep, 'value> =
    | Env of ('dep -> 'value)

Usage:

taskFlow {
    let! now = Env<IClock> _.UtcNow
    let! todos = Env<ITodoStore> _.Load

    do! Env<ILogger> (fun log ->
        log.Info $"Loaded {todos.Length} todos")
}

The CE is responsible for:

1. Reading 'dep from the environment.


2. Applying the projection.


3. Auto-binding/lifting the result according to existing FsFlow rules.



So if _.Load returns Task<string list>, the CE binds it automatically.

There should not be separate Env.task, Env.async, Env.result, etc.


---

Computation Expression Bind Shape

Conceptual implementation only:

type TaskFlowBuilder with

    member inline _.Bind
        (
            Env : Env<'dep>,
            next : 'dep -> TaskFlow<'env, 'err, 'value>
        )
        : TaskFlow<'env, 'err, 'value>
        when 'env :> Needs<'dep> =
        // read dep from env
        // pass dep to next
        ...


    member inline _.Bind
        (
            Env project : Env<'dep, 'projected>,
            next : 'unwrapped -> TaskFlow<'env, 'err, 'value>
        )
        : TaskFlow<'env, 'err, 'value>
        when 'env :> Needs<'dep> =
        // read dep from env
        // let projected = project dep
        // auto-bind/lift projected
        // pass unwrapped value to next
        ...

Important: the projected form should reuse the existing auto-lift/bind machinery.


---

Primitive Capability Interfaces

Example dependencies:

type IClock =
    abstract UtcNow : unit -> DateTimeOffset

type ILogger =
    abstract Info : string -> unit

type IRandom =
    abstract NextInt : minInclusive:int -> maxExclusive:int -> int

type ITodoStore =
    abstract Load : unit -> Task<string list>
    abstract Save : string list -> Task<unit>

These are the actual services/effects.


---

Fine-Grained Cap Sets

Each fine-grained cap-set interface should:

1. Inherit Needs<'dep>.


2. Expose a strongly named abstract member.


3. Provide the Needs<'dep>.Dep implementation via default interface implementation.



Example:

type ClockCaps =

    inherit Needs<IClock>

    abstract Clock : IClock

    interface Needs<IClock> with
        member x.Dep = x.Clock

type LoggerCaps =

    inherit Needs<ILogger>

    abstract Logger : ILogger

    interface Needs<ILogger> with
        member x.Dep = x.Logger

type RandomCaps =

    inherit Needs<IRandom>

    abstract Random : IRandom

    interface Needs<IRandom> with
        member x.Dep = x.Random

type TodoStoreCaps =

    inherit Needs<ITodoStore>

    abstract TodoStore : ITodoStore

    interface Needs<ITodoStore> with
        member x.Dep = x.TodoStore

This means containers do not repeat:

interface Needs<IRandom> with
    member x.Dep = x.Random

They only provide the required members.


---

Composed Capability Sets

Capability sets compose by interface inheritance.

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

type TimedLoggingCaps =
    inherit ClockCaps
    inherit LoggerCaps

type TodoSubsystem =
    inherit ChooseTodoCaps
    inherit TimedLoggingCaps

This gives nominal, explicit composition.

There are no anonymous inferred intersection types in F#.

So instead of wanting:

Needs<ITodoStore> & Needs<IRandom>

we name it:

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

This is the accepted tradeoff.


---

Runtime Containers

A runtime container is just a record implementing a cap-set interface.

type AppRuntime =
    { Clock : IClock
      Logger : ILogger
      Random : IRandom
      TodoStore : ITodoStore }

    interface TodoSubsystem

Because TodoSubsystem inherits ClockCaps, LoggerCaps, RandomCaps, and TodoStoreCaps, this works if the record has the required members:

Clock : IClock
Logger : ILogger
Random : IRandom
TodoStore : ITodoStore

The mapping from named member to Needs<'dep>.Dep lives in the cap-set interface, not the container.

This is the main ergonomic win.


---

Test Containers

Tests can implement smaller cap sets.

type ChooseTodoTestRuntime =
    { Random : IRandom
      TodoStore : ITodoStore }

    interface ChooseTodoCaps

No repeated Needs<'dep> boilerplate.

Example:

let env =
    { Random = FakeRandom [ 1 ]
      TodoStore = FakeTodoStore [ "A"; "B"; "C" ] }

This satisfies:

TaskFlow<ChooseTodoCaps, TodoError, string option>

because ChooseTodoTestRuntime :> ChooseTodoCaps.


---

Full Example

open System
open System.Threading.Tasks

type Needs<'dep> =
    abstract Dep : 'dep

[<Struct>]
type Env<'dep> = Env

[<Struct>]
type Env<'dep, 'value> =
    | Env of ('dep -> 'value)

type IClock =
    abstract UtcNow : unit -> DateTimeOffset

type ILogger =
    abstract Info : string -> unit

type IRandom =
    abstract NextInt : minInclusive:int -> maxExclusive:int -> int

type ITodoStore =
    abstract Load : unit -> Task<string list>
    abstract Save : string list -> Task<unit>

type ClockCaps =
    inherit Needs<IClock>

    abstract Clock : IClock

    interface Needs<IClock> with
        member x.Dep = x.Clock

type LoggerCaps =
    inherit Needs<ILogger>

    abstract Logger : ILogger

    interface Needs<ILogger> with
        member x.Dep = x.Logger

type RandomCaps =
    inherit Needs<IRandom>

    abstract Random : IRandom

    interface Needs<IRandom> with
        member x.Dep = x.Random

type TodoStoreCaps =
    inherit Needs<ITodoStore>

    abstract TodoStore : ITodoStore

    interface Needs<ITodoStore> with
        member x.Dep = x.TodoStore

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

type TodoSubsystem =
    inherit ChooseTodoCaps
    inherit ClockCaps
    inherit LoggerCaps

type TodoError =
    | EmptyTodos
    | StoreUnavailable of string

let chooseTodo : TaskFlow<ChooseTodoCaps, TodoError, string option> =
    taskFlow {
        let! todos = Env<ITodoStore> _.Load

        if List.isEmpty todos then
            return None
        else
            let! index =
                Env<IRandom> (fun random ->
                    random.NextInt 0 todos.Length)

            return Some todos[index]
    }

let runTodoWorkflow : TaskFlow<TodoSubsystem, TodoError, unit> =
    taskFlow {
        let! now = Env<IClock> _.UtcNow

        do! Env<ILogger> (fun log ->
            log.Info $"Workflow started at {now}")

        let! selected = chooseTodo

        do! Env<ILogger> (fun log ->
            log.Info $"Selected todo: {selected}")
    }

type AppRuntime =
    { Clock : IClock
      Logger : ILogger
      Random : IRandom
      TodoStore : ITodoStore }

    interface TodoSubsystem

type ChooseTodoTestRuntime =
    { Random : IRandom
      TodoStore : ITodoStore }

    interface ChooseTodoCaps


---

Why Named Capability Sets Are Good Architecture

Named cap sets make dependencies part of the module/use-case boundary.

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

This says:

Choosing a todo requires todo storage and randomness.
It does not require logging, clock, filesystem, HTTP, config, or IServiceProvider.

Benefits:

1. Dependency creep is visible.


2. Tests provision smaller environments.


3. Module boundaries are explicit.


4. LLMs can understand requirements from the type.


5. App code avoids god-container leakage.


6. Subsystems become real architectural seams.



Recommended naming layers:

ClockCaps
RandomCaps
TodoStoreCaps

ChooseTodoCaps
TimedLoggingCaps

TodoSubsystem
BillingSubsystem
DeviceProtocolSubsystem

AppRuntime


---

Runtime / Domain / Context Split

Use separate capability layers.

BCL caps        wrappers over platform effects
Context caps    request/user/correlation/tenant data
Domain caps     application-specific services
Subsystem caps  use-case/module bundles
Runtime         concrete app/test provision

Example BCL caps:

type FileSystemCaps =
    inherit Needs<IFileSystem>

    abstract FileSystem : IFileSystem

    interface Needs<IFileSystem> with
        member x.Dep = x.FileSystem

Example context caps:

type UserContext =
    { UserId : string
      TenantId : string }

type UserContextCaps =
    inherit Needs<UserContext>

    abstract UserContext : UserContext

    interface Needs<UserContext> with
        member x.Dep = x.UserContext

Example domain cap:

type TodoStoreCaps =
    inherit Needs<ITodoStore>

    abstract TodoStore : ITodoStore

    interface Needs<ITodoStore> with
        member x.Dep = x.TodoStore

Example subsystem:

type TodoAuditCaps =
    inherit TodoStoreCaps
    inherit ClockCaps
    inherit UserContextCaps
    inherit LoggerCaps

Example runtime:

type AppRuntime =
    { Clock : IClock
      Logger : ILogger
      FileSystem : IFileSystem
      UserContext : UserContext
      TodoStore : ITodoStore }

    interface TodoAuditCaps


---

Composition Rules

Rule 1: fine-grained caps wrap one dependency

type ClockCaps =
    inherit Needs<IClock>

Rule 2: use-case caps compose fine-grained caps

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

Rule 3: subsystem caps compose use-case caps

type TodoSubsystem =
    inherit ChooseTodoCaps
    inherit TimedLoggingCaps

Rule 4: runtime records implement the largest needed cap set

type AppRuntime =
    { ... }

    interface TodoSubsystem

Rule 5: tests implement the smallest useful cap set

type ChooseTodoTestRuntime =
    { TodoStore : ITodoStore
      Random : IRandom }

    interface ChooseTodoCaps


---

Why Not SRTP?

SRTP can express member constraints:

let inline foo< ^env
    when ^env : (member Random : IRandom)>
    =
    ...

But public SRTP-heavy APIs are rejected for this design because:

1. Error messages are worse.


2. Hover types are noisy.


3. Nested CE errors become hard to read.


4. LLMs are worse at modifying them safely.


5. It does not produce ergonomic inferred intersection sets anyway.



SRTP may be used internally if useful, but should not be the main user-facing capability story.


---

Why Not IProvide<'T>?

IProvide<'T> reads from the container’s perspective.

But flow signatures are read from the flow’s perspective.

This is confusing:

TaskFlow<IProvide<IClock>, Err, unit>

Does the flow provide the clock, or require it?

Needs<'T> is clearer:

TaskFlow<Needs<IClock>, Err, unit>

It says:

This flow needs IClock.


---

Why Not IHasClock?

Per-dependency interfaces like this work:

type IHasClock =
    abstract Clock : IClock

But they don’t generalize as cleanly to Env<'dep> and generic CE binding.

The chosen design separates:

Needs<'dep>     generic machine-readable requirement
ClockCaps       human/domain-readable named cap

This gives both generic mechanics and readable architecture.


---

Why Not Fully Automatic Intersection Sets?

Desired but unavailable:

TaskFlow<Needs<ITodoStore> & Needs<IRandom>, Err, string>

F# does not have general intersection types.

Therefore the design uses explicit named sets:

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

Tooling may later infer/suggest these sets, but the core model remains nominal.


---

Optional Tooling

Later tooling can improve ergonomics without changing semantics.

Analyzer: missing cap

If a flow uses:

Env<IRandom>

but its declared cap type does not inherit RandomCaps or Needs<IRandom>, report:

Flow uses Env<IRandom>, but ChooseTodoCaps does not provide IRandom.
Add RandomCaps to ChooseTodoCaps.

Analyzer: unused cap

If a flow declares:

TaskFlow<ChooseTodoCaps, Err, _>

and ChooseTodoCaps includes ClockCaps, but no Env<IClock> is used, suggest:

ClockCaps appears unused in this flow.

Code action: create cap set

From usage:

Env<ITodoStore>
Env<IRandom>

Generate:

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

Code action: create test runtime

From:

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

Generate:

type ChooseTodoTestRuntime =
    { TodoStore : ITodoStore
      Random : IRandom }

    interface ChooseTodoCaps

Do not make tooling required for understanding the model.


---

Performance Notes

Env<'dep> should compile conceptually to:

(env :> Needs<'dep>).Dep

That is:

interface cast + property read

Projected form adds:

lambda call + normal CE auto-bind/lift

Guidance:

// Fine
let! now = Env<IClock> _.UtcNow

// Prefer this in tight loops
let! clock = Env<IClock>

for item in items do
    let now = clock.UtcNow()
    ...

Use [<Struct>] for request marker types to avoid unnecessary allocation.


---

LLM-Friendliness

This design is LLM-friendly because:

1. The core vocabulary is small:

Needs<'T>

Env<T>

Caps

Runtime



2. Cap sets are explicit:

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps


3. Runtime records are plain data:

type AppRuntime =
    { Clock : IClock
      Random : IRandom }
    interface SomeCaps


4. There is no hidden SRTP maze.


5. Generated or suggested code has obvious expansions.



For local LLM tasks, provide the model with these rules:

When adding a dependency:
1. Define the dependency interface.
2. Define a fine-grained Caps interface with default Needs<T> implementation.
3. Add that Caps interface to the relevant named use-case/subsystem cap set.
4. Add a field/member to the relevant runtime record.
5. Use Env<T> or Env<T, U> inside taskFlow.
6. Prefer small cap sets for unit-testable functions.


---

Example: Adding a New Cap

Suppose we add randomness.

1. Define dependency

type IRandom =
    abstract NextInt : minInclusive:int -> maxExclusive:int -> int

Needed because flows should not call System.Random.Shared directly if we want testability and deterministic replay.

2. Define fine-grained cap

type RandomCaps =
    inherit Needs<IRandom>

    abstract Random : IRandom

    interface Needs<IRandom> with
        member x.Dep = x.Random

Needed once so containers do not each implement Needs<IRandom> manually.

3. Add to relevant use-case cap set

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

Needed because chooseTodo uses both storage and randomness.

4. Add to runtime

type AppRuntime =
    { TodoStore : ITodoStore
      Random : IRandom }

    interface ChooseTodoCaps

Needed because the app must provide concrete implementations.

5. Use in flow

let chooseTodo : TaskFlow<ChooseTodoCaps, TodoError, string option> =
    taskFlow {
        let! todos = Env<ITodoStore> _.Load

        if List.isEmpty todos then
            return None
        else
            let! index =
                Env<IRandom> (fun random ->
                    random.NextInt 0 todos.Length)

            return Some todos[index]
    }

Needed because the flow wants deterministic, statically checked access to randomness.

6. Test with small runtime

type ChooseTodoTestRuntime =
    { TodoStore : ITodoStore
      Random : IRandom }

    interface ChooseTodoCaps

Needed because tests should not construct the whole app runtime.


---

Final Recommendation

Adopt this as the primary capability design.

Canonical user-facing pattern:

type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

let chooseTodo : TaskFlow<ChooseTodoCaps, TodoError, string option> =
    taskFlow {
        let! todos = Env<ITodoStore> _.Load
        let! index = Env<IRandom> (fun r -> r.NextInt 0 todos.Length)
        return List.tryItem index todos
    }

Canonical runtime pattern:

type ChooseTodoRuntime =
    { TodoStore : ITodoStore
      Random : IRandom }

    interface ChooseTodoCaps

The design is explicit, nominal, testable, LLM-readable, and avoids the worst boilerplate by moving Needs<'T> implementation into default interface members on cap-set interfaces.
Addendum: Using # Flexible Types
F# flexible types (#Type) work well with the cap-set approach and are recommended for many public APIs.
Example:
type LoginCaps =
    inherit UserStoreCaps
    inherit ClockCaps
    inherit LoggerCaps

Instead of:
let login : TaskFlow<LoginCaps, LoginError, Session>

you can write:
let login : TaskFlow<#LoginCaps, LoginError, Session>

This means:
Any environment implementing LoginCaps is accepted.

This is often preferable because callers may provide:
a larger application runtime
a subsystem runtime
a test runtime
a benchmark runtime
without needing an exact environment type match.
Example:
type AppRuntime =
    { Clock : IClock
      Logger : ILogger
      UserStore : IUserStore
      FileSystem : IFileSystem
      Metrics : IMetrics }

    interface LoginCaps

This runtime satisfies:
TaskFlow<#LoginCaps, LoginError, Session>

even though it contains additional capabilities.

Recommended Usage
For public flows and module boundaries, prefer:
let login : TaskFlow<#LoginCaps, LoginError, Session>

rather than:
let login : TaskFlow<LoginCaps, LoginError, Session>

because the flexible form better communicates:
This flow requires LoginCaps,
but callers may provide larger environments.


Why Not Use # Everywhere?
# helps with subtype flexibility, but it does not replace named cap sets.
F# still does not support ergonomic intersection types like:
#ClockCaps & #LoggerCaps

So this is still invalid:
TaskFlow<#ClockCaps & #LoggerCaps, Err, unit>

You must still either:
Define a composed cap set:
type LoggingCaps =
    inherit ClockCaps
    inherit LoggerCaps

or:
Use generic constraints:
when 'env :> ClockCaps
and  'env :> LoggerCaps

The named cap-set approach remains the preferred primary model.

Recommended Style
Small/private helpers
Small helpers may use generic constraints directly:
let inline randomBool<
    'env
    when 'env :> RandomCaps>
    : TaskFlow<'env, Err, bool> =
    taskFlow {
        let! value =
            Env<IRandom> (fun r ->
                r.NextInt 0 2)

        return value = 0
    }

This avoids creating unnecessary named cap sets.

Public/use-case flows
Public workflows should usually define named capability sets:
type LoginCaps =
    inherit UserStoreCaps
    inherit ClockCaps
    inherit LoggerCaps

and expose flexible env usage:
let login : TaskFlow<#LoginCaps, LoginError, Session>

This gives:
clearer architecture
reusable dependency groups
cleaner type signatures
better error messages
improved documentation
improved LLM readability
while still allowing callers to provide larger runtimes.

Rule of Thumb
Use named cap sets for meaningful boundaries.
Use #CapName to allow larger runtimes to satisfy them.
Use generic constraints sparingly for tiny local helpers.

