namespace FsFlow.Tests

open System.Diagnostics
open FsFlow
open FsFlow.Runtime.Telemetry
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
        listener.ShouldListenTo <- (fun source -> source.Name = "FsFlow")
        listener.Sample <- (fun _ -> ActivitySamplingResult.AllData)
        
        let mutable capturedTags = Map.empty
        listener.ActivityStopped <- (fun activity ->
            capturedTags <- 
                activity.Tags 
                |> Seq.map (fun kv -> kv.Key, kv.Value)
                |> Map.ofSeq)

        ActivitySource.AddActivityListener(listener)

        let workflow = 
            Activity.trace "test-op" (flow { return 42 })

        let result = Flow.runSync env workflow
        
        test <@ result = Exit.Success 42 @>
        test <@ capturedTags.ContainsKey("fsflow.request_id") @>
        test <@ capturedTags["fsflow.request_id"] = "req-123" @>
        test <@ capturedTags.ContainsKey("fsflow.correlation_id") @>
        test <@ capturedTags["fsflow.correlation_id"] = "corr-456" @>
        
        listener.Dispose()
