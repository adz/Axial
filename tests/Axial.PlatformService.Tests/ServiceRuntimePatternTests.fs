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

    /// A named contract combining the two services the todo workflow needs. Legal because the
    /// parents are distinct non-generic interfaces; a generic parent would reintroduce FS0193.
    type IChooseTodoEnv =
        inherit IHasRandom
        inherit IHasTodoStore

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

        interface IChooseTodoEnv

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

        interface IChooseTodoEnv

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

    [<RequireQualifiedAccess>]
    module Clock =
        let service<'env, 'error when 'env :> IHasClock> : Flow<'env, 'error, IClock> =
            Flow.read _.Clock

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

    /// The same workflow, constrained by the combined contract instead of listing both services,
    /// and written with flexible-type syntax. Still a type variable, so it stays generic over every
    /// implementor. Prefer the explicit form when the environment appears more than once in a
    /// signature, since each occurrence of `#T` is a separate variable.
    let chooseTodoViaAggregate () : Flow<#IChooseTodoEnv, TodoError, string option> =
        flow {
            let! todoStore = TodoStore.service
            let! random = Random.service

            match todoStore.Todos with
            | [] -> return None
            | todos ->
                let index = random.NextInt 0 todos.Length
                return Some todos[index]
        }

    /// One service only, written with flexible-type syntax. The unit parameter is required: a
    /// parameterless value cannot be generic over its environment.
    let todoCount () : Flow<#IHasTodoStore, TodoError, int> =
        TodoStore.service |> Flow.map (fun store -> store.Todos.Length)

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

    [<Fact>]
    let ``every way of declaring the environment reaches the same services`` () =
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

        // two explicit constraints, generic
        test <@ Flow.run appRuntime (chooseTodo ()) = Exit.Success (Some "beta") @>
        test <@ Flow.run chooseTodoRuntime (chooseTodo ()) = Exit.Success (Some "beta") @>

        // one combined contract, still generic over both runtimes
        test <@ Flow.run appRuntime (chooseTodoViaAggregate ()) = Exit.Success (Some "beta") @>
        test <@ Flow.run chooseTodoRuntime (chooseTodoViaAggregate ()) = Exit.Success (Some "beta") @>

        // a single service through flexible-type syntax
        test <@ Flow.run appRuntime (todoCount ()) = Exit.Success 3 @>
        test <@ Flow.run chooseTodoRuntime (todoCount ()) = Exit.Success 3 @>

        // a concrete environment: two services, no annotation inside the block, and a plain value
        // rather than a function, because an environment that is not a type variable does not hit
        // the value restriction. Bound locally rather than at module level: this is the last file
        // in the project, and F# never initialises the last file's top-level values for callers
        // inside the same assembly.
        let describeTodo : Flow<AppRuntime, TodoError, string> =
            flow {
                let! clock = Clock.service
                let! todoStore = TodoStore.service
                let day = clock.UtcNow().ToString("yyyy-MM-dd")
                return $"{day}: {todoStore.Todos.Length} todos"
            }

        test <@ Flow.run appRuntime describeTodo = Exit.Success "2026-05-09: 3 todos" @>
