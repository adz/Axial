# Elmish programs

The `Axial.Elmish` package exposes the `Axial.ElmishFlows` namespace and connects an Elmish 5 program to Axial
workflows. Create one runtime per program, passing an environment and a `Post` function that delivers callbacks on the
host's dispatch thread. When already on that thread, `Post` should run inline; otherwise it should post to it. Do not
call Elmish's dispatch directly from workers. Connect `OnDefect` to the host's crash reporting path; typed failures
are messages, but defects are not.

## First-use lifecycle

`ElmishRuntime` requires an environment implementing `IHasClock`. Keep the runtime's clock environment stable, and
supply a command's current dependencies with `Flow.localEnv`. For example, a git client can open another repository
without recreating its Elmish runtime:

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

`Flow.localEnv` selects the environment when the command runs; capture the selected environment for each command when
constructing it so later repository switches do not change already-issued requests.

## Operation reference

The exhaustive operation inventory lives in the API reference:

- [Cmd](/api/Axial.ElmishFlows.Cmd.html): `Cmd.OfFlow.either`/`perform`/`attempt` and the latest-wins
  `Cmd.OfFlow.Latest` variants.
- [Sub](/api/Axial.ElmishFlows.Sub.html): `Sub.OfFlowStream.subscribe` for running a stream under a subscription id.
- [Inbox](/api/Axial.ElmishFlows.Inbox.html): `Inbox.create`/`offer`/`post`/`dropped`/`stream` for synchronous UI
  input and search-as-you-type.
- [ElmishRuntime](/api/Axial.ElmishFlows.ElmishRuntime.html): `start`, `test`, and the `Stop`/`StopAndWait` shutdown
  methods.
- [TestRuntime](/api/Axial.ElmishFlows.TestRuntime.html): `run`, `settle`, `drain`, `dispatched`, and `defects` for
  tests with a manual clock.
