namespace Axial.Tests

open System.Diagnostics
open Axial
open Axial.Telemetry
open Swensen.Unquote
open Xunit

module TelemetryTests =
    /// Waits for a fiber to settle without consuming its outcome, so it stays unobserved.
    /// Deterministic replacement for fixed sleeps, which race the thread pool under load.
    let rec private waitForSettled (fiber: Fiber<'error, 'value>) : Flow<unit, 'testError, unit> =
        flow {
            if fiber.Metadata.Status = FiberStatus.Running then
                do! Flow.Runtime.sleep (System.TimeSpan.FromMilliseconds 5.0)
                return! waitForSettled fiber
        }

    [<Fact>]
    let ``Activity.trace: exports ambient context and runtime annotations`` () =
        let requestId = AttributeKey.string "app.request.id"
        let correlationId = AttributeKey.string "app.correlation.id"

        let listener = new ActivityListener()
        listener.ShouldListenTo <- (fun source -> source.Name = "Axial")
        listener.Sample <- (fun _ -> ActivitySamplingResult.AllData)
        
        let mutable capturedTags = Map.empty
        listener.ActivityStopped <- (fun activity ->
            capturedTags <- 
                activity.Tags 
                |> Seq.map (fun kv -> kv.Key, kv.Value)
                |> Map.ofSeq)

        ActivitySource.AddActivityListener(listener)

        let workflow =
            flow { return 42 }
            |> Flow.annotate "deviceId" "device-1"
            |> Flow.traceId "trace-1"
            |> Activity.trace "test-op"
            |> Context.withAttributes [
                Context.attribute requestId "req-123"
                Context.attribute correlationId "corr-456"
            ]

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success 42 @>
        test <@ capturedTags["app.request.id"] = "req-123" @>
        test <@ capturedTags["app.correlation.id"] = "corr-456" @>
        test <@ capturedTags.ContainsKey("axial.flow.annotation.deviceId") @>
        test <@ capturedTags["axial.flow.annotation.deviceId"] = "device-1" @>
        test <@ capturedTags.ContainsKey("axial.flow.annotation.trace_id") @>
        test <@ capturedTags["axial.flow.annotation.trace_id"] = "trace-1" @>

        listener.Dispose()

    [<Fact>]
    let ``Activity.traceOn: emits application spans from the supplied source`` () =
        use applicationSource = new ActivitySource("Example.Checkout")
        use listener = new ActivityListener()
        listener.ShouldListenTo <- (fun source -> source.Name = applicationSource.Name)
        listener.Sample <- (fun _ -> ActivitySamplingResult.AllData)

        let mutable capturedSource = None
        listener.ActivityStopped <- (fun activity -> capturedSource <- Some activity.Source.Name)
        ActivitySource.AddActivityListener(listener)

        Flow.succeed 42
        |> Activity.traceOn applicationSource "checkout.submit"
        |> Flow.runSync ()
        |> ignore

        test <@ capturedSource = Some "Example.Checkout" @>

    let private captureSpans (action: unit -> unit) =
        use listener = new ActivityListener()
        listener.ShouldListenTo <- (fun source -> source.Name = "Axial")
        listener.Sample <- (fun _ -> ActivitySamplingResult.AllData)

        let stopped = ResizeArray<string * ActivityStatusCode * System.TimeSpan * Map<string, string>>()
        listener.ActivityStopped <- (fun activity ->
            let tags =
                activity.Tags
                |> Seq.map (fun kv -> kv.Key, kv.Value)
                |> Map.ofSeq

            lock stopped (fun () -> stopped.Add(activity.OperationName, activity.Status, activity.Duration, tags)))

        ActivitySource.AddActivityListener(listener)
        action ()
        lock stopped (fun () -> List.ofSeq stopped)

    [<Fact>]
    let ``Activity.trace: span lasts until asynchronous work settles`` () =
        let spans =
            captureSpans (fun () ->
                Flow.Runtime.sleep (System.TimeSpan.FromMilliseconds 80.0)
                |> Activity.trace "async-op"
                |> Flow.runSync ()
                |> ignore)

        match spans |> List.filter (fun (name, _, _, _) -> name = "async-op") with
        | [ _, status, duration, tags ] ->
            test <@ duration >= System.TimeSpan.FromMilliseconds 60.0 @>
            test <@ status = ActivityStatusCode.Ok @>
            test <@ tags["axial.flow.outcome"] = "success" @>
            test <@ tags.ContainsKey "axial.flow.fiber.id" @>
        | other -> failwithf "Expected one async-op span, got %A" other

    [<Fact>]
    let ``Activity.trace: stamps typed failures, defects, and interruptions onto the span`` () =
        let spans =
            captureSpans (fun () ->
                (Flow.fail "domain error" : Flow<unit, string, int>)
                |> Activity.trace "fail-op"
                |> Flow.runSync ()
                |> ignore

                (Flow.die (System.InvalidOperationException "defect") : Flow<unit, string, int>)
                |> Activity.trace "die-op"
                |> Flow.runSync ()
                |> ignore

                (Flow.ofExit (Exit.Failure Cause.Interrupt) : Flow<unit, string, int>)
                |> Activity.trace "interrupt-op"
                |> Flow.runSync ()
                |> ignore)

        let find name =
            spans |> List.pick (fun (n, status, _, tags) -> if n = name then Some(status, tags) else None)

        let failStatus, failTags = find "fail-op"
        test <@ failStatus = ActivityStatusCode.Error @>
        test <@ failTags["axial.flow.outcome"] = "fail" @>
        test <@ failTags["axial.flow.error"] = "domain error" @>

        let dieStatus, dieTags = find "die-op"
        test <@ dieStatus = ActivityStatusCode.Error @>
        test <@ dieTags["axial.flow.outcome"] = "die" @>
        test <@ dieTags["exception.message"] = "defect" @>

        let interruptStatus, interruptTags = find "interrupt-op"
        test <@ interruptStatus = ActivityStatusCode.Unset @>
        test <@ interruptTags["axial.flow.outcome"] = "interrupt" @>
        test <@ interruptTags["axial.flow.interrupted"] = "true" @>

    [<Fact>]
    let ``Activity.trace: composite causes carry the pretty-printed cause tree`` () =
        let composite =
            Exit.Failure(Cause.Then(Cause.Fail "first", Cause.Die(System.InvalidOperationException "second")))

        let spans =
            captureSpans (fun () ->
                (Flow.ofExit composite : Flow<unit, string, int>)
                |> Activity.trace "composite-op"
                |> Flow.runSync ()
                |> ignore)

        match spans |> List.filter (fun (name, _, _, _) -> name = "composite-op") with
        | [ _, status, _, tags ] ->
            test <@ status = ActivityStatusCode.Error @>
            test <@ tags["axial.flow.outcome"] = "die" @>
            test <@ tags["axial.flow.error"] = "first" @>
            test <@ tags.ContainsKey "axial.flow.cause" @>
            test <@ tags["axial.flow.cause"].Contains "Then" @>
        | other -> failwithf "Expected one composite-op span, got %A" other

    [<Fact>]
    let ``Activity.trace: exports typed attributes added inside its region`` () =
        let retryCount = AttributeKey.int64 "app.retry.count"
        let mutable capturedValue = None

        use listener = new ActivityListener()
        listener.ShouldListenTo <- (fun source -> source.Name = "Axial")
        listener.Sample <- (fun _ -> ActivitySamplingResult.AllData)
        listener.ActivityStopped <- (fun activity ->
            capturedValue <-
                activity.TagObjects
                |> Seq.tryPick (fun pair -> if pair.Key = "app.retry.count" then Some pair.Value else None))
        ActivitySource.AddActivityListener(listener)

        (Flow.succeed 1 : Flow<unit, string, int>)
        |> Context.withAttribute (Context.attribute retryCount 3L)
        |> Activity.trace "tagged-op"
        |> Flow.runSync ()
        |> ignore

        test <@ capturedValue = Some(box 3L) @>

    [<Fact>]
    let ``Activity.trace: nested traces both receive annotations set in the inner region`` () =
        let spans =
            captureSpans (fun () ->
                flow {
                    do! Flow.annotate "step" "inner-step" (Flow.succeed ())
                    return 1
                }
                |> Activity.trace "inner-op"
                |> Activity.trace "outer-op"
                |> Flow.runSync ()
                |> ignore)

        let tagsOf name =
            spans |> List.pick (fun (n, _, _, tags) -> if n = name then Some tags else None)

        test <@ (tagsOf "inner-op")["axial.flow.annotation.step"] = "inner-step" @>
        test <@ (tagsOf "outer-op")["axial.flow.annotation.step"] = "inner-step" @>

    let private captureSpansWithIds (action: unit -> unit) =
        use listener = new ActivityListener()
        listener.ShouldListenTo <- (fun source -> source.Name = "Axial")
        listener.Sample <- (fun _ -> ActivitySamplingResult.AllData)

        let stopped = ResizeArray<string * string * string * System.TimeSpan * Map<string, string>>()
        listener.ActivityStopped <- (fun activity ->
            let tags =
                activity.Tags
                |> Seq.map (fun kv -> kv.Key, kv.Value)
                |> Map.ofSeq

            lock stopped (fun () ->
                stopped.Add(
                    activity.OperationName,
                    activity.SpanId.ToString(),
                    activity.ParentSpanId.ToString(),
                    activity.Duration,
                    tags)))

        ActivitySource.AddActivityListener(listener)
        action ()
        lock stopped (fun () -> List.ofSeq stopped)

    [<Fact>]
    let ``FiberTelemetry.observeWithSpans: fiber spans cover fork to settle and parent to the workflow span`` () =
        let spans =
            captureSpansWithIds (fun () ->
                flow {
                    let! fiber = Flow.fork (Flow.Runtime.sleep (System.TimeSpan.FromMilliseconds 80.0) : Flow<unit, string, unit>)
                    do! Flow.join fiber
                    return "done"
                }
                |> FiberTelemetry.observeWithSpans
                |> Activity.trace "workflow-op"
                |> Flow.runSync ()
                |> ignore)

        let workflowSpanId =
            spans |> List.pick (fun (name, spanId, _, _, _) -> if name = "workflow-op" then Some spanId else None)

        match spans |> List.filter (fun (name, _, _, _, _) -> name = "axial.flow.fiber") with
        | [ _, _, parentId, duration, tags ] ->
            test <@ parentId = workflowSpanId @>
            test <@ duration >= System.TimeSpan.FromMilliseconds 60.0 @>
            test <@ tags["axial.flow.outcome"] = "success" @>
            test <@ tags["axial.flow.fiber.status"] = "Succeeded" @>
        | other -> failwithf "Expected one fiber span, got %A" other

    [<Fact>]
    let ``FiberTelemetry.observeWithSpans: a fiber that dies produces a defect-tagged fiber span`` () =
        let spans =
            captureSpansWithIds (fun () ->
                flow {
                    let! fiber = Flow.fork (Flow.die (System.InvalidOperationException "fiber defect") : Flow<unit, string, int>)
                    // Wait for the fiber to settle without letting the defect fail this workflow;
                    // interrupting before it dies would flip the outcome to interrupt.
                    do! waitForSettled fiber
                    let! _exit = Flow.interrupt fiber
                    return "done"
                }
                |> FiberTelemetry.observeWithSpans
                |> Flow.runSync ()
                |> ignore)

        match spans |> List.filter (fun (name, _, _, _, _) -> name = "axial.flow.fiber") with
        | [ _, _, _, _, tags ] ->
            test <@ tags["axial.flow.outcome"] = "die" @>
            test <@ tags["exception.message"] = "fiber defect" @>
        | other -> failwithf "Expected one fiber span, got %A" other

    [<Fact>]
    let ``FiberTelemetry.observe: records unobserved fiber defects as error spans`` () =
        let listener = new ActivityListener()
        listener.ShouldListenTo <- (fun source -> source.Name = "Axial")
        listener.Sample <- (fun _ -> ActivitySamplingResult.AllData)

        let stopped = ResizeArray<string * ActivityStatusCode * Map<string, string>>()
        listener.ActivityStopped <- (fun activity ->
            let tags =
                activity.Tags
                |> Seq.map (fun kv -> kv.Key, kv.Value)
                |> Map.ofSeq

            lock stopped (fun () -> stopped.Add(activity.OperationName, activity.Status, tags)))

        ActivitySource.AddActivityListener(listener)

        let result =
            flow {
                let! fiber = Flow.fork (Flow.die (System.InvalidOperationException "background crash") : Flow<unit, string, int>)
                do! waitForSettled fiber
                return "done"
            }
            |> FiberTelemetry.observe
            |> Flow.runSync ()

        test <@ result = Exit.Success "done" @>

        let spans = lock stopped (fun () -> List.ofSeq stopped)
        let defectSpans = spans |> List.filter (fun (name, _, _) -> name = "axial.flow.fiber.defect")
        let unobservedSpans = spans |> List.filter (fun (name, _, _) -> name = "axial.flow.fiber.unobserved_defect")

        test <@ List.length defectSpans = 1 @>
        test <@ List.length unobservedSpans = 1 @>

        match unobservedSpans with
        | [ _, status, tags ] ->
            test <@ status = ActivityStatusCode.Error @>
            test <@ tags["exception.message"] = "background crash" @>
            test <@ tags.ContainsKey "axial.flow.fiber.id" @>
        | other -> failwithf "Expected one unobserved defect span, got %A" other

        listener.Dispose()
