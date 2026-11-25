namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

/// The examples in the API docs of Flow's collection functions, run as written. Each test shadows `printfn` to record
/// the lines an example prints, so the order and stopping point stated in each doc comment are checked here.
module CollectionExamplesTests =
    let private record (lines: ResizeArray<string>) (line: string) = lock lines (fun () -> lines.Add line)

    /// Runs an example's sleeps on a test clock, so the order they finish in follows from their durations and not from
    /// the machine's load. Before each step it waits until that many sleeps are registered, then advances the clock to
    /// the next deadline.
    let private onTestClock (steps: int list) (example: Flow<unit, 'error, 'value>) : Exit<'value, 'error> =
        let platform = TestPlatform.create ()
        flow {
            let! running = Flow.fork example
            for sleeping in steps do
                do! TestClock.awaitSleepers sleeping
                do! Flow.sync (fun () -> platform.Clock.AdvanceToNextDeadline() |> ignore)
            return! Fiber.join running
        }
        |> Flow.runTestWith platform ()

    [<Fact>]
    let ``traverse runs one at a time in order, returns a list and stops at the first failure`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let double n = Flow.sync (fun () -> printfn "double %d" n; n * 2)
        let all : Exit<int list, string> = [1; 2; 3] |> Flow.traverse double |> Flow.run ()
        test <@ List.ofSeq lines = [ "double 1"; "double 2"; "double 3" ] @>
        test <@ all = Exit.Success [2; 4; 6] @>

        lines.Clear()
        let positive n = if n > 0 then double n else Flow.fail $"{n} is not positive"
        let stopped = [1; -2; 3] |> Flow.traverse positive |> Flow.run ()
        test <@ List.ofSeq lines = [ "double 1" ] @>
        test <@ stopped = Exit.Failure(Cause.Fail "-2 is not positive") @>

    [<Fact>]
    let ``forEach runs one at a time in order, keeps no results and stops at the first failure`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let save n = Flow.sync (fun () -> printfn "saved %d" n)
        let all : Exit<unit, string> = [1; 2; 3] |> Flow.forEach save |> Flow.run ()
        test <@ List.ofSeq lines = [ "saved 1"; "saved 2"; "saved 3" ] @>
        test <@ all = Exit.Success () @>

        lines.Clear()
        let saveOdd n = if n % 2 = 0 then Flow.fail $"{n} is even" else save n
        let stopped = [1; 2; 3] |> Flow.forEach saveOdd |> Flow.run ()
        test <@ List.ofSeq lines = [ "saved 1" ] @>
        test <@ stopped = Exit.Failure(Cause.Fail "2 is even") @>

    [<Fact>]
    let ``sequence runs built flows one at a time in order and stops at the first failure`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let step n = Flow.sync (fun () -> printfn "step %d" n; n)
        let steps = [ step 1; step 2; step 3 ]
        test <@ lines.Count = 0 @>
        let all : Exit<int list, string> = Flow.sequence steps |> Flow.run ()
        test <@ List.ofSeq lines = [ "step 1"; "step 2"; "step 3" ] @>
        test <@ all = Exit.Success [1; 2; 3] @>

        lines.Clear()
        let stopped = Flow.sequence [ step 1; Flow.fail "stop"; step 3 ] |> Flow.run ()
        test <@ List.ofSeq lines = [ "step 1" ] @>
        test <@ stopped = Exit.Failure(Cause.Fail "stop") @>

    [<Fact>]
    let ``traversePar runs at most N at once, returns input order and interrupts the rest on failure`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let fetch (name: string, ms: float) =
            flow {
                printfn "start %s" name
                do! Flow.sleep (TimeSpan.FromMilliseconds ms)
                printfn "done %s" name
                return name.ToUpper()
            }
        let all : Exit<string list, string> =
            [ "a", 400.0; "b", 100.0; "c", 100.0 ] |> Flow.traversePar (Parallelism.bounded 2) fetch |> onTestClock [ 2; 2; 1 ]
        test <@ List.ofSeq lines = [ "start a"; "start b"; "done b"; "start c"; "done c"; "done a" ] @>
        test <@ all = Exit.Success [ "A"; "B"; "C" ] @>

        lines.Clear()
        let fetchUnlessB (name, ms) = if name = "b" then Flow.fail "b failed" else fetch (name, ms)
        let stopped = [ "a", 400.0; "b", 100.0; "c", 100.0 ] |> Flow.traversePar (Parallelism.bounded 2) fetchUnlessB |> Flow.run ()
        test <@ List.ofSeq lines = [ "start a" ] @>
        test <@ stopped = Exit.Failure(Cause.Fail "b failed") @>

    [<Fact>]
    let ``forEachPar runs at most N at once, keeps no results and interrupts the rest on failure`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let index (name: string, ms: float) =
            flow {
                printfn "start %s" name
                do! Flow.sleep (TimeSpan.FromMilliseconds ms)
                printfn "done %s" name
            }
        let all : Exit<unit, string> =
            [ "a", 400.0; "b", 100.0; "c", 100.0 ] |> Flow.forEachPar (Parallelism.bounded 2) index |> onTestClock [ 2; 2; 1 ]
        test <@ List.ofSeq lines = [ "start a"; "start b"; "done b"; "start c"; "done c"; "done a" ] @>
        test <@ all = Exit.Success () @>

        lines.Clear()
        let indexUnlessB (name, ms) = if name = "b" then Flow.fail "b failed" else index (name, ms)
        let stopped = [ "a", 400.0; "b", 100.0; "c", 100.0 ] |> Flow.forEachPar (Parallelism.bounded 2) indexUnlessB |> Flow.run ()
        test <@ List.ofSeq lines = [ "start a" ] @>
        test <@ stopped = Exit.Failure(Cause.Fail "b failed") @>

    [<Fact>]
    let ``sequencePar runs every flow at once, returns input order and interrupts the rest on failure`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let after (ms: float) (name: string) =
            flow {
                printfn "start %s" name
                do! Flow.sleep (TimeSpan.FromMilliseconds ms)
                printfn "done %s" name
                return name
            }
        let all : Exit<string list, string> =
            Flow.sequencePar [ after 400.0 "a"; after 100.0 "b"; after 200.0 "c" ] |> onTestClock [ 3; 2; 1 ]
        test <@ List.ofSeq lines = [ "start a"; "start b"; "start c"; "done b"; "done c"; "done a" ] @>
        test <@ all = Exit.Success [ "a"; "b"; "c" ] @>

        lines.Clear()
        let stopped = Flow.sequencePar [ after 400.0 "a"; Flow.fail "b failed"; after 200.0 "c" ] |> Flow.run ()
        test <@ List.ofSeq lines = [ "start a"; "start c" ] @>
        test <@ stopped = Exit.Failure(Cause.Fail "b failed") @>

    [<Fact>]
    let ``sequenceParWith runs at most N of the flows at once and returns input order`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let after (ms: float) (name: string) =
            flow {
                printfn "start %s" name
                do! Flow.sleep (TimeSpan.FromMilliseconds ms)
                printfn "done %s" name
                return name
            }
        let all : Exit<string list, string> =
            Flow.sequenceParWith (Parallelism.bounded 2) [ after 400.0 "a"; after 100.0 "b"; after 100.0 "c" ] |> onTestClock [ 2; 2; 1 ]
        test <@ List.ofSeq lines = [ "start a"; "start b"; "done b"; "start c"; "done c"; "done a" ] @>
        test <@ all = Exit.Success [ "a"; "b"; "c" ] @>

    [<Fact>]
    let ``traverseParUsing opens one resource per worker and returns input order`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let connection =
            Resource.create
                (Flow.sync (fun () -> printfn "open"; "connection"))
                (fun _ -> Flow.sync (fun () -> printfn "close"))
        let load (_: string) n = Flow.sleep (TimeSpan.FromMilliseconds 10.0) |> Flow.map (fun () -> n * 10)
        let all : Exit<int list, string> =
            [1..6] |> Flow.traverseParUsing (Parallelism.bounded 2) connection load |> Flow.run ()
        test <@ lines |> Seq.sort |> List.ofSeq = [ "close"; "close"; "open"; "open" ] @>
        test <@ all = Exit.Success [10; 20; 30; 40; 50; 60] @>

    [<Fact>]
    let ``forEachParUsing opens one resource per worker and keeps no results`` () =
        let lines = ResizeArray<string>()
        let printfn format = Printf.ksprintf (record lines) format
        let connection =
            Resource.create
                (Flow.sync (fun () -> printfn "open"; "connection"))
                (fun _ -> Flow.sync (fun () -> printfn "close"))
        let store (_: string) (_: int) = Flow.sleep (TimeSpan.FromMilliseconds 10.0)
        let all : Exit<unit, string> =
            [1..6] |> Flow.forEachParUsing (Parallelism.bounded 2) connection store |> Flow.run ()
        test <@ lines |> Seq.sort |> List.ofSeq = [ "close"; "close"; "open"; "open" ] @>
        test <@ all = Exit.Success () @>
