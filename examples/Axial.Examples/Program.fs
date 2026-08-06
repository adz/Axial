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
    match Environment.GetEnvironmentVariable "AXIAL_EXAMPLE" with
    | "request-boundary" -> RequestBoundaryExample.run()
    | "policy" -> PolicyExamples.run()
    | "supervision" -> SupervisionExample.run()
    | _ -> Runner.run()
    0
