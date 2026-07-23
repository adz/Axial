open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.ErrorHandling

let runFlow label env (workflow: Flow<'env, 'error, 'value>) =
    let result = workflow.RunSynchronously(env)
    printfn "%s: %A" label result

let runAsyncExample label env (workflow: Flow<'env, 'error, 'value>) =
    let result =
        workflow
        |> fun workflow -> workflow.RunSynchronously(env)

    printfn "%s: %A" label result

let runTaskExample label env (workflow: Flow<'env, 'error, 'value>) =
    let result =
        workflow
        |> fun workflow -> workflow.RunSynchronously(env)

    printfn "%s: %A" label result

let syncExample : Flow<int, string, int> =
    Flow.read id // Flow<int, string, int>
    |> Flow.map ((+) 1)

let asyncExample : Flow<int, string, int> =
    flow {
        let! value = async { return 21 }
        return value * 2
    }

let taskExample : Flow<int, string, int> =
    flow {
        let! env = Flow.read id
        let! suffix = Task.FromResult 5
        return env + suffix
    }

[<EntryPoint>]
let main _ =
    runFlow "Flow" 20 syncExample
    runAsyncExample "Async" 20 asyncExample
    runTaskExample "Task" 20 taskExample
    // Flow: Ok 21
    // Async: Ok 42
    // Task: Ok 25
    0
