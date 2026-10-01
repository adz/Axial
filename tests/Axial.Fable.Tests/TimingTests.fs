/// Sleeps, timeouts, and schedules on JavaScript timers.
module Axial.Fable.Tests.TimingTests

open System
open Axial
open Axial.PlatformService
open Axial.Fable.Tests.Harness

let private ms (value: float) = TimeSpan.FromMilliseconds value

let private elapsedSince started = nowMilliseconds () - started

let tests : Test list =
    [ test "BaseRuntime record dispatches same-named services" (flow {
          let logged = ResizeArray<string>()
          let runtime =
              { BaseRuntime.liveValue with
                  Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
                  Log = Log.fromSink (fun _ message -> logged.Add message)
                  Random = Random.fromValue 7
                  Guid = Guid.fromValue global.System.Guid.Empty
                  EnvironmentVariables = EnvironmentVariables.fromPairs [ "REGION", "eu" ] }

          let services : Flow<BaseRuntime, Never, int * int * global.System.Guid * string option> =
              flow {
                  let! now = Clock.now
                  let! number = Random.next
                  let! id = Guid.newGuid
                  let! region = EnvironmentVariables.tryGet "REGION"
                  do! Log.info "seen"
                  return now.Year, number, id, region
              }

          let! actual = services |> Flow.localEnv (fun (_: ClockEnvironment) -> runtime)
          return
              [ equal "services" (2026, 7, global.System.Guid.Empty, Some "eu") actual
                equal "log" [ "seen" ] (List.ofSeq logged) ]
      })

      test "Flow.sleep waits at least its delay" (flow {
          let started = nowMilliseconds ()
          do! Flow.sleep (ms 50.0)
          let elapsed = elapsedSince started
          return [ isTrue "waited about 50 ms" (elapsed >= 45.0 && elapsed < 1000.0) ]
      })

      test "Flow.timeout fails with the timeout error and interrupts the operation first" (flow {
          let loserInterrupted = ref false
          let started = nowMilliseconds ()

          let! exit =
              Flow.sleep (TimeSpan.FromSeconds 10.0)
              |> Flow.onInterrupt (Flow.delay (fun () -> loserInterrupted.Value <- true; Flow.ok ()))
              |> Flow.timeout (ms 100.0) "timed out"
              |> exitOf

          return
              [ equal "outcome" (Exit.Failure(Cause.Fail "timed out")) exit
                isTrue "the operation was interrupted before the timeout returned" loserInterrupted.Value
                isTrue "returned promptly" (elapsedSince started < 2000.0) ]
      })

      test "Flow.timeout returns the operation's value when it finishes first" (flow {
          let! exit =
              Flow.sleep (ms 10.0)
              |> Flow.map (fun () -> "finished")
              |> Flow.timeout (TimeSpan.FromSeconds 5.0) "timed out"
              |> exitOf

          return [ equal "outcome" (Exit.Success "finished") exit ]
      })

      test "Flow.retry waits the schedule's delay between attempts" (flow {
          let attempts = ref 0
          let started = nowMilliseconds ()

          let! exit =
              Flow.delay (fun () ->
                  attempts.Value <- attempts.Value + 1
                  Flow.fail "transient")
              |> Flow.retry (Schedule.spaced (ms 20.0) |> Schedule.intersect (Schedule.recurs 2))
              |> exitOf

          return
              [ equal "outcome" (Exit.Failure(Cause.Fail "transient") : Exit<unit, string>) exit
                equal "attempts" 3 attempts.Value
                isTrue "waited two delays" (elapsedSince started >= 35.0) ]
      })

      test "FlowStream.fromSchedule emits a fixed-rate schedule's ticks" (flow {
          let started = nowMilliseconds ()

          let! ticks =
              Schedule.fixedRate (ms 20.0)
              |> FlowStream.fromSchedule
              |> FlowStream.take 3
              |> FlowStream.runCollect

          return
              [ equal "ticks" [ 0; 1; 2 ] ticks
                isTrue "took about three periods" (elapsedSince started >= 55.0) ]
      }) ]
