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
              let! second = step "pool" |> Flow.runOn RunOn.ThreadPool
              let! third = step "dedicated" |> Flow.runOn RunOn.DedicatedThread
              let! fourth = step "adaptive" |> Flow.runOn (RunOn.adaptive ())
              let! batch = [ step "batch-a"; step "batch-b" ] |> Flow.sequenceParWith (Parallelism.adaptive 2)

              return
                  [ equal "values are preserved" [ "inline"; "pool"; "dedicated"; "adaptive"; "batch-a"; "batch-b" ] [ first; second; third; fourth; yield! batch ]
                    equal "every location ran in call order" [ "inline"; "pool"; "dedicated"; "adaptive"; "batch-a"; "batch-b" ] (List.ofSeq events) ]
          }) ]
