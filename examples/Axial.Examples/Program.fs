open System

module Runner =
    let run () =
        RequestBoundaryExample.run()
        printfn ""
        DiagnosticsExample.run()
        printfn ""
        RefinedCatalogExample.run()
        printfn ""
        RefinedValueSchemaExample.run()
        printfn ""
        PolicyExamples.run()

[<EntryPoint>]
let main _ =
    match Environment.GetEnvironmentVariable "AXIAL_EXAMPLE" with
    | "request-boundary" -> RequestBoundaryExample.run()
    | "diagnostics" -> DiagnosticsExample.run()
    | "refined-catalog" -> RefinedCatalogExample.run()
    | "refined-value-schema" -> RefinedValueSchemaExample.run()
    | "policy" -> PolicyExamples.run()
    | _ -> Runner.run()
    0
