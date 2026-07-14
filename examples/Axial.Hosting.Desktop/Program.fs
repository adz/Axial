open System
open Axial.Flow

let application : Flow<string, string, unit> =
    flow {
        let! windowName = Flow.env
        // The framework closing path below waits for this root cleanup before approving exit.
        do! Flow.addFinalizerAsync (fun _ -> async { printfn "Saving state and releasing application resources." })
        do! async { printfn "%s application scope started." windowName }
        do! Flow.Runtime.sleep(TimeSpan.FromDays 1.0)
    }

[<EntryPoint>]
let main _ =
    // A real desktop framework calls this after its application/window startup event.
    let running = App.start "Main window" application

    printfn "Press Enter to simulate the desktop framework's closing event."
    Console.ReadLine() |> ignore

    // A real closing handler awaits Stop before allowing framework shutdown.
    let exit = running.Stop() |> Async.RunSynchronously
    printfn "Framework may now exit. Final result: %A" exit
    0
