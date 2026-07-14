namespace Axial.Tests

open System.Diagnostics
open Axial.Flow
open Axial.Flow.Telemetry
open Swensen.Unquote
open Xunit

type MockTelemetryEnv =
    {
        RequestId: string
        CorrelationId: string option
    }
    interface IHasRequestId with member this.RequestId = this.RequestId
    interface IHasCorrelationId with member this.CorrelationId = this.CorrelationId

module TelemetryTests =
    [<Fact>]
    let ``Activity.trace: automatically maps metadata traits to tags`` () =
        let env = { RequestId = "req-123"; CorrelationId = Some "corr-456" }
        
        let listener = new ActivityListener()
        listener.ShouldListenTo <- (fun source -> source.Name = "Axial.Flow")
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

        let result = Flow.runSync env workflow
        
        test <@ result = Exit.Success 42 @>
        test <@ capturedTags.ContainsKey("axial.flow.request_id") @>
        test <@ capturedTags["axial.flow.request_id"] = "req-123" @>
        test <@ capturedTags.ContainsKey("axial.flow.correlation_id") @>
        test <@ capturedTags["axial.flow.correlation_id"] = "corr-456" @>
        test <@ capturedTags.ContainsKey("axial.flow.annotation.deviceId") @>
        test <@ capturedTags["axial.flow.annotation.deviceId"] = "device-1" @>
        test <@ capturedTags.ContainsKey("axial.flow.annotation.trace_id") @>
        test <@ capturedTags["axial.flow.annotation.trace_id"] = "trace-1" @>

        listener.Dispose()

    [<Fact>]
    let ``FiberTelemetry.observe: records unobserved fiber defects as error spans`` () =
        let listener = new ActivityListener()
        listener.ShouldListenTo <- (fun source -> source.Name = "Axial.Flow")
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
                let! _fiber = Flow.fork (Flow.die (System.InvalidOperationException "background crash") : Flow<unit, string, int>)
                do! Flow.Runtime.sleep (System.TimeSpan.FromMilliseconds 50.0)
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
