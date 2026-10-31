module Axial.Fable.Tests.CancellationClassificationTests

open System
open System.Threading
open Axial
open Axial.Fable.Tests.Harness

let tests =
    [ test "timeout treats a canceled branch's thrown cancellation as interruption" (flow {
          let branch : Flow<unit, string, int> =
              Flow(fun (_: unit) token ->
                  async {
                      let! _ =
                          Async.FromContinuations(fun (resolve, _, _) ->
                              if token.IsCancellationRequested then resolve ()
                              else token.Register(fun () -> resolve ()) |> ignore)

                      return raise (Axial.Platform.runtimeCancellationException ())
                  })

          let! result = Flow.timeout (TimeSpan.FromMilliseconds 1.0) "timeout" branch |> exitOf
          return [ equal "timeout wins without a cancellation defect" (Exit.Failure(Cause.Fail "timeout")) result ]
      }) ]
