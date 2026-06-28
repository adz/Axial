open System

module Runner =
    let run () =
        RequestBoundaryExample.run()
        printfn ""
        DiagnosticsExample.run()
        printfn ""
        RefinedCatalogExample.run()

[<EntryPoint>]
let main _ =
    match Environment.GetEnvironmentVariable "AXIAL_EXAMPLE" with
    | "request-boundary" -> RequestBoundaryExample.run()
    | "diagnostics" -> DiagnosticsExample.run()
    | "refined-catalog" -> RefinedCatalogExample.run()
    | _ -> Runner.run()
    0
