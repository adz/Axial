# Elmish programs

The `Axial.Elmish` package exposes the `Axial.ElmishFlows` namespace and connects an Elmish 5 program to Axial workflows. Create one runtime per program, passing an environment and a `Post` function that delivers callbacks on the host's dispatch thread. When already on that thread, `Post` should run inline; otherwise it should post to it. Do not call Elmish's dispatch directly from workers. Connect `OnDefect` to the host's crash reporting path; typed failures are messages, but defects are not.

## Commands

`Cmd.OfFlow.either name runtime workflow onSuccess onError` dispatches a success or typed failure as a message. `perform` is for workflows without typed failures; `attempt` handles typed failures of unit-valued workflows. Each command runs in the runtime's scope, off the dispatch thread. Interruptions send no message.

Use `Cmd.OfFlow.Latest.either key runtime workflow onSuccess onError` for latest-wins requests. Starting another command with the same key interrupts the previous one and suppresses its queued delivery. Keys belong to the runtime, not to the process. `Cmd.OfFlow.Latest.cancel key runtime` invalidates a pending request.

## Per-command environments

`ElmishRuntime` requires an environment implementing `IHasClock`. Keep the runtime's clock environment stable, and supply a command's current dependencies with `Flow.localEnv`. For example, a git client can open another repository without recreating its Elmish runtime:

```fsharp no-check reason="The Elmish package is not referenced by the documentation example compiler; the same API is compiled in Axial.Elmish.Tests"
open Axial
open Axial.ElmishFlows
open Axial.PlatformService

type RepositoryEnvironment = { Clock: IClock; Repository: string } with
    interface IHasClock with
        member this.Clock = this.Clock

let runtime =
    ElmishRuntime.start
        { Post = (fun callback -> callback ())
          OnDefect = (fun defect -> failwith defect.Rendered)
          HistoryCapacity = 32 }
        (ClockEnvironment Clock.live)
let currentEnv = { Clock = Clock.live; Repository = "/repos/new" }
let repositoryName : Flow<RepositoryEnvironment, string, string> =
    Flow.envWith (fun env -> env.Repository)

let command =
    Cmd.OfFlow.either "repository" runtime
        (repositoryName |> Flow.localEnv (fun _ -> currentEnv))
        id
        (fun _ -> "unavailable")
```

`Flow.localEnv` selects the environment when the command runs; capture the selected environment for each command when constructing it so later repository switches do not change already-issued requests.

## Streams and inputs

`Sub.OfFlowStream.subscribe id runtime stream onError` runs a stream while Elmish keeps the subscription id. Removing the id disposes it, interrupts its fiber, and runs scoped finalizers. Delivery still passes through `Post`.

`Inbox.create capacity runtime` creates a dropping queue for synchronous UI input. `Inbox.offer value inbox` is an Elmish command; `Inbox.post value inbox` offers immediately and returns whether it was accepted. Inspect `Inbox.dropped inbox` when input can outpace the consumer. Pipe `Inbox.stream inbox` through `FlowStream.debounce` and `FlowStream.switchMapFlow` for search-as-you-type, then subscribe to the resulting stream. The inbox shuts down with the runtime.

## Shutdown and tests

Call `runtime.StopAndWait timeout` at host shutdown. It interrupts commands and subscriptions, waits for their finalizers, and returns `false` on timeout; it is safe from a single-threaded UI context. `Stop()` is the asynchronous alternative.

`ElmishRuntime.test env` creates a test runtime with queued delivery. Use `TestRuntime.run command test`, `TestRuntime.settle timeout test`, and `TestRuntime.drain test` to process callbacks; inspect `TestRuntime.dispatched test` and `TestRuntime.defects test`. A manual clock in `env` controls workflow time independently of the wall-clock settle timeout.
