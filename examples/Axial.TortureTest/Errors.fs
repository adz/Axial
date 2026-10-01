/// Typed failures, defects, and interruptions from concurrent branches, recovered and combined.
module Axial.TortureTest.Errors

open System
open Axial
open Axial.TortureTest.Scenario

// <snippet:torture-errors>
type private Outcome =
    | Value of int
    | Failed of string
    | Died
    | Interrupted

let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let branches = round.Size 120

    // Every way a flow can produce each outcome.
    let produce kind : Flow<Axial.ClockEnvironment, string, int> =
        match kind with
        | 0 -> Flow.ok 1
        | 1 -> Flow.succeed 2 |> Flow.map ((+) 1)
        | 2 -> Flow.fail "fail"
        | 3 -> Flow.error "error"
        | 4 -> Flow.fromResult (Error "result")
        | 5 -> Flow.fromOption "option" None
        | 6 -> Flow.fromValueOption "voption" ValueNone
        | 7 -> Flow.ofExit (Exit.Failure(Cause.Fail "exit"))
        | 8 -> Flow.die (InvalidOperationException "defect")
        | 9 -> Flow.delay (fun () -> raise (InvalidOperationException "thrown"))
        | 10 -> Flow.never |> Flow.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> Flow.ok 4)
        | 11 -> Flow.verify (Policy.compose Policy.pass (Policy.withError (fun input -> if input > 0 then Ok input else Error()) "policy")) 5
        | 12 -> Flow.verify (Policy.lift (fun (input: int) -> Error input) string) 6
        | 13 -> Flow.verify (Policy.optional (fun (_: ClockEnvironment) -> true) (Policy.context (fun (_: ClockEnvironment) input -> if input > 0 then Ok input else Error input) string)) 7
        | _ -> Flow.fromResult (Ok 8)

    let expected kind =
        match kind with
        | 0 -> Value 1
        | 1 -> Value 3
        | 2 -> Failed "fail"
        | 3 -> Failed "error"
        | 4 -> Failed "result"
        | 5 -> Failed "option"
        | 6 -> Failed "voption"
        | 7 -> Failed "exit"
        | 8 | 9 -> Died
        | 10 -> Value 4
        | 11 -> Value 5
        | 12 -> Failed "6"
        | 13 -> Value 7
        | _ -> Value 8

    let classify (exit: Exit<int, string>) =
        match exit with
        | Exit.Success value -> Value value
        | Exit.Failure cause when Cause.isInterrupted cause -> Interrupted
        | Exit.Failure cause ->
            match Cause.failures cause, Cause.defects cause with
            | failure :: _, _ -> Failed failure
            | [], _ :: _ -> Died
            | [], [] -> Interrupted

    flow {
        // Branches run concurrently; each one's outcome must survive being forked, joined, and classified.
        let kinds = [ for _ in 1..branches -> round.Next 15 ]
        let! fibers = kinds |> Flow.traverse (produce >> Flow.fork)
        let! exits = fibers |> Flow.traverse Fiber.await
        let classified = exits |> List.map classify

        // Recovery combinators turn every outcome into a value without losing which one it was.
        let recover kind =
            produce kind
            |> Flow.catch (fun error -> "caught " + error.Message)
            |> Flow.tapError (fun _ -> Flow.ok ())
            |> Flow.mapError (fun error -> error.ToUpperInvariant())
            |> Flow.tracedError "branch"
            |> Flow.orElseWith (fun error -> Flow.ok -error.Length)
            |> Flow.mapBoth id (Cause.map id)
            |> Flow.orElse (Flow.ok 0)
            |> exitOf
            |> Flow.map (function Exit.Success value -> value | Exit.Failure _ -> 0)

        let! recovered = kinds |> List.map recover |> Flow.sequencePar

        let recoveredAsExpected =
            List.zip kinds recovered
            |> List.forall (fun (kind, value) ->
                match expected kind with
                | Value expectedValue -> value = expectedValue
                | Failed failure -> value = -failure.Length
                | Died -> value < 0
                | Interrupted -> value = 0)

        // Causes combine without losing any failure, and Exit converts faithfully.
        let causes = exits |> List.choose (function Exit.Failure cause -> Some cause | Exit.Success _ -> None)
        let combined = causes |> List.fold (fun total cause -> Cause.both total cause) (Cause.Fail "seed")
        let sequential = causes |> List.fold Cause.thenCause (Cause.traced "start" (Cause.Fail "seed"))
        let countFailures cause = Cause.failures cause |> List.length
        let expectedFailures = 1 + (causes |> List.sumBy countFailures)
        let rendered = Cause.prettyPrint id combined

        // Exit.toResult keeps values and typed failures, and re-throws what a Result cannot hold.
        let exitRoundTrips =
            exits
            |> List.forall (fun exit ->
                let mapped = exit |> Exit.map ((+) 1) |> Exit.bind (fun value -> Exit.Success(value - 1)) |> Exit.mapError id |> Exit.mapBoth id id

                match exit with
                | Exit.Success original -> Exit.toResult mapped = Ok original && Exit.fromResult (Ok original) = exit
                | Exit.Failure(Cause.Fail failure) -> Exit.toResult mapped = Error failure && Exit.fromResult (Error failure) = exit
                | Exit.Failure _ ->
                    try
                        Exit.toResult mapped |> ignore
                        false
                    with _ ->
                        true)

        // Applicative and monadic combinators over concurrent forks agree with plain arithmetic.
        let! a = Flow.fork (Flow.ok 2)
        let! b = Flow.fork (Flow.ok 3)
        let! c = Flow.fork (Flow.ok 4)
        let! sum3 = Flow.map3 (fun x y z -> x + y + z) (Fiber.join a) (Fiber.join b) (Fiber.join c)
        let! sum2 = Flow.map2 (+) (Fiber.join a) (Fiber.join b)
        let! pair = Flow.zip (Fiber.join a) (Fiber.join c)
        let! applied = Flow.apply (Flow.ok ((*) 10)) (Fiber.join b)
        let! chained = Fiber.join a |> Flow.bind (fun x -> Flow.ok (x + 1)) |> Flow.tap (fun _ -> Flow.ok ())
        let! listed = [ Fiber.join a; Fiber.join b ] |> Flow.sequence
        let! traversed = [ 1; 2; 3 ] |> Flow.traverse (fun x -> Flow.ok (x * x))
        let! ignored = Fiber.join c |> Flow.ignore
        let! operators = Flow.(>>=) (Flow.(<!>) (fun x -> x + 1) (Fiber.join a)) (fun x -> Flow.(<!>) (fun y -> x + y) (Fiber.join b))
        let! appliedOperator = Flow.(<*>) (Flow.ok (fun x -> x - 1)) (Fiber.join c)

        // Bind adapts a Result's error at the bind site; the first Error stops the flow with the adapted error.
        let bindStep kind : Flow<Axial.ClockEnvironment, string, int> =
            flow {
                let! even = (if kind % 2 = 0 then Ok kind else Error()) |> Bind.error "odd"
                let! small = (if even < 8 then Ok even else Error even) |> Bind.mapError (fun large -> $"large {large}")
                return small
            }

        let! bound = kinds |> Flow.traverse (bindStep >> exitOf)

        let boundAsExpected =
            List.zip kinds bound
            |> List.forall (fun (kind, exit) ->
                match exit with
                | Exit.Success value -> kind % 2 = 0 && kind < 8 && value = kind
                | Exit.Failure(Cause.Fail "odd") -> kind % 2 = 1
                | Exit.Failure(Cause.Fail large) -> kind % 2 = 0 && kind >= 8 && large = $"large {kind}"
                | Exit.Failure _ -> false)

        // A flow that cannot fail stands where a typed error is expected.
        let! widened = (Flow.ok 5 : Flow<Axial.ClockEnvironment, Never, int>) |> Flow.widenError |> Flow.orElse (Flow.ok 0) |> exitOf

        let! foundResult =
            Flow.orElseFlow (Flow.ok "fallback") (Error())
            |> exitOf

        return
            [ check "every forked outcome was reported as itself" (List.zip kinds classified |> List.forall (fun (kind, outcome) -> outcome = expected kind))
              check "recovery turned every outcome into the value it should" recoveredAsExpected
              check "combined causes kept every failure" (countFailures combined = expectedFailures && countFailures sequential = expectedFailures && rendered.Length > 0)
              check "a traced cause untraces to the cause it wraps" (causes |> List.forall (fun cause -> Cause.untraced (Cause.traced "branch" cause) = Cause.untraced cause))
              check "Exit conversions kept values and failures" exitRoundTrips
              check "combinators over forks agree with arithmetic" (sum3 = 9 && sum2 = 5 && pair = (2, 4) && applied = 30 && chained = 3 && listed = [ 2; 3 ] && traversed = [ 1; 4; 9 ] && ignored = () && operators = 6 && appliedOperator = 3)
              check "Bind adapted each Result error at its bind site" boundAsExpected
              check "a flow that cannot fail widened to a typed error unchanged" (widened = Exit.Success 5)
              check "orElseFlow fails with the error its flow produced" (foundResult = Exit.Failure(Cause.Fail "fallback")) ]
    }
// </snippet:torture-errors>

let scenario : Scenario =
    { Name = "errors"
      Title = "Errors: every outcome from concurrent branches, recovered, combined, and converted"
      Run = run }
