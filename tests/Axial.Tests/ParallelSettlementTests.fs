namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

/// How `zipPar` and `race` settle for every mix of branches that finish synchronously and branches that suspend.
/// On .NET, branches that have already settled are combined without a task, so these pin that path to the same
/// outcomes as the asynchronous one.
module ParallelSettlementTests =
    let private later (value: Flow<ClockEnvironment, string, 'value>) : Flow<ClockEnvironment, string, 'value> =
        Flow.sleep (TimeSpan.FromMilliseconds 20.0) |> Flow.bind (fun () -> value)

    let private run (workflow: Flow<ClockEnvironment, string, 'value>) =
        Flow.runSync (TestSupport.clockEnv ()) workflow

    let private throwing () : Flow<ClockEnvironment, string, int> =
        Flow(fun _ _ -> raise (InvalidOperationException "thrown while starting"))

    let private isDefect (exit: Exit<'value, string>) =
        match exit with
        | Exit.Failure(Cause.Die _) -> true
        | _ -> false

    [<Fact>]
    let ``zipPar of two synchronous successes pairs their values`` () =
        test <@ run (Flow.zipPar (Flow.ok 1) (Flow.ok 2)) = Exit.Success(1, 2) @>

    [<Fact>]
    let ``zipPar reports a synchronous failure on either side`` () =
        test <@ run (Flow.zipPar (Flow.fail "left") (Flow.ok 2)) = Exit.Failure(Cause.Fail "left") @>
        test <@ run (Flow.zipPar (Flow.ok 1) (Flow.fail "right")) = Exit.Failure(Cause.Fail "right") @>

    [<Fact>]
    let ``zipPar combines two synchronous failures`` () =
        test
            <@
                run (Flow.zipPar (Flow.fail "left") (Flow.fail "right")) = Exit.Failure(
                    Cause.Both(Cause.Fail "left", Cause.Fail "right")
                )
            @>

    [<Fact>]
    let ``zipPar mixes synchronous and suspended branches`` () =
        test <@ run (Flow.zipPar (Flow.ok 1) (later (Flow.ok 2))) = Exit.Success(1, 2) @>
        test <@ run (Flow.zipPar (later (Flow.ok 1)) (Flow.ok 2)) = Exit.Success(1, 2) @>
        test <@ run (Flow.zipPar (later (Flow.ok 1)) (later (Flow.ok 2))) = Exit.Success(1, 2) @>
        test <@ run (Flow.zipPar (Flow.ok 1) (later (Flow.fail "right"))) = Exit.Failure(Cause.Fail "right") @>

    [<Fact>]
    let ``zipPar interrupts a suspended sibling of a synchronous failure`` () =
        let siblingFinished = ref false
        // Long enough that it can only finish if the failure does not interrupt it; 20 ms raced the interrupt under
        // a loaded full-suite run.
        let sibling =
            Flow.sleep (TimeSpan.FromSeconds 10.0)
            |> Flow.bind (fun () -> Flow.delay (fun () -> siblingFinished.Value <- true; Flow.ok 2))

        test <@ run (Flow.zipPar (Flow.fail "left") sibling) = Exit.Failure(Cause.Fail "left") @>
        test <@ not siblingFinished.Value @>
        test <@ run (Flow.zipPar sibling (Flow.fail "right")) = Exit.Failure(Cause.Fail "right") @>
        test <@ not siblingFinished.Value @>

    [<Fact>]
    let ``zipPar fails with a defect when a branch throws while starting`` () =
        let rightStarted = ref false
        let right = Flow.delay (fun () -> rightStarted.Value <- true; Flow.ok 2)

        test <@ isDefect (run (Flow.zipPar (throwing ()) right)) @>
        test <@ not rightStarted.Value @>
        test <@ isDefect (run (Flow.zipPar (Flow.ok 1) (throwing ()))) @>

    [<Fact>]
    let ``race returns the left exit when both settle synchronously`` () =
        test <@ run (Flow.race (Flow.ok 1) (Flow.ok 2)) = Exit.Success 1 @>
        test <@ run (Flow.race (Flow.fail "left") (Flow.ok 2)) = Exit.Failure(Cause.Fail "left") @>

    [<Fact>]
    let ``race returns a synchronous right exit while the left one is suspended`` () =
        test <@ run (Flow.race (later (Flow.ok 1)) (Flow.ok 2)) = Exit.Success 2 @>
        test <@ run (Flow.race (later (Flow.ok 1)) (Flow.fail "right")) = Exit.Failure(Cause.Fail "right") @>
        test <@ run (Flow.race Flow.never (Flow.ok 2)) = Exit.Success 2 @>

    [<Fact>]
    let ``race returns the first suspended branch to settle`` () =
        let slower = Flow.sleep (TimeSpan.FromSeconds 5.0) |> Flow.map (fun () -> 2)

        test <@ run (Flow.race (Flow.ok 1) Flow.never) = Exit.Success 1 @>
        test <@ run (Flow.race (later (Flow.ok 1)) slower) = Exit.Success 1 @>
        test <@ run (Flow.race slower (later (Flow.ok 2))) = Exit.Success 2 @>

    [<Fact>]
    let ``race fails with a defect when a branch throws while starting`` () =
        test <@ isDefect (run (Flow.race (throwing ()) (Flow.ok 2))) @>
        test <@ isDefect (run (Flow.race (Flow.ok 1) (throwing ()))) @>
