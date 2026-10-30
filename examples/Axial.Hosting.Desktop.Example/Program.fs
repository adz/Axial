open System
open Axial
open Axial.PlatformService

type DesktopEnvironment =
    { WindowName: string }

let application : Flow<DesktopEnvironment, string, unit> =
    flow {
        let! environment = Flow.env
        // The framework closing path below waits for this root cleanup before approving exit.
        do! Flow.scopeFinalizer (Flow.fromAsync (async { printfn "Saving state and releasing application resources." }))
        do! async { printfn "%s application scope started." environment.WindowName }
        do! Flow.sleep(TimeSpan.FromDays 1.0)
    }

[<EntryPoint>]
let main _ =
    // A real desktop framework calls this after its application/window startup event.
    let running = App.start { WindowName = "Main window" } application

    printfn "Press Enter to simulate the desktop framework's closing event."
    // executable host boundary performs this effect.
    // axial-allow-AXG001: intentional console host or live-service boundary
    Console.ReadLine() |> ignore

    // A real closing handler awaits Stop before allowing framework shutdown.
    // executable host boundary performs this effect.
    // axial-allow-AXG001: intentional blocking host or live-service boundary
    let exit = running.Stop() |> Async.RunSynchronously
    printfn "Framework may now exit. Final result: %s" (exit.ToString())
    0
