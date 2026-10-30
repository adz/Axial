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
          let! actual = Flow.race read Flow.never
                        |> Flow.withClock { new IClock with
                            member _.UtcNow() = instant
                            member _.Elapsed() = TimeSpan.Zero
                            member _.Sleep(delay, token) = Clock.live.Sleep(delay, token) }
                        |> Flow.withRandom (Random.fromValue 42)
                        |> Flow.withGuid (Guid.fromValue id)
          return [ equal "overrides" (instant, 42, id) actual ]
      }) ]
