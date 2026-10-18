namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

module WorkflowCallbackStreamTests =
    let private run workflow = Flow.runSync (TestSupport.clockEnv ()) workflow
    let private disposable (release: unit -> unit) = { new IDisposable with member _.Dispose() = release () }

    [<Fact>]
    let ``values from another thread arrive in order`` () =
        let source = FlowStream.fromCallback 128 (fun emit ->
            Flow.delay (fun () ->
                Task.Run(fun () ->
                    for i in 1..100 do emit.Value i |> ignore
                    emit.End()) |> ignore
                Flow.ok (disposable ignore)))
        test <@ run (FlowStream.runCollect source) = Exit.Success [ 1..100 ] @>

    [<Fact>]
    let ``take releases exactly once`` () =
        let mutable released = 0
        let source = FlowStream.fromCallback 4 (fun emit ->
            Flow.delay (fun () ->
                emit.Value 1 |> ignore
                emit.Value 2 |> ignore
                Flow.ok (disposable (fun () -> Interlocked.Increment(&released) |> ignore))))
        test <@ run (source |> FlowStream.take 2 |> FlowStream.runCollect) = Exit.Success [ 1; 2 ] @>
        test <@ released = 1 @>

    [<Fact>]
    let ``end and failure release and ignore late emits`` () =
        for fail in [ false; true ] do
            let mutable released = 0
            let mutable accepted = true
            let source = FlowStream.fromCallback 4 (fun emit ->
                Flow.delay (fun () ->
                    emit.Value 1 |> ignore
                    if fail then emit.Fail "bad" else emit.End()
                    accepted <- emit.Value 2
                    emit.End()
                    emit.Fail "late"
                    Flow.ok (disposable (fun () -> released <- released + 1))))
            let result = run (FlowStream.runCollect source)
            if fail then test <@ result = Exit.Failure(Cause.Fail "bad") @>
            else test <@ result = Exit.Success [ 1 ] @>
            test <@ not accepted && released = 1 @>

    [<Fact>]
    let ``overflow drops newest or fails stream`` () =
        for overflow in [ CallbackOverflow.Dropping; CallbackOverflow.Fail "full" ] do
            let accepted = ResizeArray<bool>()
            let source = FlowStream.fromCallbackWith 1 overflow (fun emit ->
                Flow.delay (fun () ->
                    accepted.Add(emit.Value 1)
                    accepted.Add(emit.Value 2)
                    emit.End()
                    Flow.ok (disposable ignore)))
            let result = run (FlowStream.runCollect source)
            test <@ List.ofSeq accepted = [ true; false ] @>
            match overflow with
            | CallbackOverflow.Dropping -> test <@ result = Exit.Success [ 1 ] @>
            | CallbackOverflow.Fail _ -> test <@ result = Exit.Failure(Cause.Fail "full") @>

    [<Fact>]
    let ``fail overflow delivers queued prefix`` () =
        let delivered = ResizeArray<int>()
        let source = FlowStream.fromCallbackWith 2 (CallbackOverflow.Fail "full") (fun emit ->
            Flow.delay (fun () ->
                for i in 1..3 do emit.Value i |> ignore
                Flow.ok (disposable ignore)))
        let result = source |> FlowStream.runFold (fun () value -> delivered.Add value) () |> run
        test <@ result = Exit.Failure(Cause.Fail "full") @>
        test <@ List.ofSeq delivered = [ 1; 2 ] @>

    [<Fact>]
    let ``failed subscription closes its scope`` () =
        let mutable released = false
        let source: FlowStream<_, string, int> = FlowStream.fromCallback 4 (fun _ ->
            Flow.scopeFinalizer (Flow.delay (fun () -> released <- true; Flow.succeed ()))
            |> Flow.bind (fun () -> Flow.fail "subscribe failed"))
        test <@ run (FlowStream.runCollect source) = Exit.Failure(Cause.Fail "subscribe failed") @>
        test <@ released @>

    [<Fact>]
    let ``concurrent producers preserve each producer order`` () =
        let errors = System.Collections.Concurrent.ConcurrentQueue<exn>()
        let source = FlowStream.fromCallback 4000 (fun emit ->
            Flow.delay (fun () ->
                use barrier = new Barrier(4)
                let producers =
                    [| for producer in 0..3 ->
                        Task.Factory.StartNew((fun () ->
                            try
                                barrier.SignalAndWait() |> ignore
                                for i in 0..999 do
                                    if not (emit.Value(producer * 1000 + i)) then
                                        failwith "unexpected rejection"
                            with ex -> errors.Enqueue ex), TaskCreationOptions.LongRunning) |]
                Task.WaitAll producers
                emit.End()
                Flow.ok (disposable ignore)))
        match run (FlowStream.runCollect source) with
        | Exit.Success values ->
            test <@ values.Length = 4000 @>
            test <@ List.sort values = [ 0..3999 ] @>
            for producer in 0..3 do
                test <@ values |> List.filter (fun value -> value / 1000 = producer) = [ producer * 1000 .. producer * 1000 + 999 ] @>
        | other -> failwithf "Unexpected exit: %A" other
        test <@ errors.IsEmpty @>

    [<Fact>]
    let ``end racing values never accepts after returning`` () =
        for _ in 1..100 do
            let errors = System.Collections.Concurrent.ConcurrentQueue<exn>()
            let mutable lateAccepted = false
            let source = FlowStream.fromCallback 1024 (fun emit ->
                Flow.delay (fun () ->
                    use barrier = new Barrier(2)
                    let producer = Task.Run(fun () ->
                        try
                            barrier.SignalAndWait() |> ignore
                            for i in 1..100 do emit.Value i |> ignore
                        with ex -> errors.Enqueue ex)
                    let ending = Task.Run(fun () ->
                        try
                            barrier.SignalAndWait() |> ignore
                            emit.End()
                            lateAccepted <- emit.Value 101
                        with ex -> errors.Enqueue ex)
                    Task.WaitAll(producer, ending)
                    Flow.ok (disposable ignore)))
            match run (FlowStream.runCollect source) with
            | Exit.Success values -> test <@ not (List.contains 101 values) @>
            | other -> failwithf "Unexpected exit: %A" other
            test <@ not lateAccepted && errors.IsEmpty @>

    [<Fact>]
    let ``interruption releases subscription`` () =
        use started = new ManualResetEventSlim(false)
        use released = new ManualResetEventSlim(false)
        use cancellation = new CancellationTokenSource()
        let source = FlowStream.fromCallback 1 (fun _ ->
            Flow.delay (fun () -> started.Set(); Flow.ok (disposable released.Set)))
        let running = Task.Run(fun () -> Flow.runSyncWithToken (TestSupport.clockEnv ()) cancellation.Token (FlowStream.runDrain source))
        test <@ started.Wait(5000) @>
        cancellation.Cancel()
        test <@ released.Wait(5000) @>
        test <@ running.Wait(5000) @>
