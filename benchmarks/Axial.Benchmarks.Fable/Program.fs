namespace Axial.Benchmarks.Fable

open System

module private Runner =
    let reportSection targetName title iterations baselineName baselineWork flowName flowWork =
        printfn ""
        printfn "%s - %s" targetName title
        Shared.measure iterations baselineName baselineWork |> ignore
        Shared.measure iterations flowName flowWork |> ignore

module Program =
    let private targetName = "Fable"

    [<EntryPoint>]
    let main argv =
        let iterations = 10000

        let readerEnvironment = { Shared.ReaderEnv.Prefix = "prefix" }

        printfn "Target: %s" targetName

        let schemaSummary = Shared.buildSchemaBuilderSummary ()

        if schemaSummary <> [ "0:name"; "1:age" ] then
            failwith $"Unexpected schema builder summary: %A{schemaSummary}"

        let roundTripped = Shared.runCodecRoundTrip ()

        if roundTripped <> ({ Name = "Ada"; Age = 37 }: Shared.SchemaContact) then
            failwith $"Unexpected codec round-trip result: %A{roundTripped}"

        printfn "Codec round-trip: ok"

        Runner.reportSection
            targetName
            ".NET-like sync result"
            iterations
            "manual result"
            Shared.buildSyncManual
            "flow"
            (fun () -> Shared.runFlow () (Shared.buildSyncFlow ()))

        Runner.reportSection
            targetName
            "async result"
            iterations
            "manual async result"
            Shared.buildAsyncManual
            "flow"
            (fun () -> Shared.runFlow () (Shared.buildAsyncFlow ()))

        Runner.reportSection
            targetName
            "reader propagation"
            iterations
            "manual env passing"
            (fun () -> Shared.buildReaderManual () readerEnvironment)
            "flow"
            (fun () -> Shared.runFlow readerEnvironment (Shared.buildReaderFlow ()))

        0
