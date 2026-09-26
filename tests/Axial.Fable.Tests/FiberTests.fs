/// Forked fibers, their scopes, graceful stop, and lifecycle hooks on the JavaScript runtime.
module Axial.Fable.Tests.FiberTests

open System
open Axial
open Axial.Fable.Tests.Harness

let tests : Test list =
    [ test "A forked fiber releases what it acquired when it settles" (flow {
          let released = ref false

          let! fiber =
              flow {
                  do! Flow.scopeAsyncFinalizer (fun _ -> async { released.Value <- true })
                  return 42
              }
              |> Flow.fork

          let! value = Fiber.join fiber
          return [ equal "value" 42 value; isTrue "released before the parent scope closed" released.Value ]
      })

      test "Flow.forkGraceful lets a consumer drain its queue when the scope closes" (flow {
          let written = ResizeArray<int>()

          do!
              flow {
                  let! (samples: Queue<int>) = Queue.bounded 10

                  let! _ =
                      samples
                      |> FlowStream.fromDequeue
                      |> FlowStream.tapFlow (fun _ -> Flow.sleep (TimeSpan.FromMilliseconds 2.0))
                      |> FlowStream.runForEach written.Add
                      |> Flow.forkGraceful (Dequeue.shutdown samples) (TimeSpan.FromSeconds 5.0)

                  do! samples |> Queue.offerAll [ 1..8 ] |> Flow.ignore
              }
              |> Flow.scoped

          return [ equal "written" [ 1..8 ] (List.ofSeq written) ]
      })

      test "Flow.forkGraceful interrupts a fiber that ignores its stop once the grace period ends" (flow {
          let interrupted = ref false
          let started = nowMilliseconds ()

          do!
              flow {
                  let! _ =
                      Flow.sleep (TimeSpan.FromMinutes 5.0)
                      |> Flow.onInterrupt (Flow.delay (fun () -> interrupted.Value <- true; Flow.ok ()))
                      |> Flow.forkGraceful (Flow.ok ()) (TimeSpan.FromMilliseconds 50.0)

                  ()
              }
              |> Flow.scoped

          return
              [ isTrue "interrupted" interrupted.Value
                isTrue "after the grace period, not before" (nowMilliseconds () - started >= 45.0) ]
      })

      test "Flow.ensuring and Flow.onExit run after every kind of outcome" (flow {
          let seen = ResizeArray<string>()

          let record (exit: Exit<int, string>) : Flow<unit, Never, unit> =
              Flow.delay (fun () ->
                  seen.Add(
                      match exit with
                      | Exit.Success _ -> "success"
                      | Exit.Failure cause when Cause.isInterrupted cause -> "interrupted"
                      | Exit.Failure _ -> "failure"
                  )

                  Flow.ok ())

          let! succeeded = Flow.ok 1 |> Flow.onExit record |> exitOf
          let! failed = Flow.fail "expected" |> Flow.onExit record |> exitOf
          let! fiber = Flow.never<unit, string, int> |> Flow.onExit record |> Flow.fork
          let! interrupted = Fiber.interrupt fiber

          return
              [ equal "success kept" (Exit.Success 1) succeeded
                equal "failure kept" (Exit.Failure(Cause.Fail "expected")) failed
                isTrue "interruption kept" (match interrupted with Exit.Failure cause -> Cause.isInterrupted cause | _ -> false)
                equal "outcomes seen" [ "success"; "failure"; "interrupted" ] (List.ofSeq seen) ]
      })

      test "Flow.onInterrupt runs only on interruption and finishes despite it" (flow {
          let cleaned = ref 0

          let cleanup : Flow<unit, Never, unit> =
              flow {
                  do! Flow.sleep (TimeSpan.FromMilliseconds 10.0)
                  cleaned.Value <- cleaned.Value + 1
              }

          let! _ = Flow.ok () |> Flow.onInterrupt cleanup |> exitOf
          let! fiber = Flow.never<unit, Never, unit> |> Flow.onInterrupt cleanup |> Flow.fork
          let! _ = Fiber.interrupt fiber
          return [ equal "cleanups" 1 cleaned.Value ]
      }) ]
