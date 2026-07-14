namespace Axial.Benchmarks.Fable

open System
open Axial.Flow
open Axial.Flow.Telemetry.JavaScript

/// An in-memory `@opentelemetry/api` fake proving the OTel JS surface works end to end under Fable.
module private OtelCheck =
    type RecordedSpan =
        { Name: string
          Tags: ResizeArray<string * string>
          mutable StatusCode: int
          mutable Ended: bool }

    let fakeApi (spans: ResizeArray<RecordedSpan>) : OpenTelemetryApi =
        let makeSpan name =
            let recorded = { Name = name; Tags = ResizeArray (); StatusCode = 0; Ended = false }
            spans.Add recorded

            { new Span with
                member _.setAttribute (key, value) = recorded.Tags.Add (key, value)
                member _.setStatus status =
                    recorded.StatusCode <- (unbox<{| code: int; message: string |}> status).code
                member _.spanContext () = box recorded
                member _.``end`` () = recorded.Ended <- true }

        let tracer =
            { new Tracer with
                member _.startSpan (name, _, _) = makeSpan name }

        { new OpenTelemetryApi with
            member _.trace =
                { new TraceApi with
                    member _.getTracer _ = tracer
                    member _.setSpan (context, _) = context }
            member _.context =
                { new ContextApi with
                    member _.active () = unbox<Context> (box "active-context")
                    member _.``with`` (_, operation) = operation () } }

#if FABLE_COMPILER
    let private runToExit (flow: Flow<unit, string, int>) : Exit<int, string> =
        let mutable exit = None

        Async.StartWithContinuations (
            flow.ToAsync (()),
            (fun value -> exit <- Some value),
            raise,
            (fun _ -> raise (OperationCanceledException ()))
        )

        match exit with
        | Some value -> value
        | None -> failwith "The traced Fable flow did not complete synchronously."

    let private expectTag (span: RecordedSpan) key expected =
        match span.Tags |> Seq.tryFind (fun (tagKey, _) -> tagKey = key) with
        | Some (_, value) when value = expected -> ()
        | Some (_, value) -> failwith $"Span '{span.Name}' tag {key}: expected '{expected}', got '{value}'."
        | None -> failwith $"Span '{span.Name}' is missing tag {key}."

    let run () =
        let spans = ResizeArray ()
        Otel.install (fakeApi spans)

        let successExit =
            Flow.ok 21
            |> Flow.annotate "check.key" "check-value"
            |> Otel.trace "fable.trace.success"
            |> runToExit

        match successExit with
        | Exit.Success 21 -> ()
        | other -> failwith $"Unexpected traced success exit: %A{other}"

        let failureExit =
            (Flow.fail "boom": Flow<unit, string, int>)
            |> Otel.trace "fable.trace.failure"
            |> runToExit

        match failureExit with
        | Exit.Failure (Cause.Fail "boom") -> ()
        | other -> failwith $"Unexpected traced failure exit: %A{other}"

        match Seq.toList spans with
        | [ successSpan; failureSpan ] ->
            if not (successSpan.Ended && failureSpan.Ended) then
                failwith "Traced spans were not ended."

            expectTag successSpan "axial.flow.outcome" "success"
            expectTag successSpan "axial.flow.annotation.check.key" "check-value"

            if successSpan.StatusCode <> 1 then
                failwith $"Success span status: expected 1, got {successSpan.StatusCode}."

            expectTag failureSpan "axial.flow.outcome" "fail"
            expectTag failureSpan "axial.flow.error" "boom"

            if failureSpan.StatusCode <> 2 then
                failwith $"Failure span status: expected 2, got {failureSpan.StatusCode}."
        | other -> failwith $"Expected two recorded spans, got %d{List.length other}."

        // The fiber observers share the installed tracer: a defect end event must produce an ended
        // error span with the fiber vocabulary.
        let metadata =
            { Id = FiberId 41L
              ParentId = None
              StartedAt = DateTimeOffset.UtcNow
              Status = FiberStatus.Failed
              Observed = true }

        FiberTelemetry.observer.OnEnd metadata (Some (InvalidOperationException "fiber-defect"))

        match spans |> Seq.tryFind (fun span -> span.Name = "axial.flow.fiber.defect") with
        | Some defectSpan when defectSpan.Ended ->
            expectTag defectSpan "exception.message" "fiber-defect"
        | Some _ -> failwith "Fiber defect span was not ended."
        | None -> failwith "Fiber defect span was not recorded."

        Otel.uninstall ()
        printfn "Otel spans: ok"
#endif

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

#if FABLE_COMPILER
        OtelCheck.run ()
#endif

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
