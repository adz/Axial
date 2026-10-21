module Axial.Fable.Tests.RunOnTests

open Axial
open Axial.Fable.Tests.Harness

let tests =
    [ test "runOn locations execute inline on JavaScript" (
          flow {
              let events = ResizeArray<string>()

              let step name =
                  Flow.delay (fun () ->
                      events.Add name
                      Flow.ok name)

              let! first = step "inline" |> Flow.runOn RunOn.Inline
              let! second = step "pool" |> Flow.onThreadPool
              let! third = step "dedicated" |> Flow.runOn RunOn.DedicatedThread

              return
                  [ equal "values are preserved" [ "inline"; "pool"; "dedicated" ] [ first; second; third ]
                    equal "every location ran in call order" [ "inline"; "pool"; "dedicated" ] (List.ofSeq events) ]
          }) ]
