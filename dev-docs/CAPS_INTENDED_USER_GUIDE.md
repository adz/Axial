# Using FsFlow Caps

Draft target guide for the CAPS implementation phase.

When this becomes user-facing documentation, move it under `docs/`, keep the code examples aligned
with the implemented API, and follow [`dev-docs/DOCS.md`](DOCS.md).

FsFlow caps let a flow say what it needs without passing every dependency as an argument.

You normally do not define caps yourself. FsFlow and your application libraries provide them.

You just compose the caps your flow needs, then use `Env<T>` inside the flow.

## A small example

```fsharp
type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

let chooseTodo : TaskFlow<ChooseTodoCaps, TodoError, string option> =
    taskFlow {
        let! todos = Env<ITodoStore> _.Load

        if List.isEmpty todos then
            return None
        else
            let! index = Env<IRandom> (fun r -> r.NextInt 0 todos.Length)
            return Some todos[index]
    }
```

This says:

```text
chooseTodo needs:
- a todo store
- randomness
```

It does not need logging, a clock, config, HTTP, files, or the full app runtime.

## Why this is useful

Without caps, workflows often drift toward one of two extremes.

Either every dependency is passed manually:

```fsharp
let chooseTodo random todoStore = ...
```

which becomes noisy as workflows grow.

Or everything receives the full app runtime:

```fsharp
let chooseTodo runtime = ...
```

which means the function can secretly use anything.

Caps give you the middle ground:

```fsharp
TaskFlow<ChooseTodoCaps, TodoError, string option>
```

The type tells you the dependency boundary.

## Using a dependency

Use `Env<T>` to ask the flow environment for a dependency.

```fsharp
taskFlow {
    let! clock = Env<IClock>
    let now = clock.UtcNow()
}
```

When you only need one value or operation, project directly:

```fsharp
taskFlow {
    let! now = Env<IClock> _.UtcNow
}
```

If the operation returns `Task`, `Async`, `Result`, or another supported value, FsFlow binds it normally:

```fsharp
taskFlow {
    let! text = Env<IFileSystem> (fun fs ->
        fs.ReadAllText "todo.txt")
}
```

## Composing caps

Caps are normal F# interfaces that compose by inheritance.

```fsharp
type ImportTodosCaps =
    inherit FileSystemCaps
    inherit TodoStoreCaps
    inherit LoggerCaps
```

Now the flow can use exactly those dependencies:

```fsharp
let importTodos : TaskFlow<ImportTodosCaps, ImportError, unit> =
    taskFlow {
        let! text = Env<IFileSystem> (fun fs ->
            fs.ReadAllText "todos.txt")

        do! Env<ILogger> (fun log ->
            log.Info "Loaded todos file")

        do! Env<ITodoStore> (fun store ->
            store.SaveText text)
    }
```

## Running a flow

At the edge of the app, provide a runtime record that implements the cap set.

```fsharp
type AppRuntime =
    { Clock : IClock
      Logger : ILogger
      FileSystem : IFileSystem
      TodoStore : ITodoStore
      Random : IRandom }

    interface ImportTodosCaps
    interface ChooseTodoCaps
```

Then run:

```fsharp
importTodos
|> TaskFlow.run appRuntime
```

The runtime can be larger than the flow’s caps. A runtime that implements `AppCaps` can still satisfy smaller cap sets if it implements them.

## Testing

For tests, provide only the caps the flow needs.

```fsharp
type ChooseTodoTestRuntime =
    { TodoStore : ITodoStore
      Random : IRandom }

    interface ChooseTodoCaps
```

Then:

```fsharp
let runtime =
    { TodoStore = FakeTodoStore [ "A"; "B"; "C" ]
      Random = FakeRandom [ 1 ] }

let result =
    chooseTodo
    |> TaskFlow.run runtime
```

The test does not need to construct logging, file system, HTTP clients, config, or the whole application runtime.

## When to create a named cap set

Create a named cap set when the combination means something.

Good:

```fsharp
type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps
```

```fsharp
type ImportTodosCaps =
    inherit FileSystemCaps
    inherit TodoStoreCaps
    inherit LoggerCaps
```

Less useful:

```fsharp
type ClockAndRandomAndLoggerCaps =
    inherit ClockCaps
    inherit RandomCaps
    inherit LoggerCaps
```

Prefer names that describe the use-case or module boundary.

## Common library caps

FsFlow libraries may provide caps such as:

```fsharp
ClockCaps
LoggerCaps
RandomCaps
GuidCaps
FileSystemCaps
EnvironmentCaps
HttpClientCaps
CancellationCaps
```

Application libraries may provide domain caps such as:

```fsharp
TodoStoreCaps
UserStoreCaps
PaymentGatewayCaps
DeviceGatewayCaps
```

You usually compose these. You do not usually write them yourself.

## Writing your own caps

Only write a new cap when you introduce a new dependency type.

For example, an application-specific dependency:

```fsharp
type IUserStore =
    abstract FindByEmail : string -> Task<User option>
```

Define the cap once:

```fsharp
type UserStoreCaps =
    inherit Needs<IUserStore>

    abstract UserStore : IUserStore

    interface Needs<IUserStore> with
        member x.Dep = x.UserStore
```

Then users just compose it:

```fsharp
type LoginCaps =
    inherit UserStoreCaps
    inherit ClockCaps
    inherit LoggerCaps
```

And use it:

```fsharp
let login : TaskFlow<LoginCaps, LoginError, Session> =
    taskFlow {
        let! user =
            Env<IUserStore> (fun users ->
                users.FindByEmail "ada@example.com")

        let! now = Env<IClock> _.UtcNow

        do! Env<ILogger> (fun log ->
            log.Info $"Login at {now}")

        return ...
    }
```

## The rule of thumb

Use the smallest meaningful cap set.

```fsharp
// Good
let chooseTodo : TaskFlow<ChooseTodoCaps, TodoError, string option>

// Too broad for most code
let chooseTodo : TaskFlow<AppRuntime, TodoError, string option>
```

Caps are useful because they make this visible:

```text
This flow needs these effects, and no others.
```

That makes code easier to test, easier to review, and easier to change.
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

