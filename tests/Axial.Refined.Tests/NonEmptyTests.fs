namespace Axial.Refined.Tests

open Axial.Refined
open Swensen.Unquote
open Xunit

/// Proves the operations the type exists to make total, and the closure properties
/// consumers are entitled to rely on.
module NonEmptyTests =

    let private sample () = NonEmpty(1, [ 2; 3; 4 ])

    [<Fact>]
    let ``the case is public, so callers can pattern match and construct without a Result`` () =
        let (NonEmpty(head, tail)) = sample ()
        test <@ head = 1 @>
        test <@ tail = [ 2; 3; 4 ] @>
        test <@ NonEmpty(1, [ 2 ]) |> NonEmptyList.toList = [ 1; 2 ] @>
        test <@ NonEmptyList.singleton 9 |> NonEmptyList.toList = [ 9 ] @>

    [<Fact>]
    let ``head, last, min, max, and reduce are total with no option or exception`` () =
        let input = sample ()
        test <@ NonEmptyList.head input = 1 @>
        test <@ NonEmptyList.last input = 4 @>
        test <@ NonEmptyList.min input = 1 @>
        test <@ NonEmptyList.max input = 4 @>
        test <@ NonEmptyList.reduce (+) input = 10 @>

    [<Fact>]
    let ``last and reduce hold on the single-item case`` () =
        let single = NonEmptyList.singleton 7
        test <@ NonEmptyList.last single = 7 @>
        test <@ NonEmptyList.reduce (+) single = 7 @>
        test <@ NonEmptyList.length single = 1 @>

    [<Fact>]
    let ``map, rev, sort, append, and distinct are closed over non-emptiness`` () =
        let input = sample ()
        test <@ NonEmptyList.map ((*) 2) input |> NonEmptyList.toList = [ 2; 4; 6; 8 ] @>
        test <@ NonEmptyList.rev input |> NonEmptyList.toList = [ 4; 3; 2; 1 ] @>
        test <@ NonEmptyList.sort (NonEmpty(3, [ 1; 2 ])) |> NonEmptyList.toList = [ 1; 2; 3 ] @>
        test <@ NonEmptyList.append input (NonEmptyList.singleton 5) |> NonEmptyList.toList = [ 1; 2; 3; 4; 5 ] @>
        test <@ NonEmptyList.distinct (NonEmpty(1, [ 1; 2 ])) |> NonEmptyList.toList = [ 1; 2 ] @>

    [<Fact>]
    let ``map preserves identity and composition`` () =
        let input = sample ()
        test <@ NonEmptyList.map id input = input @>

        let f value = value + 1
        let g value = value * 3
        test <@ NonEmptyList.map (f >> g) input = (input |> NonEmptyList.map f |> NonEmptyList.map g) @>

    [<Fact>]
    let ``rev is its own inverse and append is associative`` () =
        let input = sample ()
        test <@ input |> NonEmptyList.rev |> NonEmptyList.rev = input @>

        let a = NonEmpty(1, [])
        let b = NonEmpty(2, [ 3 ])
        let c = NonEmpty(4, [])
        test <@ NonEmptyList.append (NonEmptyList.append a b) c = NonEmptyList.append a (NonEmptyList.append b c) @>

    [<Fact>]
    let ``narrowing operations admit emptiness honestly`` () =
        let input = sample ()
        test <@ NonEmptyList.filter (fun value -> value > 10) input = [] @>
        test <@ NonEmptyList.tryFilter (fun value -> value > 10) input = None @>
        test <@ NonEmptyList.tryFilter (fun value -> value > 2) input |> Option.map NonEmptyList.toList = Some [ 3; 4 ] @>

    [<Fact>]
    let ``ofList returns None for an empty source and Some otherwise`` () =
        test <@ NonEmptyList.ofList ([]: int list) = None @>
        test <@ NonEmptyList.ofList [ 1 ] |> Option.map NonEmptyList.toList = Some [ 1 ] @>

    [<Fact>]
    let ``traverseResult accumulates every failure rather than stopping at the first`` () =
        let mapping value =
            if value % 2 = 0 then Ok value else Error [ $"odd:{value}" ]

        test <@ NonEmptyList.traverseResult mapping (NonEmpty(2, [ 4 ])) |> Result.map NonEmptyList.toList = Ok [ 2; 4 ] @>
        test <@ NonEmptyList.traverseResult mapping (NonEmpty(1, [ 2; 3 ])) = Error [ "odd:1"; "odd:3" ] @>

    [<Fact>]
    let ``traverseOption succeeds only when every item is present`` () =
        test <@ NonEmptyList.traverseOption Some (NonEmpty(1, [ 2 ])) |> Option.map NonEmptyList.toList = Some [ 1; 2 ] @>
        test <@ NonEmptyList.traverseOption (fun value -> if value > 1 then Some value else None) (NonEmpty(1, [ 2 ])) = None @>

    [<Fact>]
    let ``the refinement still reports the same failure as before the rewrite`` () =
        test <@ NonEmptyList.create ([]: int list) |> Result.isError @>
        test <@ NonEmptyList.create [ 1; 2 ] |> Result.map NonEmptyList.toList = Ok [ 1; 2 ] @>

    [<Fact>]
    let ``zip truncates to the shorter input rather than raising`` () =
        // List.zip raises on a length mismatch, which would make zip partial.
        raises<System.ArgumentException> <@ List.zip [ 1; 2 ] [ 9 ] @>

        let longer = NonEmpty(1, [ 2; 3 ])
        let shorter = NonEmpty(9, [ 8 ])
        test <@ NonEmptyList.zip longer shorter |> NonEmptyList.toList = [ (1, 9); (2, 8) ] @>
        test <@ NonEmptyList.zip shorter longer |> NonEmptyList.toList = [ (9, 1); (8, 2) ] @>
        test <@ NonEmptyList.zip longer longer |> NonEmptyList.toList = [ (1, 1); (2, 2); (3, 3) ] @>

    [<Fact>]
    let ``non-empty arrays keep contiguous storage while making head and reduce total`` () =
        let input = NonEmptyArray.create [ 3; 1; 2 ] |> Result.defaultWith (failwithf "%A")
        test <@ NonEmptyArray.head input = 3 @>
        test <@ NonEmptyArray.last input = 2 @>
        test <@ NonEmptyArray.max input = 3 @>
        test <@ NonEmptyArray.reduce (+) input = 6 @>
        test <@ NonEmptyArray.sort input |> NonEmptyArray.toArray = [| 1; 2; 3 |] @>
        test <@ NonEmptyArray.create ([]: int list) |> Result.isError @>

    [<Fact>]
    let ``non-empty arrays and lists convert without losing the invariant`` () =
        let list = NonEmpty(1, [ 2; 3 ])
        test <@ list |> NonEmptyArray.ofNonEmptyList |> NonEmptyArray.toNonEmptyList = list @>

    [<Fact>]
    let ``the returned array is a copy, so mutation cannot break the invariant`` () =
        let input = NonEmptyArray.singleton 1
        let copy = NonEmptyArray.toArray input
        copy[0] <- 99
        test <@ NonEmptyArray.head input = 1 @>

    [<Fact>]
    let ``non-empty arrays traverse and accumulate like non-empty lists`` () =
        let input = NonEmptyArray.create [ 2; 4 ] |> Result.defaultWith (failwithf "%A")
        let mapping value = if value % 2 = 0 then Ok value else Error [ $"odd:{value}" ]

        test <@ NonEmptyArray.traverseResult mapping input |> Result.map NonEmptyArray.toArray = Ok [| 2; 4 |] @>

        let mixed = NonEmptyArray.create [ 1; 2; 3 ] |> Result.defaultWith (failwithf "%A")
        test <@ NonEmptyArray.traverseResult mapping mixed = Error [ "odd:1"; "odd:3" ] @>
        test <@ NonEmptyArray.traverseOption Some input |> Option.map NonEmptyArray.toArray = Some [| 2; 4 |] @>

    [<Fact>]
    let ``non-empty array closed operations preserve non-emptiness`` () =
        let input = NonEmptyArray.create [ 1; 2; 3 ] |> Result.defaultWith (failwithf "%A")

        test <@ NonEmptyArray.consTo 0 input |> NonEmptyArray.toArray = [| 0; 1; 2; 3 |] @>
        test <@ NonEmptyArray.distinct (NonEmptyArray.create [ 1; 1; 2 ] |> Result.defaultWith (failwithf "%A"))
                    |> NonEmptyArray.toArray = [| 1; 2 |] @>
        test <@ NonEmptyArray.sortDescending input |> NonEmptyArray.toArray = [| 3; 2; 1 |] @>
        test <@ NonEmptyArray.concat (NonEmptyArray.create [ input; input ] |> Result.defaultWith (failwithf "%A"))
                    |> NonEmptyArray.length = 6 @>
        test <@ NonEmptyArray.reduceBack (-) input = 2 @>
        test <@ NonEmptyArray.contains 2 input @>

    [<Fact>]
    let ``non-empty array zip truncates rather than raising`` () =
        let longer = NonEmptyArray.create [ 1; 2; 3 ] |> Result.defaultWith (failwithf "%A")
        let shorter = NonEmptyArray.create [ 9 ] |> Result.defaultWith (failwithf "%A")

        raises<System.ArgumentException> <@ Array.zip [| 1; 2 |] [| 9 |] @>
        test <@ NonEmptyArray.zip longer shorter |> NonEmptyArray.toArray = [| (1, 9) |] @>



    [<Fact>]
    let ``groupBy keeps every group non-empty, because a group only exists if filled`` () =
        let grouped = NonEmpty(1, [ 2; 3; 4; 5 ]) |> NonEmptyList.groupBy (fun value -> value % 2)

        test <@ Map.count grouped = 2 @>
        test <@ grouped[1] |> NonEmptyList.toList = [ 1; 3; 5 ] @>
        test <@ grouped[0] |> NonEmptyList.toList = [ 2; 4 ] @>
        // The payoff: max on a group needs no option.
        test <@ grouped[1] |> NonEmptyList.max = 5 @>

    [<Fact>]
    let ``chunkBySize keeps both the outer list and every chunk non-empty`` () =
        let chunks = NonEmpty(1, [ 2; 3; 4; 5 ]) |> NonEmptyList.chunkBySize 2

        test <@ chunks |> NonEmptyList.toList |> List.map NonEmptyList.toList = [ [ 1; 2 ]; [ 3; 4 ]; [ 5 ] ] @>
        test <@ chunks |> NonEmptyList.head |> NonEmptyList.head = 1 @>

        // List.chunkBySize raises on zero; a size below one is treated as one instead.
        raises<System.ArgumentException> <@ List.chunkBySize 0 [ 1 ] @>
        test <@ NonEmptyList.chunkBySize 0 (NonEmpty(1, [ 2 ])) |> NonEmptyList.length = 2 @>

    [<Fact>]
    let ``an admitted array cannot be mutated through the caller's reference`` () =
        let source = [| 1; 2 |]
        let admitted = Refinement.create (NonEmptyArray.refinement ()) source |> Result.defaultWith (failwithf "%A")
        source[0] <- 99
        test <@ NonEmptyArray.head admitted = 1 @>
