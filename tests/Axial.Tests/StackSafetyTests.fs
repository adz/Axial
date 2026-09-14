namespace Axial.Tests

open System.Threading
open Axial
open Swensen.Unquote
open Xunit

/// Loops whose steps complete synchronously must not grow the stack per iteration. These run on a 1MB-stack thread,
/// the default on Windows, where a stack-unsafe loop overflows within a few thousand iterations.
module StackSafetyTests =
    let private iterations = 200_000

    let private onSmallStack (action: unit -> 'value) : 'value =
        let mutable result = Unchecked.defaultof<'value>
        let mutable error = null
        let thread =
            Thread((fun () ->
                try result <- action ()
                with ex -> error <- ex), 1024 * 1024)
        thread.Start()
        thread.Join()
        if not (isNull error) then raise error
        result

    [<Fact>]
    let ``flow for loop with synchronous steps is stack safe`` () =
        let workflow : Flow<unit, string, int> =
            flow {
                let mutable total = 0
                for value in Seq.init iterations id do
                    do! Flow.succeed ()
                    total <- total + (value % 2)
                return total
            }

        test <@ onSmallStack (fun () -> Flow.runSync () workflow) = Exit.Success(iterations / 2) @>

    [<Fact>]
    let ``flow while loop with synchronous steps is stack safe`` () =
        let workflow : Flow<unit, string, int> =
            flow {
                let mutable count = 0
                while count < iterations do
                    do! Flow.succeed ()
                    count <- count + 1
                return count
            }

        test <@ onSmallStack (fun () -> Flow.runSync () workflow) = Exit.Success iterations @>

    [<Fact>]
    let ``flow for loop stops at the first failure`` () =
        let visited = ref 0
        let workflow : Flow<unit, string, unit> =
            flow {
                for value in Seq.init iterations id do
                    visited.Value <- visited.Value + 1
                    if value = 150_000 then return! Flow.fail "stop"
            }

        test <@ onSmallStack (fun () -> Flow.runSync () workflow) = Exit.Failure(Cause.Fail "stop") @>
        test <@ visited.Value = 150_001 @>

    [<Fact>]
    let ``Flow traverse with synchronous steps is stack safe`` () =
        let workflow : Flow<unit, string, int list> = Flow.traverse Flow.succeed (Seq.init iterations id)

        let exit = onSmallStack (fun () -> Flow.runSync () workflow)
        test <@ (match exit with Exit.Success values -> values.Length = iterations && List.last values = iterations - 1 | _ -> false) @>

    [<Fact>]
    let ``left-nested bind chains are stack safe`` () =
        let workflow : Flow<unit, string, int> =
            Seq.init iterations id
            |> Seq.fold (fun acc _ -> acc |> Flow.bind (fun total -> Flow.succeed (total + 1))) (Flow.succeed 0)

        test <@ onSmallStack (fun () -> Flow.runSync () workflow) = Exit.Success iterations @>

    [<Fact>]
    let ``long map and catch chains are stack safe`` () =
        let mapped : Flow<unit, string, int> =
            Seq.init iterations id |> Seq.fold (fun acc _ -> acc |> Flow.map (fun total -> total + 1)) (Flow.succeed 0)
        let recovered : Flow<unit, string, int> =
            Seq.init iterations id |> Seq.fold (fun acc _ -> acc |> Flow.orElse (Flow.succeed 1)) (Flow.fail "boom")

        test <@ onSmallStack (fun () -> Flow.runSync () mapped) = Exit.Success iterations @>
        test <@ onSmallStack (fun () -> Flow.runSync () recovered) = Exit.Success 1 @>

    [<Fact>]
    let ``deeply recursive flows are stack safe`` () =
        // Recursion is deferred with Flow.delay (the builder defers implicitly); an eager
        // `countDown (n - 1) |> Flow.map` recurses in plain F# while building the value, before Axial runs.
        let rec countDown n : Flow<unit, string, int> =
            if n = 0 then Flow.succeed 0
            else Flow.delay (fun () -> countDown (n - 1)) |> Flow.map (fun total -> total + 1)

        let rec tailCountDown n : Flow<unit, string, int> =
            flow {
                if n = 0 then return 0
                else return! tailCountDown (n - 1)
            }

        test <@ onSmallStack (fun () -> Flow.runSync () (countDown iterations)) = Exit.Success iterations @>
        test <@ onSmallStack (fun () -> Flow.runSync () (tailCountDown iterations)) = Exit.Success 0 @>

    [<Fact>]
    let ``Flow sequence of synchronous flows is stack safe`` () =
        let workflow : Flow<unit, string, int list> = Flow.sequence (Seq.init iterations Flow.succeed)
        let exit = onSmallStack (fun () -> Flow.runSync () workflow)
        test <@ (match exit with Exit.Success values -> values.Length = iterations | _ -> false) @>

    [<Fact>]
    let ``large synchronous streams are stack safe`` () =
        let total : Flow<unit, string, int> =
            FlowStream.fromSeq (Seq.init iterations id)
            |> FlowStream.filter (fun value -> value = iterations - 1)
            |> FlowStream.map (fun value -> value + 1)
            |> FlowStream.runFold (fun state value -> state + value) 0
        let counted : Flow<unit, string, int> =
            FlowStream.unfoldFlow (fun n -> Flow.succeed (if n < iterations then Some(n, n + 1) else None)) 0
            |> FlowStream.runFold (fun state _ -> state + 1) 0

        test <@ onSmallStack (fun () -> Flow.runSync () total) = Exit.Success iterations @>
        test <@ onSmallStack (fun () -> Flow.runSync () counted) = Exit.Success iterations @>

    [<Fact>]
    let ``schedule repeat with many recurrences is stack safe`` () =
        let counter = ref 0
        let workflow : Flow<unit, string, int> =
            Flow.delay (fun () -> counter.Value <- counter.Value + 1; Flow.succeed counter.Value)
            |> Schedule.repeat (Schedule.recurs 50_000)

        test <@ onSmallStack (fun () -> Flow.runSync () workflow) = Exit.Success 50_001 @>

    [<Fact>]
    let ``loops stay stack safe when a flow blocks on a synchronization context`` () =
        // A hop off a deep stack must not post back to a context the blocked caller owns.
        let workflow : Flow<unit, string, int> =
            flow {
                let mutable count = 0
                while count < iterations do
                    do! Flow.succeed ()
                    count <- count + 1
                return count
            }

        let exit =
            onSmallStack (fun () ->
                SynchronizationContext.SetSynchronizationContext(SynchronizationContext())
                Flow.runSync () workflow)

        test <@ exit = Exit.Success iterations @>
