namespace Axial.Tests

open System.Diagnostics.Metrics
open Axial
open Axial.Telemetry
open Swensen.Unquote
open Xunit

module QueueMetricsTests =
    /// Collects the Axial meter's observable queue instruments, tagged by queue name, each time it is sampled.
    type private QueueCapture() =
        let gate = obj ()
        let latest = System.Collections.Generic.Dictionary<string * string, int64>()
        let listener = new MeterListener()

        do
            listener.InstrumentPublished <-
                fun instrument metricListener ->
                    if instrument.Meter.Name = "Axial" && instrument.Name.StartsWith "axial.queue." then
                        metricListener.EnableMeasurementEvents instrument

            listener.SetMeasurementEventCallback<int64>(fun instrument value tags _ ->
                let queueName =
                    tags.ToArray()
                    |> Array.tryFind (fun tag -> tag.Key = "axial.queue.name")
                    |> Option.map (fun tag -> string tag.Value)
                    |> Option.defaultValue ""

                lock gate (fun () -> latest[(instrument.Name, queueName)] <- value))

            listener.Start()

        /// Samples the observable instruments and returns the value reported for one queue, if any.
        member _.Read(instrument: string, queueName: string) =
            lock gate (fun () -> latest.Clear())
            listener.RecordObservableInstruments()

            lock gate (fun () ->
                match latest.TryGetValue((instrument, queueName)) with
                | true, value -> Some value
                | _ -> None)

        interface System.IDisposable with
            member _.Dispose() = listener.Dispose()

    [<Fact>]
    let ``QueueMetrics reports a queue's depth and losses while its scope is open`` () =
        use capture = new QueueCapture()

        let workflow : Flow<unit, Never, (int64 option list) * int64 option> =
            flow {
                let! readings =
                    flow {
                        let! (display: Queue<int>) = Queue.sliding 2
                        do! display |> QueueMetrics.observe "queue-metrics-test-display"
                        do! display |> Queue.offerAll [ 1..5 ] |> Flow.ignore

                        return
                            [ for instrument in [ "axial.queue.size"; "axial.queue.capacity"; "axial.queue.accepted"; "axial.queue.evicted"; "axial.queue.dropped" ] ->
                                  capture.Read(instrument, "queue-metrics-test-display") ]
                    }
                    |> Flow.scoped

                let afterScope = capture.Read("axial.queue.size", "queue-metrics-test-display")
                return readings, afterScope
            }

        test <@ Flow.runSync () workflow = Exit.Success([ Some 2L; Some 2L; Some 5L; Some 3L; Some 0L ], None) @>
