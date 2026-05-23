namespace FsFlow.Runtime.Telemetry

open System.Diagnostics
open FsFlow

[<RequireQualifiedAccess>]
module Activity =
    /// <summary>The activity source for FsFlow tracing.</summary>
    let source = new ActivitySource("FsFlow")

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
                | :? IHasRequestId as req -> activity.SetTag("fsflow.request_id", req.RequestId) |> ignore
                | _ -> ()
                
                match box env with
                | :? IHasCorrelationId as corr -> 
                    match corr.CorrelationId with
                    | Some id -> activity.SetTag("fsflow.correlation_id", id) |> ignore
                    | None -> ()
                | _ -> ()

                match box env with
                | :? IHasTenantId as t -> 
                    match t.TenantId with
                    | Some id -> activity.SetTag("fsflow.tenant_id", id) |> ignore
                    | None -> ()
                | _ -> ()

            let tracedFlow =
                let sourceWithExistingAnnotations =
                    flow {
                        let! annotations = Flow.Runtime.annotations

                        if not (isNull activity) then
                            for KeyValue(name, value) in annotations do
                                activity.SetTag($"fsflow.annotation.{name}", value) |> ignore

                        return! sourceFlow
                    }

                if isNull activity then
                    sourceWithExistingAnnotations
                else
                    sourceWithExistingAnnotations
                    |> Flow.withAnnotationSink (fun name value ->
                        activity.SetTag($"fsflow.annotation.{name}", value) |> ignore)

            let (Flow operation) = tracedFlow
            operation env cancellationToken
        )
