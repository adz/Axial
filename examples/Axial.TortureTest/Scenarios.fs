/// Every torture scenario, in the order the runner reports them.
module Axial.TortureTest.Scenarios

open Axial.TortureTest.Scenario

let all : Scenario list =
    [ Pipeline.scenario
      Queues.scenario
      Hubs.scenario
      Coordination.semaphoreScenario
      Coordination.deferredScenario
      Coordination.refScenario
      Stm.scenario
      Fibers.scenario
      Caches.scenario
      Schedules.scenario
      Parallel.scenario
      Errors.scenario
      Scopes.scenario
      Layers.scenario
      Streams.scenario
      Interop.scenario ]

let tryFind (name: string) : Scenario option = all |> List.tryFind (fun scenario -> scenario.Name = name)
