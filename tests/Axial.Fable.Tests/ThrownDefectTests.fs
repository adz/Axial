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

// Throws outside the flow builder's syntax, so the guardrail against raising inside `flow { }` stays quiet: these
// tests exist to throw on purpose.
let private throwNow (message: string) : 'value = raise (Exception message)

let private messageOf (source: Flow<unit, string, int>) : Flow<unit, Never, string> =
    source |> Flow.foldCause (fun _ -> Flow.ok "success") (dieMessage >> Flow.ok)

let private messageOf' (source: Flow<unit, string, string>) : Flow<unit, Never, string> =
    source |> Flow.foldCause Flow.ok (dieMessage >> Flow.ok)

let tests =
    [ test "foldCause sees an exception thrown synchronously and after an await" (
          flow {
              let! synchronous = messageOf (flow { return throwNow "sync boom" })

              let! afterAwait =
                  messageOf (
                      Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                      |> Flow.bind (fun () -> throwNow "async boom")
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
                              return throwNow "finally boom"
                          finally
                              compensations.Value <- compensations.Value + 1
                      }
                  )

              let! caught =
                  flow {
                      try
                          do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                          return throwNow "with boom"
                      with error ->
                          return $"caught {error.Message}"
                  }

              return
                  [ equal "the defect is kept" "finally boom" finallyMessage
                    equal "the compensation ran once" 1 compensations.Value
                    equal "try/with caught the throw" "caught with boom" caught ]
          })
      // T28b: parity with .NET for `use`, mapBoth/tracedError, handler throws and interruption.
      test "flow use disposes the resource when the body throws synchronously or after an await" (
          flow {
              let disposed = ref 0
              let resource () = { new IDisposable with member _.Dispose() = disposed.Value <- disposed.Value + 1 }

              let! synchronous =
                  messageOf (
                      flow {
                          use _ = resource ()
                          return throwNow "use sync boom"
                      }
                  )

              let! afterAwait =
                  messageOf (
                      flow {
                          use _ = resource ()
                          do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                          return throwNow "use async boom"
                      }
                  )

              return
                  [ equal "the synchronous defect is kept" "use sync boom" synchronous
                    equal "the defect after an await is kept" "use async boom" afterAwait
                    equal "each resource was disposed once" 2 disposed.Value ]
          })
      test "mapBoth and tracedError see an exception thrown after an await" (
          flow {
              let throwsAfterAwait message : Flow<unit, string, int> =
                  Flow.sleep (TimeSpan.FromMilliseconds 1.0) |> Flow.bind (fun () -> throwNow message)

              let! mapped =
                  messageOf (
                      throwsAfterAwait "mapBoth boom"
                      |> Flow.mapBoth id (fun cause -> Cause.Die(Exception $"mapped {dieMessage cause}")))

              let! traced =
                  throwsAfterAwait "traced boom"
                  |> Flow.tracedError "fable.trace"
                  |> Flow.foldCause
                      (fun _ -> Flow.ok "success")
                      (fun cause ->
                          match cause with
                          | Cause.Traced(_, trace) -> Flow.ok $"{trace}: {dieMessage cause}"
                          | _ -> Flow.ok "not traced")

              return
                  [ equal "mapBoth mapped the thrown defect" "mapped mapBoth boom" mapped
                    equal "tracedError traced the thrown defect" "fable.trace: traced boom" traced ]
          })
      test "a throwing handler is neither swallowed nor called again" (
          flow {
              let failureCalls = ref 0
              let catchCalls = ref 0

              let! fromSuccessHandler =
                  flow {
                      do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                      return 1
                  }
                  |> Flow.foldCause
                      (fun (_: int) -> throwNow "success handler boom")
                      (fun (_: Cause<string>) ->
                          failureCalls.Value <- failureCalls.Value + 1
                          Flow.ok "failure handler")
                  |> messageOf'

              let! fromCatch =
                  (Flow.die (Exception "defect") : Flow<unit, string, int>)
                  |> Flow.catch (fun _ ->
                      catchCalls.Value <- catchCalls.Value + 1
                      throwNow "catch handler boom")
                  |> messageOf

              return
                  [ equal "the success handler's throw is a defect" "success handler boom" fromSuccessHandler
                    equal "the failure handler did not run" 0 failureCalls.Value
                    equal "the catch handler's throw is a defect" "catch handler boom" fromCatch
                    equal "the catch handler ran once" 1 catchCalls.Value ]
          })
      test "an interrupted body stays interrupted through try/with and foldCause" (
          flow {
              let! started = Deferred.make<unit, Never, unit> ()

              let guarded : Flow<unit, string, string> =
                  flow {
                      try
                          Deferred.succeedNow () started |> ignore
                          do! Flow.sleep (TimeSpan.FromHours 1.0)
                          return "body"
                      with _ ->
                          return "fallback"
                  }
                  |> Flow.foldCause Flow.ok (fun cause ->
                      if Cause.isInterrupted cause then Flow.ofExit (Exit.Failure cause) else Flow.ok "handled")

              let! fiber = Flow.fork guarded
              do! Deferred.await started
              let! exit = Fiber.interrupt fiber

              return
                  [ isTrue
                        "the fiber ended interrupted"
                        (match exit with
                         | Exit.Failure cause -> Cause.isInterrupted cause
                         | Exit.Success _ -> false) ]
          }) ]
