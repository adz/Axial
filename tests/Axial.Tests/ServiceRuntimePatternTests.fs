namespace Axial.Tests

open Axial
open Swensen.Unquote
open Xunit

module ServiceRuntimePatternTests =
    // Application authority stays explicit even though platform reads no longer need env contracts.
    type ITodoStore = abstract Todos: string list
    type IUserPreferences = abstract PreferredIndex: int
    type IHasTodoStore = abstract TodoStore: ITodoStore
    type IHasUserPreferences = abstract Preferences: IUserPreferences
    type IChooseTodoEnv =
        inherit IHasTodoStore
        inherit IHasUserPreferences

    type InMemoryTodoStore(todos: string list) =
        interface ITodoStore with member _.Todos = todos
    type FixedPreferences(index: int) =
        interface IUserPreferences with member _.PreferredIndex = index

    type ChooseTodoTestRuntime =
        { TodoStoreService: ITodoStore; PreferencesService: IUserPreferences }
        interface IChooseTodoEnv
        interface IHasTodoStore with member this.TodoStore = this.TodoStoreService
        interface IHasUserPreferences with member this.Preferences = this.PreferencesService

    type AppRuntime =
        { Region: string; TodoStoreService: ITodoStore; PreferencesService: IUserPreferences }
        interface IChooseTodoEnv
        interface IHasTodoStore with member this.TodoStore = this.TodoStoreService
        interface IHasUserPreferences with member this.Preferences = this.PreferencesService

    [<RequireQualifiedAccess>]
    module TodoStore =
        let service<'env, 'error when 'env :> IHasTodoStore> : Flow<'env, 'error, ITodoStore> = Flow.envWith _.TodoStore
    [<RequireQualifiedAccess>]
    module Preferences =
        let service<'env, 'error when 'env :> IHasUserPreferences> : Flow<'env, 'error, IUserPreferences> = Flow.envWith _.Preferences

    type TodoError = EmptyTodoList

    let chooseTodo<'env when 'env :> IHasTodoStore and 'env :> IHasUserPreferences> () : Flow<'env, TodoError, string option> =
        flow {
            let! store = TodoStore.service
            let! preferences = Preferences.service
            return match store.Todos with [] -> None | todos -> Some todos[preferences.PreferredIndex]
        }

    let chooseTodoViaAggregate () : Flow<#IChooseTodoEnv, TodoError, string option> =
        flow {
            let! store = TodoStore.service
            let! preferences = Preferences.service
            return match store.Todos with [] -> None | todos -> Some todos[preferences.PreferredIndex]
        }

    let todoCount () : Flow<#IHasTodoStore, TodoError, int> = TodoStore.service |> Flow.map (fun store -> store.Todos.Length)

    [<Fact>]
    let ``environments expose application services through per-service contracts`` () =
        let store = InMemoryTodoStore ["alpha"; "beta"; "gamma"]
        let preferences = FixedPreferences 1
        let full = { Region = "eu"; TodoStoreService = store; PreferencesService = preferences }
        let small = { TodoStoreService = store; PreferencesService = preferences }
        test <@ obj.ReferenceEquals((full :> IHasTodoStore).TodoStore, store) @>
        test <@ obj.ReferenceEquals((full :> IHasUserPreferences).Preferences, preferences) @>
        test <@ obj.ReferenceEquals((small :> IHasTodoStore).TodoStore, store) @>
        test <@ obj.ReferenceEquals((small :> IHasUserPreferences).Preferences, preferences) @>

    [<Fact>]
    let ``one flow declaring two application services runs on full and small environments`` () =
        let store = InMemoryTodoStore ["alpha"; "beta"; "gamma"]
        let preferences = FixedPreferences 1
        let full = { Region = "eu"; TodoStoreService = store; PreferencesService = preferences }
        let small = { TodoStoreService = store; PreferencesService = preferences }
        test <@ chooseTodo () |> Flow.runSync full = Exit.Success(Some "beta") @>
        test <@ chooseTodo () |> Flow.runSync small = Exit.Success(Some "beta") @>

    [<Fact>]
    let ``an empty store returns no todo`` () =
        let environment = { TodoStoreService = InMemoryTodoStore []; PreferencesService = FixedPreferences 0 }
        test <@ chooseTodo () |> Flow.runSync environment = Exit.Success None @>

    [<Fact>]
    let ``every way of declaring the environment reaches the same application services`` () =
        let store = InMemoryTodoStore ["alpha"; "beta"; "gamma"]
        let preferences = FixedPreferences 1
        let full = { Region = "eu"; TodoStoreService = store; PreferencesService = preferences }
        let small = { TodoStoreService = store; PreferencesService = preferences }
        test <@ chooseTodo () |> Flow.runSync full = Exit.Success(Some "beta") @>
        test <@ chooseTodo () |> Flow.runSync small = Exit.Success(Some "beta") @>
        test <@ chooseTodoViaAggregate () |> Flow.runSync full = Exit.Success(Some "beta") @>
        test <@ chooseTodoViaAggregate () |> Flow.runSync small = Exit.Success(Some "beta") @>
        test <@ todoCount () |> Flow.runSync full = Exit.Success 3 @>
        test <@ todoCount () |> Flow.runSync small = Exit.Success 3 @>
        let describeTodo: Flow<AppRuntime, TodoError, string> = flow {
            let! region = Flow.envWith _.Region
            let! store = TodoStore.service
            return $"{region}: {store.Todos.Length} todos"
        }
        test <@ describeTodo |> Flow.runSync full = Exit.Success "eu: 3 todos" @>
