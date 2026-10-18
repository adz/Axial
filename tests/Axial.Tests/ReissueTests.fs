namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

module ReissueTests =
    [<Fact>]
    let ``Reissue wakes interrupt three runs without retrying`` () =
        let mutable starts = 0
        let mutable stopped = 0
        let mutable failures = 0
        let workflow : Flow<ClockEnvironment, string, int * int * int * int> =
            flow {
                let! wakes = Queue.unbounded ()
                let! started = Queue.unbounded ()
                let! finish = Deferred.make<ClockEnvironment, string, int> ()
                let work =
                    Flow.delay (fun () ->
                        starts <- starts + 1
                        flow {
                            do! started |> Queue.offer starts |> Flow.ignore
                            return! Deferred.await finish
                        })
                    |> Flow.ensuring (Flow.delay (fun () -> stopped <- stopped + 1; Flow.ok ()))
                    |> Flow.mapError (fun error -> failures <- failures + 1; error)
                    |> Flow.retry (Schedule.recurs 10)
                let! running = FlowStream.reissueOnWake (FlowStream.fromDequeue wakes) work |> Flow.fork
                for index in 1..3 do
                    let! observed = Dequeue.take started
                    if observed <> index then failwithf "expected run %d, got %d" index observed
                    do! wakes |> Queue.offer () |> Flow.ignore
                let! _fourth = Dequeue.take started
                do! Deferred.succeed 42 finish |> Flow.ignore
                let! value = Fiber.join running
                return value, starts, stopped, failures
            }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(42, 4, 4, 0) @>

    [<Fact>]
    let ``Reissue propagates a typed failure to retry`` () =
        let mutable failures = 0
        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! wakes = Queue.unbounded ()
                let work = Flow.fail "broken" |> Flow.mapError (fun error -> failures <- failures + 1; error)
                return! FlowStream.reissueOnWake (FlowStream.fromDequeue wakes) (work |> Flow.retry (Schedule.recurs 0))
            }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Failure(Cause.Fail "broken") @>
        test <@ failures = 1 @>

    [<Fact>]
    let ``Reissue timeout closes the current run`` () =
        let mutable stopped = 0
        let workflow : Flow<ClockEnvironment, string, _> =
            flow {
                let! wakes = Queue.unbounded ()
                let! gate = Deferred.make<ClockEnvironment, string, int> ()
                let work = Deferred.await gate |> Flow.ensuring (Flow.delay (fun () -> stopped <- stopped + 1; Flow.ok ()))
                return! FlowStream.reissueOnWake (FlowStream.fromDequeue wakes) work
                        |> Flow.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
            }
        let result = Flow.runSync (TestSupport.clockEnv ()) workflow
        test <@ stopped = 1 @>
        test <@ result <> Exit.Success 0 @>

    [<Fact>]
    let ``Reissue returns initial result without wakes`` () =
        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! wakes = Queue.unbounded ()
                return! FlowStream.reissueOnWake (FlowStream.fromDequeue wakes) (Flow.ok 17)
            }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 17 @>
