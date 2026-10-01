module Axial.Hosting.Node.Example

open System
open Axial
open Axial.PlatformService
open Axial.Hosting.Node

type AppError =
    | StartupFailure of string

let describeError = function
    | StartupFailure message -> message

type AppEnv =
    { Arguments: string list
      Greeting: string
      Clock: IClock }
    interface IHasClock with member this.Clock = this.Clock

let application : Flow<AppEnv, AppError, unit> =
    flow {
        let! environment = Flow.env
        // Root-scope cleanup must complete before NodeApp publishes the process exit code.
        do! Flow.scopeAsyncFinalizer (fun _ -> async { printfn "Node root cleanup finished." })
        do! async { printfn "%s Arguments: %A" environment.Greeting environment.Arguments }
        do! async { printfn "Press Ctrl+C to request coordinated shutdown." }
        do! Flow.sleep(TimeSpan.FromDays 1.0)
    }

let environment =
    { Arguments = NodeApp.arguments()
      Greeting = NodeEnvironment.live.TryGet("AXIAL_GREETING") |> Option.defaultValue "Hello from Node."
      Clock = Clock.live }

// NodeApp owns SIGINT/SIGTERM subscriptions and delays process.exitCode until Completion settles.
let running = NodeApp.start describeError environment application

async {
    let! _ = running.Completion
    return ()
}
|> Async.StartImmediate
