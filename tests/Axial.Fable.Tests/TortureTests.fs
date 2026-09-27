/// Runs every torture scenario from examples/Axial.TortureTest on the JavaScript runtime, at a fifth of its .NET size.
module Axial.Fable.Tests.TortureTests

open Axial
open Axial.TortureTest
open Axial.TortureTest.Scenario
open Axial.Fable.Tests.Harness

let private rounds = 3

let tests : Test list =
    [ for scenario in Scenarios.all ->
          test $"Torture: {scenario.Title}" (flow {
              let! results = [ 1..rounds ] |> Flow.traverse (fun seed -> scenario.Run(Round(seed, 20)) |> Flow.map (fun checks -> seed, checks))

              return
                  [ for seed, checks in results do
                        for held in checks -> isTrue $"seed {seed}: {held.Invariant}" held.Held ]
          }) ]
