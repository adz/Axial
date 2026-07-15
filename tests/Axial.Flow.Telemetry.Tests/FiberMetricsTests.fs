namespace Axial.Tests

open System.Collections.Generic
open System.Diagnostics
open System.Diagnostics.Metrics
open Axial.Flow
open Axial.Flow.Telemetry
open Swensen.Unquote
open Xunit

module FiberMetricsTests =
    /// Collects measurements from the Axial.Flow meter for the duration of a test.
    type private MetricCapture() =
        let gate = obj()
        let measurements = ResizeArray<string * int64 * Map<string, string>>()

        let listener = new MeterListener()

        do
            listener.InstrumentPublished <-
                fun instrument metricListener ->
                    if instrument.Meter.Name = "Axial.Flow" then
                        metricListener.EnableMeasurementEvents instrument

            listener.SetMeasurementEventCallback<int64>(fun instrument value tags _ ->
                let tagMap =
                    tags.ToArray()
                    |> Array.map (fun tag -> tag.Key, string tag.Value)
                    |> Map.ofArray

                lock gate (fun () -> measurements.Add(instrument.Name, value, tagMap)))

            listener.Start()

        member _.Total(name: string) =
            lock gate (fun () ->
                measurements
                |> Seq.filter (fun (instrument, _, _) -> instrument = name)
                |> Seq.sumBy (fun (_, value, _) -> value))

        member _.Tagged(name: string) =
            lock gate (fun () ->
                measurements
                |> Seq.filter (fun (instrument, _, _) -> instrument = name)
                |> Seq.map (fun (_, value, tags) -> value, tags)
                |> List.ofSeq)

        interface System.IDisposable with
            member _.Dispose() = listener.Dispose()

    let rec private waitForSettled (fiber: Fiber<'error, 'value>) : Flow<unit, 'testError, unit> =
        flow {
            if fiber.Metadata.Status = FiberStatus.Running then
                do! Flow.Runtime.sleep (System.TimeSpan.FromMilliseconds 5.0)
                return! waitForSettled fiber
        }

    let private waitUntil (condition: unit -> bool) =
        let mutable remaining = 500

        while not (condition ()) && remaining > 0 do
            remaining <- remaining - 1
            System.Threading.Thread.Sleep 10

    [<Fact>]
    let ``fiber metrics count starts, settles with status, and net live fibers`` () =
        use capture = new MetricCapture()

        let result =
            flow {
                let! succeeding = Flow.fork (Flow.succeed 1)
                let! failing = Flow.forkDetached (Flow.fail "boom" : Flow<unit, string, int>)
                let! sleeper = Flow.fork (Flow.Runtime.sleep (System.TimeSpan.FromSeconds 30.0))
                let! _ = Flow.join succeeding
                do! waitForSettled failing
                let! _ = Flow.interrupt sleeper
                return ()
            }
            |> FiberMetrics.observe
            |> Flow.runSync ()

        test <@ result = Exit.Success() @>
        waitUntil (fun () -> capture.Total "axial.flow.fibers.settled" = 3L)

        test <@ capture.Total "axial.flow.fibers.started" = 3L @>
        test <@ capture.Total "axial.flow.fibers.settled" = 3L @>
        test <@ capture.Total "axial.flow.fibers.live" = 0L @>

        let settledStatuses =
            capture.Tagged "axial.flow.fibers.settled"
            |> List.choose (fun (_, tags) -> Map.tryFind "axial.flow.fiber.status" tags)
            |> List.sort

        test <@ settledStatuses = [ "Failed"; "Interrupted"; "Succeeded" ] @>

    [<Fact>]
    let ``dump event lands on the current activity`` () =
        let listener = new ActivityListener()
        listener.ShouldListenTo <- fun source -> source.Name = "Axial.Flow" || source.Name = "FiberMetricsTests"
        listener.Sample <- fun _ -> ActivitySamplingResult.AllData
        ActivitySource.AddActivityListener listener
        use _ = listener

        use source = new ActivitySource("FiberMetricsTests")
        use activity = source.StartActivity("request")

        let registry = FiberRegistry()
        FiberDumpTelemetry.record registry

        let events = activity.Events |> List.ofSeq
        test <@ events |> List.map _.Name = [ "axial.flow.fiber.dump" ] @>

        let tags =
            events
            |> List.collect (fun event -> event.Tags |> List.ofSeq)
            |> List.map (fun (KeyValue(key, value)) -> key, string value)
            |> Map.ofList

        test <@ tags["axial.flow.fibers.live"] = "0" @>
        test <@ (tags["axial.flow.fiber.dump.tree"]).StartsWith "Fiber dump @" @>
