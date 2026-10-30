# Elmish programs

The `Axial.ElmishFlows` package exposes the `Axial.ElmishFlows` namespace and connects an Elmish 5 program to Axial
workflows. Create one runtime per program, passing an environment and a `Post` function that delivers callbacks on the
host's dispatch thread. When already on that thread, `Post` should run inline; otherwise it should post to it. Do not
call Elmish's dispatch directly from workers. Connect `OnDefect` to the host's crash reporting path; typed failures
are messages, but defects are not.

## Install

```bash
dotnet add package Axial.ElmishFlows
```

`Axial.ElmishFlows` brings Elmish 5 with it. Reference Elmish directly when your program uses `Program.mkProgram` and
`dispatch`. Open `Axial.ElmishFlows` for the Axial API, and `Axial.PlatformService` when you override the runtime clock.

A minimal runtime supplies an environment, a `Post` function, a defect handler, a history capacity, and an optional
clock (`Clock = None` keeps the runtime's live default; `Some clock` installs that clock for commands, subscriptions
and layer provisioning):

```fsharp no-check reason="The Elmish package is not referenced by the documentation example compiler; the same API is compiled in Axial.ElmishFlows.Tests"
open Axial
open Axial.ElmishFlows
open Axial.PlatformService

// In an Elmish host, `Post` is usually the program's dispatch, so callbacks run on the host's dispatch thread.
// Here the code is already on that thread, so it runs the callback inline.
let runtime =
    ElmishRuntime.start
        { Post = (fun callback -> callback ())
          OnDefect = (fun defect -> failwith defect.Rendered)
          HistoryCapacity = 32
          Clock = None }
        ()
```

When `Post` is not already on the dispatch thread it must post to it (for example `Application.Post` in WPF or
dispatching back to a UI loop); never call Elmish's dispatch directly from a worker.

## First-use lifecycle

`ElmishRuntime` takes any environment, with no clock interface required: time comes from the runtime, and
`ElmishRuntimeOptions.Clock` overrides it. Keep the runtime's environment stable, and
supply a command's current dependencies with `Flow.localEnv`. For example, a git client can open another repository
without recreating its Elmish runtime:

```fsharp no-check reason="The Elmish package is not referenced by the documentation example compiler; the same API is compiled in Axial.ElmishFlows.Tests"
open Axial
open Axial.ElmishFlows
open Axial.PlatformService

type RepositoryEnvironment = { Repository: string }

let runtime =
    ElmishRuntime.start
        { Post = (fun callback -> callback ())
          OnDefect = (fun defect -> failwith defect.Rendered)
          HistoryCapacity = 32
          Clock = None }
        ()
let currentEnv = { Repository = "/repos/new" }
let repositoryName : Flow<RepositoryEnvironment, string, string> =
    Flow.envWith (fun env -> env.Repository)

let command =
    Cmd.OfFlow.either "repository" runtime
        (repositoryName |> Flow.localEnv (fun _ -> currentEnv))
        id
        (fun _ -> "unavailable")
```

`Flow.localEnv` selects the environment when the command runs; capture the selected environment for each command when
constructing it so later repository switches do not change already-issued requests.

## Provisioning with startLayer

When a runtime needs resources that must live for the whole program — a `Cache.make*` cache, queues, hubs, or a
`Layer.fromFlow` resource — build them with a layer and start the runtime with `ElmishRuntime.startLayer`. The layer
is provisioned inside the runtime's root scope, so `Stop`/`StopAndWait` releases what the layer acquired. A typed
provisioning failure returns `Error` and leaves nothing running.

```fsharp no-check reason="The Elmish package is not referenced by the documentation example compiler; the same API is compiled in Axial.ElmishFlows.Tests"
open Axial
open Axial.ElmishFlows
open Axial.PlatformService

type CacheEnvironment = { Cache: Cache<int, Never, string> }

let layer : Layer<unit, string, CacheEnvironment> =
    Layer.fromFlow (flow {
        let! cache =
            Cache.make (fun key -> Flow.succeed $"value-{key}" : Flow<unit, Never, string>)
        return { Cache = cache }
    })

let runtime =
    match
        ElmishRuntime.startLayer
            { Post = (fun callback -> callback ())
              OnDefect = (fun defect -> failwith defect.Rendered)
              HistoryCapacity = 32
              Clock = None }
            layer
            ()
        |> Async.RunSynchronously
    with
    | Ok runtime -> runtime
    | Error error -> failwith error
```

## Long-lived services and fire-and-forget

A service that lives as long as the program — a search pipeline, a poller, a cache warmer — belongs in a stream
subscription or in a service the runtime provisions, not in a never-completing command. `Cmd.OfFlow.perform` needs a
success message, so running `Flow.never` through it forces an unreachable `onSuccess`; use
`Sub.OfFlowStream.subscribe` instead, and let Elmish dispose the subscription
(interrupting the stream and releasing its resources) when the program unmounts:

```fsharp no-check reason="The Elmish package is not referenced by the documentation example compiler; the same API is compiled in Axial.ElmishFlows.Tests"
open Axial
open Axial.ElmishFlows
open Axial.PlatformService

// A search pipeline backed by an inbox: post queries, subscribe to results for the program's lifetime.
let searchPipeline (runtime: ElmishRuntime<unit>) =
    let inbox = Inbox.create 8 runtime
    let queries : FlowStream<unit, string, string> =
        Inbox.stream inbox |> FlowStream.map (fun query -> $"result:{query}")
    inbox, Sub.OfFlowStream.subscribe [ "search" ] runtime queries (fun error -> $"search-error:{error}")
```

When the service also needs resources provisioned once for the whole program, `ElmishRuntime.startLayer`
above is the alternative: it builds the cache, queue, or hub inside the runtime's root scope and releases it on
`Stop`/`StopAndWait`.

`Cmd.OfFlow.start` is the right tool for fire-and-forget work, but it accepts only `Flow<'env, Never, unit>`. A flow
with a typed failure channel must handle that failure first — log it, then recover to `unit`:

```fsharp prepare
open System
open Axial
open Axial.PlatformService

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let indexDocument (query: string) : Flow<unit, string, unit> =
    flow {
        if String.IsNullOrWhiteSpace query then
            return! Flow.fail "empty query"
    }

// Handle the typed failure before Cmd.OfFlow.start, which accepts only Flow<'env, Never, unit>.
let indexSafely (query: string) : Flow<unit, Never, unit> =
    indexDocument query
    |> Flow.recover (fun error ->
        flow {
            do! Log.error $"indexing skipped: {error}"
        })
```

```fsharp run
let log = Log.memory ()

indexSafely "" |> Flow.withLogSink log |> Flow.run () |> shouldEqual (Exit.Success ())

log.Entries |> Seq.map _.Message |> Seq.toList |> shouldEqual [ "indexing skipped: empty query" ]
```

```fsharp no-check reason="The Elmish package is not referenced by the documentation example compiler; the same API is compiled in Axial.ElmishFlows.Tests"
open Axial
open Axial.ElmishFlows

// indexSafely already handled its typed failure, so its type is Flow<'env, Never, unit>.
let backgroundIndex runtime query =
    Cmd.OfFlow.start "index" runtime (indexSafely query)
```

## Command parameter order

Axial's commands keep Elmish's "work first" idea, but the runtime owns the command's root scope, so it precedes the
flow. Pass the flow positionally, not piped:

```fsharp no-check reason="The Elmish package is not referenced by the documentation example compiler; the same API is compiled in Axial.ElmishFlows.Tests"
let command =
    Cmd.OfFlow.either "increment" runtime
        (Flow.succeed 2)
        id
        (fun (_: string) -> 0)
```

The order is `name runtime workflow onSuccess onError` for `either`; `perform` drops `onError`, `attempt` drops
`onSuccess`, and `Cmd.OfFlow.start name runtime workflow` is fire-and-forget: it runs a
`Flow<'env, Never, unit>` as a named fiber in the root scope, dispatches nothing, and reports defects to `OnDefect`.

## Operation reference

The exhaustive operation inventory lives in the API reference:

- [Cmd](/api/Axial.ElmishFlows.Cmd.html): `Cmd.OfFlow.either`/`perform`/`attempt`/`start` and the latest-wins
  `Cmd.OfFlow.Latest` variants.
- [Sub](/api/Axial.ElmishFlows.Sub.html): `Sub.OfFlowStream.subscribe` for running a stream under a subscription id.
- [Inbox](/api/Axial.ElmishFlows.Inbox.html): `Inbox.create`/`offer`/`post`/`dropped`/`stream` for synchronous UI
  input and search-as-you-type.
- [ElmishRuntime](/api/Axial.ElmishFlows.ElmishRuntime.html): `start`, `startLayer`, `test`, and the
  `Stop`/`StopAndWait` shutdown methods.
- [TestRuntime](/api/Axial.ElmishFlows.TestRuntime.html): `run`, `settle`, `drain`, `dispatched`, and `defects` for
  tests with a manual clock.
