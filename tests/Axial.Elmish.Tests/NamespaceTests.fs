namespace Axial.Elmish.Tests

open Axial
open Elmish
open Axial.ElmishFlows
open Axial.PlatformService
open Xunit

module NamespaceTests =
    type RepositoryEnvironment = { Clock: IClock; Repository: string } with
        interface IHasClock with
            member this.Clock = this.Clock

    [<Fact>]
    let ``Elmish and Axial command modules coexist`` () =
        use runtime =
            ElmishRuntime.start
                { Post = (fun callback -> callback ()); OnDefect = (fun defect -> failwith defect.Rendered); HistoryCapacity = 32 }
                (ClockEnvironment Clock.live)
        let program =
            Elmish.Program.mkProgram
                (fun () -> 0, Cmd.ofMsg 1)
                (fun message model ->
                    model + message,
                    Cmd.OfFlow.either "increment" runtime (Flow.succeed 2) id (fun (_: string) -> 0))
                (fun _ _ -> ())
        Assert.NotNull(box program)

    [<Fact>]
    let ``Elmish Sub module coexists with Axial Sub module`` () =
        use runtime =
            ElmishRuntime.start
                { Post = (fun callback -> callback ()); OnDefect = (fun defect -> failwith defect.Rendered); HistoryCapacity = 32 }
                (ClockEnvironment Clock.live)
        let source : FlowStream<ClockEnvironment, string, int> = FlowStream.empty
        let ax = Sub.OfFlowStream.subscribe ["values"] runtime source (fun (_: string) -> 0)
        let none = Sub.none
        let batch = Sub.batch [ ax; none ]
        Assert.Empty(none)
        Assert.Equal(1, List.length batch)

    [<Fact>]
    let ``per-command environments override the runtime clock environment`` () =
        use runtime =
            ElmishRuntime.start
                { Post = (fun callback -> callback ()); OnDefect = (fun defect -> failwith defect.Rendered); HistoryCapacity = 32 }
                (ClockEnvironment Clock.live)
        let currentEnv = { Clock = Clock.live; Repository = "/repos/new" }
        let repositoryName : Flow<RepositoryEnvironment, string, string> =
            Flow.envWith (fun env -> env.Repository)
        let command =
            Cmd.OfFlow.either "repository" runtime
                (repositoryName |> Flow.localEnv (fun _ -> currentEnv))
                id
                (fun _ -> "unavailable")
        Assert.NotNull(box command)
