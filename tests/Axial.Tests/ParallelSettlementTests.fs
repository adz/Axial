namespace Axial.Tests

open System
open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

/// How `zipPar` and `race` settle for every mix of branches that finish synchronously and branches that suspend.
/// On .NET, branches that have already settled are combined without a task, so these pin that path to the same
/// outcomes as the asynchronous one.
module ParallelSettlementTests =
    let private later (value: Flow<unit, string, 'value>) : Flow<unit, string, 'value> =
        Flow.sleep (TimeSpan.FromMilliseconds 20.0) |> Flow.bind (fun () -> value)

    let private run (workflow: Flow<unit, string, 'value>) =
        Flow.runSync () workflow

    let private throwing () : Flow<unit, string, int> =
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

    // T30 review fix: the right branch throws while starting, after the left one has started and holds a resource.
    let private holdingLeft (released: bool ref) (defect: exn option) : Flow<unit, string, unit> =
        // Ignores cancellation for 100 ms, as a branch in the middle of a release would, then ends with its defect.
        Flow(fun _ _ ->
            ValueTask<Exit<unit, string>>(
                task {
                    do! Task.Delay 100
                    released.Value <- true

                    return
                        match defect with
                        | Some error -> Exit.Failure(Cause.Die error)
                        | None -> Exit.Success()
                }))

    let private throwingUnit () : Flow<unit, string, unit> =
        Flow(fun _ _ -> raise (InvalidOperationException "thrown while starting"))

    let private rec' (cause: Cause<string>) =
        let rec dies c =
            match c with
            | Cause.Die e -> [ e ]
            | Cause.Both(a, b) -> dies a @ dies b
            | _ -> []
        dies cause

    [<Fact>]
    let ``a synchronously throwing right branch settles the started left branch before the outcome`` () =
        for combinator in
            [ (fun l r -> Flow.zipPar l r |> Flow.map ignore)
              (fun l r -> Flow.sequencePar [ l; r ] |> Flow.map ignore) ] do
            let released = ref false
            let outcome = run (combinator (holdingLeft released None) (throwingUnit ()))
            test <@ released.Value @>
            test <@ (match outcome with Exit.Failure(Cause.Die _) -> true | _ -> false) @>

    [<Fact>]
    let ``a synchronously throwing right branch combines the started left branch defect`` () =
        for combinator in
            [ (fun l r -> Flow.zipPar l r |> Flow.map ignore)
              (fun l r -> Flow.sequencePar [ l; r ] |> Flow.map ignore) ] do
            let released = ref false
            let leftBoom = InvalidOperationException "left finalizer"
            let outcome = run (combinator (holdingLeft released (Some leftBoom)) (throwingUnit ()))
            test <@ released.Value @>
            match outcome with
            | Exit.Failure cause -> test <@ rec' cause |> List.exists (fun e -> obj.ReferenceEquals(e, leftBoom)) @>
            | _ -> failwith "expected failure"

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
