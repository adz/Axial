namespace Axial.Tests

open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

module StreamInteropTests =
    let private asyncEnumerableOf (items: int list) =
        { new System.Collections.Generic.IAsyncEnumerable<int> with
            member _.GetAsyncEnumerator(_) =
                let mutable remaining = items
                let mutable current = None

                { new System.Collections.Generic.IAsyncEnumerator<int> with
                    member _.Current = current.Value

                    member _.MoveNextAsync() =
                        match remaining with
                        | [] ->
                            current <- None
                            ValueTask<bool>(false)
                        | head :: rest ->
                            current <- Some head
                            remaining <- rest
                            ValueTask<bool>(true)

                    member _.DisposeAsync() = ValueTask() } }

    [<Fact>]
    let ``ofAsyncEnumerable pulls all values from an async enumerable`` () =
        let result =
            asyncEnumerableOf [ 1; 2; 3 ]
            |> FlowStream.ofAsyncEnumerable
            |> FlowStream.runCollect
            |> Flow.runSync ()

        test <@ result = Exit.Success [ 1; 2; 3 ] @>

    [<Fact>]
    let ``ofAsyncEnumerable restarts from the source on each run`` () =
        let stream = asyncEnumerableOf [ 1; 2; 3 ] |> FlowStream.ofAsyncEnumerable

        let first = stream |> FlowStream.runCollect |> Flow.runSync ()
        let second = stream |> FlowStream.runCollect |> Flow.runSync ()

        test <@ first = Exit.Success [ 1; 2; 3 ] @>
        test <@ second = Exit.Success [ 1; 2; 3 ] @>

    [<Fact>]
    let ``ofAsyncEnumerable disposes the enumerator when consumption is cut short`` () =
        let disposed = ref 0

        let source =
            { new System.Collections.Generic.IAsyncEnumerable<int> with
                member _.GetAsyncEnumerator(_) =
                    let mutable remaining = [ 1; 2; 3 ]
                    let mutable current = None

                    { new System.Collections.Generic.IAsyncEnumerator<int> with
                        member _.Current = current.Value

                        member _.MoveNextAsync() =
                            match remaining with
                            | [] ->
                                current <- None
                                ValueTask<bool>(false)
                            | head :: rest ->
                                current <- Some head
                                remaining <- rest
                                ValueTask<bool>(true)

                        member _.DisposeAsync() =
                            disposed.Value <- disposed.Value + 1
                            ValueTask() } }

        let result =
            source
            |> FlowStream.ofAsyncEnumerable
            |> FlowStream.runTryHead
            |> Flow.runSync ()

        test <@ result = Exit.Success (Some 1) @>
        test <@ disposed.Value = 1 @>

    [<Fact>]
    let ``ofAsyncEnumerable reads a completed channel through ReadAllAsync`` () =
        let channel = System.Threading.Channels.Channel.CreateUnbounded<int>()
        channel.Writer.TryWrite 1 |> ignore
        channel.Writer.TryWrite 2 |> ignore
        channel.Writer.TryComplete() |> ignore

        let result =
            channel.Reader.ReadAllAsync()
            |> FlowStream.ofAsyncEnumerable
            |> FlowStream.runCollect
            |> Flow.runSync ()

        test <@ result = Exit.Success [ 1; 2 ] @>
