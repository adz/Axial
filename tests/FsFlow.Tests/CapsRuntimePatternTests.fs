namespace FsFlow.Tests

open System
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module CapsRuntimePatternTests =
    type IClock =
        abstract UtcNow : unit -> DateTimeOffset

    type ILogger =
        abstract Log : string -> unit

    type IRandom =
        abstract NextInt : minInclusive: int -> maxExclusive: int -> int

    type ITodoStore =
        abstract Todos : string list

    type FixedClock(now: DateTimeOffset) =
        interface IClock with
            member _.UtcNow() = now

    type RecordingLogger() =
        let messages = ResizeArray<string>()

        member _.Messages = messages |> Seq.toList

        interface ILogger with
            member _.Log(message: string) = messages.Add message

    type FixedRandom(index: int) =
        interface IRandom with
            member _.NextInt minInclusive maxExclusive = index

    type InMemoryTodoStore(todos: string list) =
        interface ITodoStore with
            member _.Todos = todos

    type ClockCaps =
        interface
            inherit Needs<IClock>
            abstract Clock : IClock
        end

    type LoggerCaps =
        interface
            inherit Needs<ILogger>
            abstract Logger : ILogger
        end

    type RandomCaps =
        interface
            inherit Needs<IRandom>
            abstract Random : IRandom
        end

    type TodoStoreCaps =
        interface
            inherit Needs<ITodoStore>
            abstract TodoStore : ITodoStore
        end

    type ChooseTodoCaps =
        interface
            inherit Needs<IRandom>
            inherit Needs<ITodoStore>
            abstract Random : IRandom
            abstract TodoStore : ITodoStore
        end

    type AppCaps =
        interface
            inherit ChooseTodoCaps
            inherit Needs<IClock>
            inherit Needs<ILogger>
            abstract Clock : IClock
            abstract Logger : ILogger
        end

    type ChooseTodoTestRuntime =
        { RandomService: IRandom
          TodoStoreService: ITodoStore }
        with
            interface ChooseTodoCaps with
                member x.Random = x.RandomService
                member x.TodoStore = x.TodoStoreService

            interface Needs<IRandom> with
                member x.Dep = x.RandomService

            interface Needs<ITodoStore> with
                member x.Dep = x.TodoStoreService

    type AppRuntime =
        { ClockService: IClock
          LoggerService: ILogger
          RandomService: IRandom
          TodoStoreService: ITodoStore }
        with
            interface AppCaps with
                member x.Clock = x.ClockService
                member x.Logger = x.LoggerService
                member x.Random = x.RandomService
                member x.TodoStore = x.TodoStoreService

            interface Needs<IClock> with
                member x.Dep = x.ClockService

            interface Needs<ILogger> with
                member x.Dep = x.LoggerService

            interface Needs<IRandom> with
                member x.Dep = x.RandomService

            interface Needs<ITodoStore> with
                member x.Dep = x.TodoStoreService

    type TodoError =
        | EmptyTodoList

    [<Fact>]
    let ``fine-grained caps expose their dependencies through Needs`` () =
        let clock = FixedClock(DateTimeOffset(2026, 5, 9, 12, 30, 0, TimeSpan.Zero))
        let logger = RecordingLogger()
        let random = FixedRandom 1
        let todoStore = InMemoryTodoStore [ "alpha"; "beta"; "gamma" ]

        let appRuntime =
            { ClockService = clock :> IClock
              LoggerService = logger :> ILogger
              RandomService = random :> IRandom
              TodoStoreService = todoStore :> ITodoStore }

        let chooseTodoRuntime =
            { RandomService = random :> IRandom
              TodoStoreService = todoStore :> ITodoStore }

        let clockNeeds = appRuntime :> Needs<IClock>
        let loggerNeeds = appRuntime :> Needs<ILogger>
        let randomNeeds = appRuntime :> Needs<IRandom>
        let todoStoreNeeds = appRuntime :> Needs<ITodoStore>
        let testRandomNeeds = chooseTodoRuntime :> Needs<IRandom>
        let testTodoStoreNeeds = chooseTodoRuntime :> Needs<ITodoStore>

        test <@ obj.ReferenceEquals(box clockNeeds.Dep, box clock) @>
        test <@ obj.ReferenceEquals(box loggerNeeds.Dep, box logger) @>
        test <@ obj.ReferenceEquals(box randomNeeds.Dep, box random) @>
        test <@ obj.ReferenceEquals(box todoStoreNeeds.Dep, box todoStore) @>
        test <@ obj.ReferenceEquals(box testRandomNeeds.Dep, box random) @>
        test <@ obj.ReferenceEquals(box testTodoStoreNeeds.Dep, box todoStore) @>

    [<Fact>]
    let ``named cap-set flows run on both larger app runtimes and smaller test runtimes`` () =
        let clock = FixedClock(DateTimeOffset(2026, 5, 9, 12, 30, 0, TimeSpan.Zero))
        let logger = RecordingLogger()
        let random = FixedRandom 1
        let todoStore = InMemoryTodoStore [ "alpha"; "beta"; "gamma" ]

        let appRuntime =
            { ClockService = clock :> IClock
              LoggerService = logger :> ILogger
              RandomService = random :> IRandom
              TodoStoreService = todoStore :> ITodoStore }

        let chooseTodoRuntime =
            { RandomService = random :> IRandom
              TodoStoreService = todoStore :> ITodoStore }

        let chooseTodoFlowForApp : TaskFlow<AppRuntime, TodoError, string option> =
            Flow.read (fun (runtime: AppRuntime) ->
                let todos = (runtime :> Needs<ITodoStore>).Dep.Todos

                match todos with
                | [] -> None
                | _ ->
                    let index = (runtime :> Needs<IRandom>).Dep.NextInt 0 todos.Length
                    Some todos[index])
            |> TaskFlow.fromFlow

        let chooseTodoFlowForTest : TaskFlow<ChooseTodoTestRuntime, TodoError, string option> =
            Flow.read (fun (runtime: ChooseTodoTestRuntime) ->
                let todos = (runtime :> Needs<ITodoStore>).Dep.Todos

                match todos with
                | [] -> None
                | _ ->
                    let index = (runtime :> Needs<IRandom>).Dep.NextInt 0 todos.Length
                    Some todos[index])
            |> TaskFlow.fromFlow

        let appResult =
            TaskFlow.run appRuntime CancellationToken.None chooseTodoFlowForApp
            |> fun task -> task.GetAwaiter().GetResult()

        let testResult =
            TaskFlow.run chooseTodoRuntime CancellationToken.None chooseTodoFlowForTest
            |> fun task -> task.GetAwaiter().GetResult()

        test <@ appResult = Exit.Success (Some "beta") @>
        test <@ testResult = Exit.Success (Some "beta") @>

    [<Fact>]
    let ``flexible type boundaries keep the app runtime story small and explicit`` () =
        let clock = FixedClock(DateTimeOffset(2026, 5, 9, 12, 30, 0, TimeSpan.Zero))
        let logger = RecordingLogger()
        let random = FixedRandom 1
        let todoStore = InMemoryTodoStore [ "alpha"; "beta"; "gamma" ]

        let appRuntime =
            { ClockService = clock :> IClock
              LoggerService = logger :> ILogger
              RandomService = random :> IRandom
              TodoStoreService = todoStore :> ITodoStore }

        let chooseTodoFlow : TaskFlow<AppRuntime, TodoError, string option> =
            Flow.read (fun (runtime: AppRuntime) ->
                let todos = (runtime :> Needs<ITodoStore>).Dep.Todos

                match todos with
                | [] -> None
                | _ ->
                    let index = (runtime :> Needs<IRandom>).Dep.NextInt 0 todos.Length
                    Some todos[index])
            |> TaskFlow.fromFlow

        let _chooseTodoBoundary : TaskFlow<#ChooseTodoCaps, TodoError, string option> = chooseTodoFlow
        let _appBoundary : TaskFlow<#AppCaps, TodoError, string option> = chooseTodoFlow

        let chooseTodoResult =
            TaskFlow.run appRuntime CancellationToken.None chooseTodoFlow
            |> fun task -> task.GetAwaiter().GetResult()

        test <@ chooseTodoResult = Exit.Success (Some "beta") @>
