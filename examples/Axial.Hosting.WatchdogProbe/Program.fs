open System
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Axial
open Axial.Hosting
open Axial.PlatformService

type ProbeEnv = unit

[<EntryPoint>]
let main args =
    let hangAfter =
        match args with
        | [| "run" |] -> None
        | [| "hang-after"; seconds |] when Int32.TryParse(seconds) |> fst && int seconds >= 0 -> Some(int seconds)
        | _ -> invalidArg "args" "Expected run or hang-after <seconds>"
    let clock = Clock.live
    let started = clock.Elapsed()
    let heartbeat : Flow<ProbeEnv, Never, unit> =
        let rec loop () = flow {
            do! Flow.sleep(TimeSpan.FromSeconds 1.)
            printfn "heartbeat %d" (int (clock.Elapsed() - started).TotalSeconds)
            return! loop ()
        }
        loop ()
    let health : Flow<ProbeEnv, Never, bool> =
        flow {
            match hangAfter with
            | None -> return true
            | Some seconds -> return (clock.Elapsed() - started).TotalSeconds < float seconds
        }
    let variables = EnvironmentVariables.live
    let application : Flow<ProbeEnv, Never, unit> =
        Flow.zipPar heartbeat
            (Hosting.watchdogWith variables Environment.ProcessId
                (SystemdNotify.live (variables.TryGet "NOTIFY_SOCKET" |> Option.defaultValue "@unused")) health)
        |> Flow.map ignore
    Host.CreateDefaultBuilder(args)
        .UseSystemd()
        .ConfigureServices(fun services ->
            services |> Hosting.addApp (fun _ -> ()) (fun (_: Never) -> "never") application |> ignore)
        .Build().Run()
    0
