namespace Axial.Flow.Telemetry

open System.Diagnostics
open Axial.Flow

[<RequireQualifiedAccess>]
module Activity =
    /// <summary>The activity source for Axial Flow tracing.</summary>
    let source = new ActivitySource("Axial.Flow")

    /// <summary>
    /// Wraps a flow in a new activity and automatically maps metadata traits from the environment to tags.
    /// </summary>
    /// <param name="name">The name of the activity.</param>
    /// <param name="flow">The flow to trace.</param>
    /// <returns>A flow that executes within the activity span.</returns>
    let trace (name: string) (sourceFlow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun env cancellationToken ->
            use activity = source.StartActivity(name)
            
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

            let tracedFlow =
                let sourceWithExistingAnnotations =
                    flow {
                        let! annotations = Flow.Runtime.annotations

                        if not (isNull activity) then
                            for KeyValue(name, value) in annotations do
                                activity.SetTag($"axial.flow.annotation.{name}", value) |> ignore

                        return! sourceFlow
                    }

                if isNull activity then
                    sourceWithExistingAnnotations
                else
                    sourceWithExistingAnnotations
                    |> Flow.withAnnotationSink (fun name value ->
                        activity.SetTag($"axial.flow.annotation.{name}", value) |> ignore)

            let (Flow operation) = tracedFlow
            operation env cancellationToken
        )

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

    let private tagDefect (activity: Activity) (defect: exn) =
        activity.SetStatus(ActivityStatusCode.Error, defect.Message) |> ignore
        activity.SetTag("exception.type", defect.GetType().FullName) |> ignore
        activity.SetTag("exception.message", defect.Message) |> ignore
        activity.SetTag("exception.stacktrace", string defect) |> ignore

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
                            tagDefect activity exn
                    | None -> ()
            OnUnobservedDefect =
                fun metadata defect ->
                    use activity = Activity.source.StartActivity("axial.flow.fiber.unobserved_defect")

                    if not (isNull activity) then
                        metadata |> Option.iter (tagMetadata activity)
                        tagDefect activity defect }

    /// <summary>Installs the telemetry fiber observer on a flow, typically once at the application edge.</summary>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose forked fibers report defects through the <c>Axial.Flow</c> activity source.</returns>
    let observe (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow.withFiberObserver observer flow
