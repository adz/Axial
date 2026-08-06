open System

module Runner =
    let run () =
        RefinedCatalogExample.run()
        printfn ""
        RefinedValueSchemaExample.run()

[<EntryPoint>]
let main _ =
    match Environment.GetEnvironmentVariable "AXIAL_EXAMPLE" with
    | "refined-catalog" -> RefinedCatalogExample.run()
    | "refined-value-schema" -> RefinedValueSchemaExample.run()
    | _ -> Runner.run()

    0
