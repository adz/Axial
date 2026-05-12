module RequestBoundaryExample

open System
open System.Threading
open System.Threading.Tasks
open FsFlow

type User =
    { Id: int
      Name: string }

type AppDb =
    { FindUser: int -> User option }

type RequestEnv =
    { TraceId: Guid
      Prefix: string
      Db: AppDb
      LoadSuffix: Task<string> }

let validateName (name: string) : Result<string, string> =
    Check.notBlank name
    |> Check.orError "name is required"

let loadUser : Flow<RequestEnv, string, User> =
    flow {
        let! db = Flow.read _.Db // Flow<RequestEnv, string, AppDb>
        let! user = db.FindUser 42 |> Flow.fromOption "user not found" // Flow<RequestEnv, string, User>
        return user
    }

let renderTrace : Flow<RequestEnv, string, string> =
    flow {
        let! env = Flow.env // Flow<RequestEnv, string, RequestEnv>
        let! user = loadUser // Flow<RequestEnv, string, User>
        let! validName = validateName user.Name // Flow<RequestEnv, string, string>
        return $"{env.Prefix} [{env.TraceId}] {validName}"
    }

let publishResponse : Flow<RequestEnv, string, string> =
    flow {
        let! env = Flow.env // Flow<RequestEnv, string, RequestEnv>
        let! user = loadUser // Flow<RequestEnv, string, User>
        let! suffix = env.LoadSuffix // Flow<RequestEnv, string, string>
        return $"{env.Prefix} [{env.TraceId}] {user.Name}{suffix}"
    }

let run () =
    let environment =
        { TraceId = Guid.Parse "11111111-1111-1111-1111-111111111111"
          Prefix = "Hello"
          Db =
            { FindUser =
                function
                | 42 -> Some { Id = 42; Name = "Ada" }
                | _ -> None }
          LoadSuffix = Task.FromResult "!" }

    let syncResult =
        loadUser
        |> Flow.run environment
        |> fun t -> t.AsTask().GetAwaiter().GetResult()

    let asyncResult =
        renderTrace
        |> Flow.run environment
        |> fun t -> t.AsTask().GetAwaiter().GetResult()

    let taskResult =
        publishResponse
        |> Flow.run environment
        |> fun t -> t.AsTask().GetAwaiter().GetResult()

    printfn "Flow result: %A" syncResult
    printfn "Flow result: %A" asyncResult
    printfn "Flow result: %A" taskResult
    // Flow result: Ok { Id = 42; Name = "Ada" }
    // Flow result: Ok "Hello [11111111-1111-1111-1111-111111111111] Ada"
    // Flow result: Ok "Hello [11111111-1111-1111-1111-111111111111] Ada!"
