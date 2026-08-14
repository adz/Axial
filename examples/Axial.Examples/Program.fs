open System

module Runner =
    let run () =
        RequestBoundaryExample.run()
        printfn ""
        PolicyExamples.run()
        printfn ""
        SupervisionExample.run()

[<EntryPoint>]
let main _ =
    // Program.main is the OS-process entry point itself, so reading the environment here selects
    // which demo to run rather than leaking an ambient effect into application logic.
    match Environment.GetEnvironmentVariable "AXIAL_EXAMPLE" with // axial-allow-effect: environment
    | "request-boundary" -> RequestBoundaryExample.run()
    | "policy" -> PolicyExamples.run()
    | "supervision" -> SupervisionExample.run()
    | _ -> Runner.run()
    0
