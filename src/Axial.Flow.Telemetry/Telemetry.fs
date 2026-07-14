namespace Axial.Flow.Telemetry

open System.Diagnostics
open Axial.Flow
open Axial.Flow.Telemetry.Shared

/// The `Activity` adapter for the shared span vocabulary in `Axial.Flow.Telemetry.Shared`.
module internal Tags =
    let writer (activity: Activity) : SpanWriter =
        { SetTag = fun name value -> activity.SetTag(name, value) |> ignore
          SetStatus =
            function
            | SpanStatusOutcome.Ok -> activity.SetStatus(ActivityStatusCode.Ok) |> ignore
            | SpanStatusOutcome.Error "" -> activity.SetStatus(ActivityStatusCode.Error) |> ignore
            | SpanStatusOutcome.Error message -> activity.SetStatus(ActivityStatusCode.Error, message) |> ignore
          DefectTypeName = fun defect -> defect.GetType().FullName }

    let tagDefect (activity: Activity) (defect: exn) =
        SpanConventions.tagDefect (writer activity) defect

    let stampExit (renderError: 'error -> string) (activity: Activity) (exit: Exit<'value, 'error>) =
        SpanConventions.stampExit renderError (writer activity) exit

[<RequireQualifiedAccess>]
module Activity =
    /// <summary>The activity source for Axial Flow tracing.</summary>
    let source = new ActivitySource("Axial.Flow")

    /// <summary>
    /// Wraps a flow in a new activity that spans the workflow's execution, maps metadata traits from the
    /// environment to tags, and stamps the final exit onto the span.
    /// </summary>
    /// <remarks>
    /// The activity stops when the workflow settles, so span duration covers asynchronous work. On settle the
    /// span receives <c>ActivityStatusCode</c> from the exit, <c>axial.flow.outcome</c>
    /// (<c>success</c>/<c>fail</c>/<c>die</c>/<c>interrupt</c>), <c>axial.flow.error</c> for typed errors,
    /// OpenTelemetry <c>exception.*</c> tags for defects, <c>axial.flow.interrupted</c> for cancellation, and
    /// <c>axial.flow.cause</c> with the pretty-printed tree for composite causes. Typed errors are rendered
    /// with <c>string</c>; use <c>traceWith</c> to supply a custom renderer.
    /// </remarks>
    /// <param name="name">The name of the activity.</param>
    /// <param name="flow">The flow to trace.</param>
    /// <returns>A flow that executes within the activity span.</returns>
    let traceWith
        (renderError: 'error -> string)
        (name: string)
        (sourceFlow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun env cancellationToken ->
            let activity = source.StartActivity(name)

            if not (isNull activity) then
                match box env with
                | :? IHasRequestId as req -> activity.SetTag("axial.flow.request_id", req.RequestId) |> ignore
                | _ -> ()

                match box env with
                | :? IHasCorrelationId as corr ->
                    match corr.CorrelationId with
                    | Some id -> activity.SetTag("axial.flow.correlation_id", id) |> ignore
                    | None -> ()
                | _ -> ()

                match box env with
                | :? IHasTenantId as t ->
                    match t.TenantId with
                    | Some id -> activity.SetTag("axial.flow.tenant_id", id) |> ignore
                    | None -> ()
                | _ -> ()

                match box env with
                | :? IHasTelemetryTags as tagged ->
                    for tagName, tagValue in tagged.TelemetryTags do
                        activity.SetTag(tagName, tagValue) |> ignore
                | _ -> ()

            let tracedFlow =
                let sourceWithExistingAnnotations =
                    flow {
                        let! fiberId = Flow.Runtime.fiberId
                        let! annotations = Flow.Runtime.annotations

                        if not (isNull activity) then
                            activity.SetTag("axial.flow.fiber.id", string fiberId.Value) |> ignore

                            for KeyValue(name, value) in annotations do
                                activity.SetTag($"axial.flow.annotation.{name}", value) |> ignore

                        return! sourceFlow
                    }

                if isNull activity then
                    sourceWithExistingAnnotations
                else
                    sourceWithExistingAnnotations
                    |> Flow.addAnnotationSink (fun name value ->
                        activity.SetTag($"axial.flow.annotation.{name}", value) |> ignore)

            let (Flow operation) = tracedFlow

            if isNull activity then
                operation env cancellationToken
            else
                // The activity must stop when the execution settles, not when it starts: stamp the
                // exit (or thrown error) onto the span, then dispose so durations cover async work.
                let settle (exit: Exit<'value, 'error>) =
                    Tags.stampExit renderError activity exit
                    activity.Dispose()

                Platform.tryExecution
                    (fun () ->
                        operation env cancellationToken
                        |> Execution.fold
                            (fun value ->
                                settle (Exit.Success value)
                                Execution.ofValue value)
                            (fun cause ->
                                settle (Exit.Failure cause)
                                Execution.ofCause cause))
                    (fun error ->
                        settle (Exit.Failure(Execution.causeOfException error))
                        raise error))

    /// <summary>
    /// Wraps a flow in a new activity that spans the workflow's execution. Typed errors are rendered onto the
    /// span with <c>string</c>; see <c>traceWith</c> for a custom renderer.
    /// </summary>
    /// <param name="name">The name of the activity.</param>
    /// <param name="flow">The flow to trace.</param>
    /// <returns>A flow that executes within the activity span.</returns>
    let trace (name: string) (sourceFlow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        traceWith (fun error -> string (box error)) name sourceFlow

/// <summary>Telemetry wiring for runtime fiber-lifecycle observation.</summary>
[<RequireQualifiedAccess>]
module FiberTelemetry =
    let private tagMetadata (activity: Activity) (metadata: FiberMetadata) =
        activity.SetTag("axial.flow.fiber.id", string metadata.Id.Value) |> ignore

        match metadata.ParentId with
        | Some parentId -> activity.SetTag("axial.flow.fiber.parent_id", string parentId.Value) |> ignore
        | None -> ()

        activity.SetTag("axial.flow.fiber.started_at", metadata.StartedAt.ToString "O") |> ignore
        activity.SetTag("axial.flow.fiber.status", string metadata.Status) |> ignore

    /// <summary>
    /// A fiber observer that records fiber defects on the <c>Axial.Flow</c> activity source: every fiber that
    /// settles with a defect produces an <c>axial.flow.fiber.defect</c> error span, and every defect the
    /// runtime proves unobservable (a discarded fork handle, or a race/timeout loser) produces an
    /// <c>axial.flow.fiber.unobserved_defect</c> error span.
    /// </summary>
    let observer : FiberObserver =
        { FiberObserver.none with
            OnEnd =
                fun metadata defect ->
                    match defect with
                    | Some exn ->
                        use activity = Activity.source.StartActivity("axial.flow.fiber.defect")

                        if not (isNull activity) then
                            tagMetadata activity metadata
                            Tags.tagDefect activity exn
                    | None -> ()
            OnUnobservedDefect =
                fun metadata defect ->
                    use activity = Activity.source.StartActivity("axial.flow.fiber.unobserved_defect")

                    if not (isNull activity) then
                        metadata |> Option.iter (tagMetadata activity)
                        Tags.tagDefect activity defect }

    /// <summary>Installs the telemetry fiber observer on a flow, typically once at the application edge.</summary>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose forked fibers report defects through the <c>Axial.Flow</c> activity source.</returns>
    let observe (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow.withFiberObserver observer flow

    // Fiber spans opened at fork and closed at settle. Keyed weakly by the fiber's metadata record,
    // which both hooks receive; entries become collectable with the fiber itself.
    let private fiberSpans =
        System.Runtime.CompilerServices.ConditionalWeakTable<FiberMetadata, Activity>()

    /// <summary>
    /// A fiber observer that gives every forked fiber a real <c>axial.flow.fiber</c> span: opened at the fork
    /// site (so the parent is the workflow span that forked it), closed when the fiber settles, and stamped
    /// with the fiber's status and any defect. Unobservable defects still produce a linked
    /// <c>axial.flow.fiber.unobserved_defect</c> span.
    /// </summary>
    /// <remarks>
    /// Span-per-fiber is opt-in: a hot path forking many fibers can stay on <c>observer</c>, which records
    /// defect spans only. The forking workflow's ambient activity is restored immediately after the fiber
    /// span is opened, so code inside the fiber parents to the workflow span, not the fiber span.
    /// </remarks>
    let observerWithSpans : FiberObserver =
        {
            OnStart =
                fun metadata ->
                    let previous = System.Diagnostics.Activity.Current
                    let activity = Activity.source.StartActivity("axial.flow.fiber")
                    System.Diagnostics.Activity.Current <- previous

                    if not (isNull activity) then
                        tagMetadata activity metadata
                        fiberSpans.Add(metadata, activity)
            OnEnd =
                fun metadata defect ->
                    match fiberSpans.TryGetValue metadata with
                    | true, activity ->
                        SpanConventions.stampFiberEnd (Tags.writer activity) metadata.Status defect
                        activity.Dispose()
                    | _ -> ()
            OnUnobservedDefect =
                fun metadata defect ->
                    let links =
                        match metadata with
                        | Some m ->
                            match fiberSpans.TryGetValue m with
                            | true, fiberActivity -> [ ActivityLink fiberActivity.Context ]
                            | _ -> []
                        | None -> []

                    use activity =
                        Activity.source.StartActivity(
                            "axial.flow.fiber.unobserved_defect",
                            ActivityKind.Internal,
                            ActivityContext(),
                            links = links)

                    if not (isNull activity) then
                        metadata |> Option.iter (tagMetadata activity)
                        Tags.tagDefect activity defect
        }

    /// <summary>
    /// Installs the span-per-fiber telemetry observer on a flow: every forked fiber becomes an
    /// <c>axial.flow.fiber</c> span covering fork to settle. See <c>observerWithSpans</c>.
    /// </summary>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose forked fibers each produce a span on the <c>Axial.Flow</c> activity source.</returns>
    let observeWithSpans (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow.withFiberObserver observerWithSpans flow
