namespace Axial.Tests

open System.Diagnostics.Metrics
open Axial
open Axial.Telemetry
open Swensen.Unquote
open Xunit

module MetricsTests =
    [<Fact>]
    let ``Metrics add records a counter increment`` () =
        use meter = Metrics.meter "Axial.Tests.Metrics"
        let counter = Metrics.counter meter "test.counter"
        let captured = ResizeArray<int64>()

        use listener = new MeterListener()

        do
            listener.InstrumentPublished <- fun instrument l -> l.EnableMeasurementEvents instrument
            listener.SetMeasurementEventCallback<int64>(fun _ value _ _ -> captured.Add value)
            listener.Start()

        Flow.runSync () (Metrics.add counter 5L) |> ignore

        test <@ captured |> Seq.exists (fun value -> value = 5L) @>

    [<Fact>]
    let ``Metrics record records a histogram value`` () =
        use meter = Metrics.meter "Axial.Tests.Metrics"
        let histogram = Metrics.histogram meter "test.duration"
        let captured = ResizeArray<float>()

        use listener = new MeterListener()

        do
            listener.InstrumentPublished <- fun instrument l -> l.EnableMeasurementEvents instrument
            listener.SetMeasurementEventCallback<float>(fun _ value _ _ -> captured.Add value)
            listener.Start()

        Flow.runSync () (Metrics.record histogram 2.5) |> ignore

        test <@ captured |> Seq.exists (fun value -> value = 2.5) @>

    [<Fact>]
    let ``Metrics gauge is observable and reads the supplied measure`` () =
        use meter = Metrics.meter "Axial.Tests.Metrics"
        let gauge = Metrics.gauge meter "test.level" (fun () -> 3.0)
        let observed = ResizeArray<float>()

        use listener = new MeterListener()

        do
            listener.InstrumentPublished <- fun instrument l -> l.EnableMeasurementEvents instrument
            listener.SetMeasurementEventCallback<float>(fun _ value _ _ -> observed.Add value)
            listener.Start()
            listener.RecordObservableInstruments()

        test <@ observed |> Seq.exists (fun value -> value = 3.0) @>
