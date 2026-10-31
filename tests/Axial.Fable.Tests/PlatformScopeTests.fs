module Axial.Fable.Tests.PlatformScopeTests

open System
open Axial
open Axial.PlatformService
open Axial.Fable.Tests.Harness

let tests =
    [ test "platform overrides survive await and race branches" (flow {
          let instant = DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)
          let id = System.Guid.Parse "00000000-0000-0000-0000-000000000042"
          let read = flow {
              do! Flow.sleep (TimeSpan.FromMilliseconds 1.)
              let! now = Clock.now
              let! number = Random.next
              let! guid = Guid.newGuid
              return now, number, guid
          }
          let outerInstant = DateTimeOffset(1999, 1, 1, 0, 0, 0, TimeSpan.Zero)
          let outerId = System.Guid.Empty
          let inner = Flow.race read Flow.never
                        |> Flow.withClock { new IClock with
                            member _.UtcNow() = instant
                            member _.Elapsed() = TimeSpan.Zero
                            member _.Sleep(delay, token) = Clock.live.Sleep(delay, token) }
                        |> Flow.withRandom (Random.fromValue 42)
                        |> Flow.withGuid (Guid.fromValue id)
          let! actual, restored =
              (flow {
                  let! actual = inner
                  let! restored = read
                  return actual, restored
              })
              |> Flow.withClock { new IClock with
                  member _.UtcNow() = outerInstant
                  member _.Elapsed() = TimeSpan.Zero
                  member _.Sleep(delay, token) = Clock.live.Sleep(delay, token) }
              |> Flow.withRandom (Random.fromValue 7)
              |> Flow.withGuid (Guid.fromValue outerId)
          return [ equal "overrides" (instant, 42, id) actual
                   equal "outer restored after await" (outerInstant, 7, outerId) restored ]
      })
      test "fixed clock sleep is explicitly advanced" (flow {
          let instant = DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)
          let clock = Clock.fromValue instant
          let fixedClock = Clock.fromValue instant
          let! registered = Deferred.make ()
          let notifying =
              { new IClock with
                  member _.UtcNow() = (clock :> IClock).UtcNow()
                  member _.Elapsed() = (clock :> IClock).Elapsed()
                  member _.Sleep(delay, token) =
                      let pending = (clock :> IClock).Sleep(delay, token)
                      Deferred.succeedNow () registered |> ignore
                      pending }
          let! child = Flow.sleep (TimeSpan.FromSeconds 1.) |> Flow.fork |> Flow.withClock notifying
          do! Deferred.await registered
          clock.AdvanceBy(TimeSpan.FromSeconds 1.)
          do! Fiber.join child
          return [ equal "manual time" (TimeSpan.FromSeconds 1.) ((clock :> IClock).Elapsed())
                   equal "fixed starts at the supplied instant" instant ((fixedClock :> IClock).UtcNow()) ]
      }) ]
