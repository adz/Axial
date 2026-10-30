/// Forked fibers, their scopes, graceful stop, and lifecycle hooks on the JavaScript runtime.
module Axial.Fable.Tests.FiberTests

open System
open Axial
open Axial.Fable.Tests.Harness

let tests : Test list =
    [ test "FiberLocal overrides are inherited by forked fibers" (flow {
          let local = FiberLocal.make 0
          let! value =
              flow {
                  let! fiber = Flow.fork (FiberLocal.get local)
                  return! Fiber.join fiber
              }
              |> FiberLocal.locally 9 local
          let! restored = FiberLocal.get local
          return [ equal "forked" 9 value; equal "restored" 0 restored ]
      })

      test "FiberLocal survives suspension while another fiber reads its own value" (flow {
          let local = FiberLocal.named "request" 0
          let! other =
              (flow {
                  do! Flow.sleep (TimeSpan.FromMilliseconds 2.0)
                  return! FiberLocal.get local
              }
              |> FiberLocal.locally 2 local)
              |> Flow.fork
          let! inner =
              (flow {
                  do! Flow.sleep (TimeSpan.FromMilliseconds 10.0)
                  return! FiberLocal.get local
              }
              |> FiberLocal.locally 1 local)
          let! sibling = Fiber.join other
          let! outer = FiberLocal.get local
          return [ equal "inner" 1 inner; equal "sibling" 2 sibling; equal "restored" 0 outer ]
      })

      test "A forked fiber releases what it acquired when it settles" (flow {
          let released = ref false

          let! fiber =
              flow {
                  do! Flow.scopeFinalizer (Flow.delay (fun () -> released.Value <- true; Flow.ok ()))
                  return 42
              }
              |> Flow.fork

          let! value = Fiber.join fiber
          return [ equal "value" 42 value; isTrue "released before the parent scope closed" released.Value ]
      })

      test "Flow.forkResource keeps the resource in the caller's scope" (flow {
          let events = ResizeArray<string>()

          let connection =
              Resource.create
                  (Flow.delay (fun () -> events.Add "acquired"; Flow.ok "db"))
                  (fun _ -> Flow.delay (fun () -> events.Add "released"; Flow.ok ()))

          let! whileOpen =
              flow {
                  let! connecting = Flow.forkResource connection
                  let! name = Fiber.join connecting
                  return name, List.ofSeq events
              }
              |> Flow.scoped

          return
              [ equal "joined while open" ("db", [ "acquired" ]) whileOpen
                equal "released with the scope" [ "acquired"; "released" ] (List.ofSeq events) ]
      })

      test "Flow.zipPar and Flow.race hand over the scoped resources of values the caller receives" (flow {
          let events = ResizeArray<string>()

          let acquire (name: string) =
              Flow.scopeResource (
                  Resource.create
                      (Flow.delay (fun () -> events.Add $"{name}:acquired"; Flow.ok name))
                      (fun _ -> Flow.delay (fun () -> events.Add $"{name}:released"; Flow.ok ())))

          let releasedNow () = events |> Seq.filter (fun event -> event.EndsWith ":released") |> List.ofSeq |> List.sort

          let! insideScope =
              flow {
                  let! _ = Flow.zipPar (acquire "left") (acquire "right")
                  let afterZip = releasedNow ()

                  let loser =
                      flow {
                          let! _ = acquire "loser"
                          do! Flow.never
                          return "loser"
                      }

                  let winner = Flow.sleep (TimeSpan.FromMilliseconds 20.0) |> Flow.bind (fun () -> acquire "winner")
                  let! won = Flow.race loser winner
                  do! Flow.sleep (TimeSpan.FromMilliseconds 20.0)
                  return afterZip, won, releasedNow ()
              }
              |> Flow.scoped

          let afterZip, won, afterRace = insideScope

          return
              [ equal "nothing released after zipPar" [] afterZip
                equal "winner" "winner" won
                equal "only the loser released after race" [ "loser:released" ] afterRace
                equal
                    "the rest released with the scope"
                    [ "left:released"; "loser:released"; "right:released"; "winner:released" ]
                    (releasedNow ()) ]
      })

      test "Flow.disconnect reports a late defect exactly once" (flow {
          let! observed = Deferred.make<unit, Never, exn> ()
          let count = ref 0
          let started = ref false
          let mutable release = fun () -> ()
          let defect = InvalidOperationException "late disconnected Fable defect"

          let observer =
              { FiberObserver.none with
                  OnUnobservedDefect = fun _ error ->
                      count.Value <- count.Value + 1
                      Deferred.succeedNow error observed |> ignore }

          let stubborn : Flow<unit, Never, unit> =
              Async.FromContinuations(fun (_, error, _) ->
                  started.Value <- true
                  release <- fun () -> error defect)
              |> Flow.fromAsync

          let! fiber = stubborn |> Flow.disconnect |> Flow.withFiberObserver observer |> Flow.fork
          let! interrupted = Fiber.interrupt fiber
          release ()
          let! reported = Deferred.await observed

          return
              [ isTrue "the disconnected operation started" started.Value
                isTrue "its caller was interrupted" (match interrupted with Exit.Failure cause -> Cause.isInterrupted cause | _ -> false)
                isTrue "the late defect was preserved" (obj.ReferenceEquals(defect, reported))
                equal "the observer saw it once" 1 count.Value ]
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
      })

      // Fable's Async continues on a fresh stack through setTimeout after a few thousand synchronous steps; the fiber
      // must still see its own runtime (annotations, fiber id, scope) when it resumes there.
      test "A fiber keeps its own runtime across a long synchronous stretch" (flow {
          let worker (name: string) =
              flow {
                  let! before = Flow.fiberId
                  let! _ = [ 1..20000 ] |> Flow.traverse Flow.ok
                  let! annotations = Flow.annotations
                  let! after = Flow.fiberId
                  // Opening a scope registers it with the fiber's own scope, which must still be open.
                  do! Flow.scoped (Flow.ok ())
                  return annotations |> Map.tryFind "worker" = Some name && before = after
              }
              |> Flow.annotate "worker" name

          let! fibers = [ "a"; "b"; "c" ] |> Flow.traverse (worker >> Flow.fork)
          do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
          let! kept = fibers |> Flow.traverse Fiber.join
          let! rootAnnotations = Flow.annotations
          return [ equal "each fiber kept its runtime" [ true; true; true ] kept; isTrue "the root kept its runtime" (not (rootAnnotations.ContainsKey "worker")) ]
      }) ]
