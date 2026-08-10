namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

module ServiceRuntimePatternTests =
    type IClock =
        abstract UtcNow : unit -> DateTimeOffset

    type ILogger =
        abstract Log : string -> unit

    type IRandom =
        abstract NextInt : minInclusive: int -> maxExclusive: int -> int

    type ITodoStore =
        abstract Todos : string list

    /// One non-generic contract per service. Because these are distinct interfaces, a single
    /// environment can carry any combination of them and the constraints merge without help.
    type IHasClock =
        abstract Clock : IClock

    type IHasLogger =
        abstract Logger : ILogger

    type IHasRandom =
        abstract Random : IRandom

    type IHasTodoStore =
        abstract TodoStore : ITodoStore

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
            member _.NextInt _ _ = index

    type InMemoryTodoStore(todos: string list) =
        interface ITodoStore with
            member _.Todos = todos

    /// A small runtime supplying only what the todo workflow needs.
    type ChooseTodoTestRuntime =
        { RandomService: IRandom
          TodoStoreService: ITodoStore }

        interface IHasRandom with
            member this.Random = this.RandomService

        interface IHasTodoStore with
            member this.TodoStore = this.TodoStoreService

    /// The full application runtime, supplying every service.
    type AppRuntime =
        { ClockService: IClock
          LoggerService: ILogger
          RandomService: IRandom
          TodoStoreService: ITodoStore }

        interface IHasClock with
            member this.Clock = this.ClockService

        interface IHasLogger with
            member this.Logger = this.LoggerService

        interface IHasRandom with
            member this.Random = this.RandomService

        interface IHasTodoStore with
            member this.TodoStore = this.TodoStoreService

    [<RequireQualifiedAccess>]
    module Random =
        let service<'env, 'error when 'env :> IHasRandom> : Flow<'env, 'error, IRandom> =
            Flow.read _.Random

    [<RequireQualifiedAccess>]
    module TodoStore =
        let service<'env, 'error when 'env :> IHasTodoStore> : Flow<'env, 'error, ITodoStore> =
            Flow.read _.TodoStore

    type TodoError =
        | EmptyTodoList

    /// Declares exactly the two services it uses, and stays generic over any environment that
    /// supplies them. No annotation inside the block, and no aggregate interface.
    let chooseTodo<'env when 'env :> IHasRandom and 'env :> IHasTodoStore>
        ()
        : Flow<'env, TodoError, string option> =
        flow {
            let! todoStore = TodoStore.service
            let! random = Random.service

            match todoStore.Todos with
            | [] -> return None
            | todos ->
                let index = random.NextInt 0 todos.Length
                return Some todos[index]
        }

    [<Fact>]
    let ``environments expose their services through per-service contracts`` () =
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

        test <@ obj.ReferenceEquals(box (appRuntime :> IHasClock).Clock, box clock) @>
        test <@ obj.ReferenceEquals(box (appRuntime :> IHasLogger).Logger, box logger) @>
        test <@ obj.ReferenceEquals(box (appRuntime :> IHasRandom).Random, box random) @>
        test <@ obj.ReferenceEquals(box (appRuntime :> IHasTodoStore).TodoStore, box todoStore) @>
        test <@ obj.ReferenceEquals(box (chooseTodoRuntime :> IHasRandom).Random, box random) @>
        test <@ obj.ReferenceEquals(box (chooseTodoRuntime :> IHasTodoStore).TodoStore, box todoStore) @>

    [<Fact>]
    let ``one flow declaring two services runs on both a full runtime and a smaller test runtime`` () =
        let random = FixedRandom 1
        let todoStore = InMemoryTodoStore [ "alpha"; "beta"; "gamma" ]

        let appRuntime =
            { ClockService = FixedClock(DateTimeOffset(2026, 5, 9, 12, 30, 0, TimeSpan.Zero)) :> IClock
              LoggerService = RecordingLogger() :> ILogger
              RandomService = random :> IRandom
              TodoStoreService = todoStore :> ITodoStore }

        let chooseTodoRuntime =
            { RandomService = random :> IRandom
              TodoStoreService = todoStore :> ITodoStore }

        let appResult = Flow.run appRuntime (chooseTodo ())
        let testResult = Flow.run chooseTodoRuntime (chooseTodo ())

        test <@ appResult = Exit.Success (Some "beta") @>
        test <@ testResult = Exit.Success (Some "beta") @>

    [<Fact>]
    let ``an empty store returns no todo`` () =
        let runtime =
            { RandomService = FixedRandom 0 :> IRandom
              TodoStoreService = InMemoryTodoStore [] :> ITodoStore }

        test <@ Flow.run runtime (chooseTodo ()) = Exit.Success None @>
