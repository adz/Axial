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

    type ClockServices =
        interface
            inherit IHas<IClock>
            abstract Clock : IClock
        end

    type LoggerServices =
        interface
            inherit IHas<ILogger>
            abstract Logger : ILogger
        end

    type RandomServices =
        interface
            inherit IHas<IRandom>
            abstract Random : IRandom
        end

    type TodoStoreServices =
        interface
            inherit IHas<ITodoStore>
            abstract TodoStore : ITodoStore
        end

    type ChooseTodoServices =
        interface
            inherit IHas<IRandom>
            inherit IHas<ITodoStore>
            abstract Random : IRandom
            abstract TodoStore : ITodoStore
        end

    type AppServices =
        interface
            inherit ChooseTodoServices
            inherit IHas<IClock>
            inherit IHas<ILogger>
            abstract Clock : IClock
            abstract Logger : ILogger
        end

    type ChooseTodoTestRuntime =
        { RandomService: IRandom
          TodoStoreService: ITodoStore }
        with
            interface ChooseTodoServices with
                member x.Random = x.RandomService
                member x.TodoStore = x.TodoStoreService

            interface IHas<IRandom> with
                member x.Service = x.RandomService

            interface IHas<ITodoStore> with
                member x.Service = x.TodoStoreService

    type AppRuntime =
        { ClockService: IClock
          LoggerService: ILogger
          RandomService: IRandom
          TodoStoreService: ITodoStore }
        with
            interface AppServices with
                member x.Clock = x.ClockService
                member x.Logger = x.LoggerService
                member x.Random = x.RandomService
                member x.TodoStore = x.TodoStoreService

            interface IHas<IClock> with
                member x.Service = x.ClockService

            interface IHas<ILogger> with
                member x.Service = x.LoggerService

            interface IHas<IRandom> with
                member x.Service = x.RandomService

            interface IHas<ITodoStore> with
                member x.Service = x.TodoStoreService

    type TodoError =
        | EmptyTodoList

    [<Fact>]
    let ``fine-grained services expose their dependencies through IHas`` () =
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

        let clockNeeds = appRuntime :> IHas<IClock>
        let loggerNeeds = appRuntime :> IHas<ILogger>
        let randomNeeds = appRuntime :> IHas<IRandom>
        let todoStoreNeeds = appRuntime :> IHas<ITodoStore>
        let testRandomNeeds = chooseTodoRuntime :> IHas<IRandom>
        let testTodoStoreNeeds = chooseTodoRuntime :> IHas<ITodoStore>

        test <@ obj.ReferenceEquals(box clockNeeds.Service, box clock) @>
        test <@ obj.ReferenceEquals(box loggerNeeds.Service, box logger) @>
        test <@ obj.ReferenceEquals(box randomNeeds.Service, box random) @>
        test <@ obj.ReferenceEquals(box todoStoreNeeds.Service, box todoStore) @>
        test <@ obj.ReferenceEquals(box testRandomNeeds.Service, box random) @>
        test <@ obj.ReferenceEquals(box testTodoStoreNeeds.Service, box todoStore) @>

    [<Fact>]
    let ``named service-set flows run on both larger app runtimes and smaller test runtimes`` () =
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

        let chooseTodoFlowForApp : Flow<AppRuntime, TodoError, string option> =
            flow {
                let! todoStore = Service<ITodoStore>.get<AppRuntime, TodoError>()
                let! random = Service<IRandom>.get<AppRuntime, TodoError>()
                let todos = todoStore.Todos

                match todos with
                | [] -> return None
                | _ ->
                    let index = random.NextInt 0 todos.Length
                    return Some todos[index]
            }

        let chooseTodoFlowForTest : Flow<ChooseTodoTestRuntime, TodoError, string option> =
            flow {
                let! todoStore = Service<ITodoStore>.get<ChooseTodoTestRuntime, TodoError>()
                let! random = Service<IRandom>.get<ChooseTodoTestRuntime, TodoError>()
                let todos = todoStore.Todos

                match todos with
                | [] -> return None
                | _ ->
                    let index = random.NextInt 0 todos.Length
                    return Some todos[index]
            }

        let appResult =
            Flow.runSync appRuntime chooseTodoFlowForApp

        let testResult =
            Flow.runSync chooseTodoRuntime chooseTodoFlowForTest

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

        let chooseTodoFlow : Flow<AppRuntime, TodoError, string option> =
            flow {
                let! todoStore = Service<ITodoStore>.get<AppRuntime, TodoError>()
                let! random = Service<IRandom>.get<AppRuntime, TodoError>()
                let todos = todoStore.Todos

                match todos with
                | [] -> return None
                | _ ->
                    let index = random.NextInt 0 todos.Length
                    return Some todos[index]
            }

        let chooseTodoResult =
            Flow.runSync appRuntime chooseTodoFlow

        test <@ chooseTodoResult = Exit.Success (Some "beta") @>
