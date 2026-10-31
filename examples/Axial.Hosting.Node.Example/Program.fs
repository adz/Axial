module Axial.Hosting.Node.Example

open System
open Axial
open Axial.Configuration
open Axial.Hosting.Node
open Reified

type AppError =
    | StartupFailure of string

let describeError = function
    | StartupFailure message -> message

type AppEnv =
    { Arguments: string list
      Greeting: string }

let application : Flow<AppEnv, AppError, unit> =
    flow {
        let! environment = Flow.env
        // Root-scope cleanup must complete before NodeApp publishes the process exit code.
        do! Flow.scopeFinalizer (Flow.fromAsync (async { printfn "Node root cleanup finished." }))
        do! async { printfn "%s Arguments: %s" environment.Greeting (String.concat ", " environment.Arguments) }
        do! async { printfn "Press Ctrl+C to request coordinated shutdown." }
        do! Flow.sleep(TimeSpan.FromDays 1.0)
    }

let private configuration =
    match Flow.run () (Config.loadData [ ConfigSource.environment None ]) with
    | Exit.Success config -> config
    | Exit.Failure _ -> Config.ofData (Data.Object [])

let environment =
    { Arguments = NodeApp.arguments()
      Greeting =
        Config.tryFind "AXIAL_GREETING" configuration
        |> Result.toOption
        |> Option.bind id
        |> Option.defaultValue "Hello from Node." }

// NodeApp owns SIGINT/SIGTERM subscriptions and delays process.exitCode until Completion settles.
let running = NodeApp.start describeError environment application

async {
    let! _ = running.Completion
    return ()
}
|> Async.StartImmediate
