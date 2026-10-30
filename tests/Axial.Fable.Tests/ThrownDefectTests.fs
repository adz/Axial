module Axial.Fable.Tests.ThrownDefectTests

open System
open Axial
open Axial.Fable.Tests.Harness

// T28: combinators that see the whole cause, and the builder's try/with and try/finally, observe an exception a
// flow throws, synchronously or after an await, on JavaScript as on .NET.
let private dieMessage (cause: Cause<string>) : string =
    match Cause.untraced cause with
    | Cause.Die error -> error.Message
    | _ -> "not a defect"

let private messageOf (source: Flow<unit, string, int>) : Flow<unit, Never, string> =
    source |> Flow.foldCause (fun _ -> Flow.ok "success") (dieMessage >> Flow.ok)

let tests =
    [ test "foldCause sees an exception thrown synchronously and after an await" (
          flow {
              let! synchronous = messageOf (flow { return raise (Exception "sync boom") })

              let! afterAwait =
                  messageOf (
                      Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                      |> Flow.bind (fun () -> raise (Exception "async boom"))
                  )

              return
                  [ equal "synchronous throw reaches the failure handler" "sync boom" synchronous
                    equal "throw after an await reaches the failure handler" "async boom" afterAwait ]
          })
      test "flow try finally and try with observe an exception thrown after an await" (
          flow {
              let compensations = ref 0

              let! finallyMessage =
                  messageOf (
                      flow {
                          try
                              do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                              return raise (Exception "finally boom")
                          finally
                              compensations.Value <- compensations.Value + 1
                      }
                  )

              let! caught =
                  flow {
                      try
                          do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                          return raise (Exception "with boom")
                      with error ->
                          return $"caught {error.Message}"
                  }

              return
                  [ equal "the defect is kept" "finally boom" finallyMessage
                    equal "the compensation ran once" 1 compensations.Value
                    equal "try/with caught the throw" "caught with boom" caught ]
          }) ]
