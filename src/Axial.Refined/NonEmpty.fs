namespace Axial.Refined

open Axial.Check

/// <summary>A list that contains at least one item.</summary>
/// <remarks>
/// The case is public: non-emptiness is carried by the representation rather than by a
/// checked constructor, so <c>head</c>, <c>last</c>, <c>reduce</c>, <c>min</c>, and
/// <c>max</c> are total and pattern matching is available to callers.
/// </remarks>
type NonEmptyList<'value> =
    | NonEmpty of head: 'value * tail: 'value list

    /// <summary>Returns the first item.</summary>
    member this.Head =
        let (NonEmpty(head, _)) = this
        head

    /// <summary>Returns the remaining items.</summary>
    member this.Tail =
        let (NonEmpty(_, tail)) = this
        tail

    /// <summary>Returns the refined value as a standard list.</summary>
    member this.ToList() =
        this.Head :: this.Tail

    interface seq<'value> with
        member this.GetEnumerator() =
            (this.ToList() :> seq<'value>).GetEnumerator()

        member this.GetEnumerator() =
            (this.ToList() :> System.Collections.IEnumerable).GetEnumerator()

/// <summary>An array that contains at least one item.</summary>
/// <remarks>
/// Unlike <see cref="T:Axial.Refined.NonEmptyList`1"/> this stays smart-constructed. A
/// structural head-and-tail representation would forfeit contiguous storage and indexed
/// access, which are the reasons to choose an array in the first place.
/// </remarks>
type NonEmptyArray<'value> =
    private
    | NonEmptyArray of 'value array

    /// <summary>Returns the first item.</summary>
    member this.Head =
        let (NonEmptyArray values) = this
        values[0]

    /// <summary>Returns all items after the head.</summary>
    member this.Tail =
        let (NonEmptyArray values) = this
        values[1..]

    /// <summary>Returns the number of items, which is always at least one.</summary>
    member this.Length =
        let (NonEmptyArray values) = this
        values.Length

    /// <summary>Returns a copy of the refined value as a standard array.</summary>
    member this.ToArray() =
        let (NonEmptyArray values) = this
        Array.copy values

    interface seq<'value> with
        member this.GetEnumerator() =
            (this.ToArray() :> seq<'value>).GetEnumerator()

        member this.GetEnumerator() =
            (this.ToArray() :> System.Collections.IEnumerable).GetEnumerator()

/// Operations over lists that carry their non-emptiness in the type.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module NonEmptyList =

    // Construction ---------------------------------------------------------------------

    /// <summary>
    /// Converts a list whose non-emptiness the caller has already established. Internal,
    /// and the single place in the package where that obligation is discharged by
    /// assertion rather than by the type — the refinement layer calls it only after its
    /// length check has passed.
    /// </summary>
    let internal ofCheckedList values =
        match values with
        | head :: tail -> NonEmpty(head, tail)
        | [] -> invalidArg (nameof values) "A non-empty list cannot be built from an empty list."

    /// <summary>Builds a list of exactly one item.</summary>
    let singleton value = NonEmpty(value, [])

    /// <summary>Prepends an item to a standard list.</summary>
    let cons head tail = NonEmpty(head, tail)

    /// <summary>Prepends an item to an already non-empty list.</summary>
    let consTo head (input: NonEmptyList<'value>) = NonEmpty(head, input.ToList())

    /// <summary>Returns the non-empty list, or <c>None</c> when the source is empty.</summary>
    let ofList values =
        match values with
        | head :: tail -> Some(NonEmpty(head, tail))
        | [] -> None

    /// <summary>Returns the non-empty list, or <c>None</c> when the source is empty.</summary>
    let ofSeq values = values |> List.ofSeq |> ofList

    /// <summary>Returns the non-empty list, or <c>None</c> when the source is empty.</summary>
    let ofArray (values: 'value array) = values |> List.ofArray |> ofList

    /// <summary>Returns a refinement admitting any list with at least one item.</summary>
    let refinement<'value> () : Refinement<'value list, NonEmptyList<'value>> =
        let constraint': Constraint<'value list> = Constraint.minLength 1
        Refinement.define constraint' ofCheckedList _.ToList()

    /// <summary>Admits a non-empty list, reporting the same failure the refinement does.</summary>
    let create (values: 'value seq) : Result<NonEmptyList<'value>, CheckFailure list> =
        values |> List.ofSeq |> Refinement.create (refinement ())

    /// <summary>Converts to a non-empty array, preserving order.</summary>
    let toArray (input: NonEmptyList<'value>) = input.ToList() |> List.toArray

    /// <summary>Returns the refined value as a standard list.</summary>
    let toList (input: NonEmptyList<'value>) = input.ToList()

    /// <summary>Returns the refined value as a sequence.</summary>
    let toSeq (input: NonEmptyList<'value>) = input.ToList() |> List.toSeq

    // Total projections ----------------------------------------------------------------

    /// <summary>Returns the first item. Total.</summary>
    let head (input: NonEmptyList<'value>) = input.Head

    /// <summary>Returns every item after the first.</summary>
    let tail (input: NonEmptyList<'value>) = input.Tail

    /// <summary>Returns the final item. Total.</summary>
    let last (input: NonEmptyList<'value>) =
        match input.Tail with
        | [] -> input.Head
        | tail -> List.last tail

    /// <summary>Returns the number of items as a plain <c>int</c>, matching <c>List.length</c>.</summary>
    let length (input: NonEmptyList<'value>) = 1 + List.length input.Tail


    // Invariant-preserving operations --------------------------------------------------

    /// <summary>Applies a mapping to every item. Non-emptiness is preserved.</summary>
    let map mapping (input: NonEmptyList<'value>) =
        let (NonEmpty(head, tail)) = input
        NonEmpty(mapping head, List.map mapping tail)

    /// <summary>Applies an index-aware mapping to every item. Non-emptiness is preserved.</summary>
    let mapi mapping (input: NonEmptyList<'value>) =
        let (NonEmpty(head, tail)) = input
        NonEmpty(mapping 0 head, tail |> List.mapi (fun index value -> mapping (index + 1) value))

    /// <summary>Pairs every item with its index. Non-emptiness is preserved.</summary>
    let indexed (input: NonEmptyList<'value>) = mapi (fun index value -> index, value) input

    /// <summary>Concatenates two non-empty lists.</summary>
    let append (first: NonEmptyList<'value>) (second: NonEmptyList<'value>) =
        NonEmpty(first.Head, first.Tail @ second.ToList())

    /// <summary>Appends a standard list to a non-empty list.</summary>
    let appendList (first: NonEmptyList<'value>) values =
        NonEmpty(first.Head, first.Tail @ values)

    /// <summary>Maps each item to a non-empty list and concatenates the results.</summary>
    let collect (mapping: 'value -> NonEmptyList<'result>) (input: NonEmptyList<'value>) =
        let (NonEmpty(head, tail)) = input
        let (NonEmpty(resultHead, resultTail)) = mapping head
        NonEmpty(resultHead, resultTail @ (tail |> List.collect (mapping >> toList)))

    /// <summary>Concatenates a non-empty list of non-empty lists.</summary>
    let concat (input: NonEmptyList<NonEmptyList<'value>>) = collect id input

    /// <summary>Reverses the order of the items.</summary>
    let rev (input: NonEmptyList<'value>) =
        match input.ToList() |> List.rev with
        | head :: tail -> NonEmpty(head, tail)
        | [] -> input

    /// <summary>Sorts the items in ascending order.</summary>
    let sort (input: NonEmptyList<'value>) =
        match input.ToList() |> List.sort with
        | head :: tail -> NonEmpty(head, tail)
        | [] -> input

    /// <summary>Sorts the items by a projected key.</summary>
    let sortBy projection (input: NonEmptyList<'value>) =
        match input.ToList() |> List.sortBy projection with
        | head :: tail -> NonEmpty(head, tail)
        | [] -> input

    /// <summary>Sorts the items using an explicit comparison.</summary>
    let sortWith comparer (input: NonEmptyList<'value>) =
        match input.ToList() |> List.sortWith comparer with
        | head :: tail -> NonEmpty(head, tail)
        | [] -> input

    /// <summary>Sorts the items in descending order.</summary>
    let sortDescending (input: NonEmptyList<'value>) =
        match input.ToList() |> List.sortDescending with
        | head :: tail -> NonEmpty(head, tail)
        | [] -> input

    /// <summary>Removes duplicate items, preserving first-seen order. Non-emptiness is preserved.</summary>
    let distinct (input: NonEmptyList<'value>) =
        match input.ToList() |> List.distinct with
        | head :: tail -> NonEmpty(head, tail)
        | [] -> input

    /// <summary>
    /// Pairs items positionally, truncating to the shorter input. Total — unlike
    /// <c>List.zip</c>, which raises when the lengths differ.
    /// </summary>
    let zip (first: NonEmptyList<'first>) (second: NonEmptyList<'second>) =
        let rec loop left right accumulated =
            match left, right with
            | leftHead :: leftTail, rightHead :: rightTail ->
                loop leftTail rightTail ((leftHead, rightHead) :: accumulated)
            | _ -> List.rev accumulated

        NonEmpty((first.Head, second.Head), loop first.Tail second.Tail [])

    /// <summary>Splits a non-empty list of pairs into a pair of non-empty lists.</summary>
    let unzip (input: NonEmptyList<'first * 'second>) =
        let (NonEmpty((firstHead, secondHead), tail)) = input
        let firstTail, secondTail = List.unzip tail
        NonEmpty(firstHead, firstTail), NonEmpty(secondHead, secondTail)

    // Total folds ----------------------------------------------------------------------

    /// <summary>Combines every item with an associative operation. Total — no seed required.</summary>
    let reduce reduction (input: NonEmptyList<'value>) =
        let (NonEmpty(head, tail)) = input
        List.fold reduction head tail

    /// <summary>Combines every item from the right. Total — no seed required.</summary>
    let reduceBack reduction (input: NonEmptyList<'value>) =
        input.ToList() |> List.reduceBack reduction

    /// <summary>Folds over the items from an explicit seed.</summary>
    let fold folder state (input: NonEmptyList<'value>) =
        let (NonEmpty(head, tail)) = input
        List.fold folder (folder state head) tail

    /// <summary>Folds over the items from the right.</summary>
    let foldBack folder (input: NonEmptyList<'value>) state =
        List.foldBack folder (input.ToList()) state

    /// <summary>Returns the smallest item. Total.</summary>
    let min (input: NonEmptyList<'value>) = reduce Operators.min input

    /// <summary>Returns the largest item. Total.</summary>
    let max (input: NonEmptyList<'value>) = reduce Operators.max input

    /// <summary>Returns the item with the smallest projected key. Total.</summary>
    let minBy projection (input: NonEmptyList<'value>) =
        reduce (fun left right -> if projection right < projection left then right else left) input

    /// <summary>Returns the item with the largest projected key. Total.</summary>
    let maxBy projection (input: NonEmptyList<'value>) =
        reduce (fun left right -> if projection right > projection left then right else left) input

    // Narrowing operations -------------------------------------------------------------

    /// <summary>Filters the items, returning a standard list because emptiness is possible.</summary>
    let filter predicate (input: NonEmptyList<'value>) = input.ToList() |> List.filter predicate

    /// <summary>Filters the items, returning <c>None</c> when nothing survives.</summary>
    let tryFilter predicate input = input |> filter predicate |> ofList

    /// <summary>Partitions the items into matching and non-matching standard lists.</summary>
    let partition predicate (input: NonEmptyList<'value>) = input.ToList() |> List.partition predicate

    /// <summary>Returns whether any item satisfies the predicate.</summary>
    let exists predicate (input: NonEmptyList<'value>) = input.ToList() |> List.exists predicate

    /// <summary>Returns whether every item satisfies the predicate.</summary>
    let forall predicate (input: NonEmptyList<'value>) = input.ToList() |> List.forall predicate

    /// <summary>Returns whether the list contains the item.</summary>
    let contains value (input: NonEmptyList<'value>) = input.ToList() |> List.contains value

    /// <summary>Returns the first matching item, if any.</summary>
    let tryFind predicate (input: NonEmptyList<'value>) = input.ToList() |> List.tryFind predicate

    /// <summary>Applies an action to every item.</summary>
    let iter action (input: NonEmptyList<'value>) = input.ToList() |> List.iter action

    /// <summary>
    /// Groups items by a key. Every group is non-empty by construction — a group only
    /// exists because something fell into it — so the values keep their type rather than
    /// degrading to a list the caller has to re-check.
    /// </summary>
    let groupBy projection (input: NonEmptyList<'value>) =
        input.ToList()
        |> List.groupBy projection
        |> List.map (fun (key, items) -> key, ofCheckedList items)
        |> Map.ofList

    /// <summary>
    /// Splits into consecutive runs of the given size. Total: a size below one is treated
    /// as one, where <c>List.chunkBySize</c> raises, and both the outer list and every
    /// chunk stay non-empty.
    /// </summary>
    let chunkBySize size (input: NonEmptyList<'value>) =
        input.ToList()
        |> List.chunkBySize (Operators.max 1 size)
        |> List.map ofCheckedList
        |> ofCheckedList

    // Effectful traversals -------------------------------------------------------------

    /// <summary>
    /// Applies a fallible mapping to every item, accumulating every failure rather than
    /// stopping at the first.
    /// </summary>
    let traverseResult (mapping: 'value -> Result<'result, 'failure list>) (input: NonEmptyList<'value>) =
        let folded =
            input.ToList()
            |> List.fold
                (fun state value ->
                    match state, mapping value with
                    | Ok results, Ok result -> Ok(result :: results)
                    | Ok _, Error failures -> Error failures
                    | Error failures, Ok _ -> Error failures
                    | Error failures, Error moreFailures -> Error(failures @ moreFailures))
                (Ok [])

        match folded with
        | Ok results ->
            match List.rev results with
            | head :: tail -> Ok(NonEmpty(head, tail))
            | [] -> Error []
        | Error failures -> Error failures

    /// <summary>Collects a non-empty list of results, accumulating every failure.</summary>
    let sequenceResult (input: NonEmptyList<Result<'value, 'failure list>>) = traverseResult id input

    /// <summary>Applies a mapping that may yield nothing, succeeding only when every item does.</summary>
    let traverseOption (mapping: 'value -> 'result option) (input: NonEmptyList<'value>) =
        let (NonEmpty(head, tail)) = input

        match mapping head with
        | None -> None
        | Some mappedHead ->
            let rec loop remaining accumulated =
                match remaining with
                | [] -> Some(NonEmpty(mappedHead, List.rev accumulated))
                | value :: rest ->
                    match mapping value with
                    | None -> None
                    | Some mapped -> loop rest (mapped :: accumulated)

            loop tail []

    /// <summary>Collects a non-empty list of options, succeeding only when every item is present.</summary>
    let sequenceOption (input: NonEmptyList<'value option>) = traverseOption id input

/// Operations over arrays that carry their non-emptiness in the type.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module NonEmptyArray =

    /// <summary>
    /// Constructs from an array whose non-emptiness the caller has already established.
    /// Internal because the invariant is carried by the caller rather than by the
    /// representation. Copies, so a caller holding the source array cannot mutate the
    /// contents of a value that has already been admitted.
    /// </summary>
    let internal ofCheckedArray (values: 'value array) = NonEmptyArray(Array.copy values)

    /// <summary>
    /// Converts a list whose non-emptiness the caller has already established. Used by
    /// the schema adapters, whose wire shape is a list.
    /// </summary>
    let internal ofCheckedList (values: 'value list) = NonEmptyArray(List.toArray values)

    /// <summary>Builds an array of exactly one item.</summary>
    let singleton value = NonEmptyArray [| value |]

    /// <summary>Returns the non-empty array, or <c>None</c> when the source is empty.</summary>
    let ofArray (values: 'value array) =
        if values.Length = 0 then None else Some(NonEmptyArray(Array.copy values))

    /// <summary>Returns the non-empty array, or <c>None</c> when the source is empty.</summary>
    let ofSeq values = values |> Array.ofSeq |> ofArray

    /// <summary>Returns the non-empty array, or <c>None</c> when the source is empty.</summary>
    let ofList values = values |> Array.ofList |> ofArray

    /// <summary>Returns a refinement admitting any array with at least one item.</summary>
    let refinement<'value> () : Refinement<'value array, NonEmptyArray<'value>> =
        let constraint': Constraint<'value array> = Constraint.minLength 1
        Refinement.define constraint' ofCheckedArray _.ToArray()

    /// <summary>
    /// Returns a refinement admitting a non-empty array from a list, which is the shape
    /// structured wire formats use.
    /// </summary>
    let listRefinement<'value> () : Refinement<'value list, NonEmptyArray<'value>> =
        let constraint': Constraint<'value list> = Constraint.minLength 1
        Refinement.define constraint' ofCheckedList (fun value -> value.ToArray() |> Array.toList)

    /// <summary>Admits a non-empty array, reporting the same failure the refinement does.</summary>
    let create (values: 'value seq) : Result<NonEmptyArray<'value>, CheckFailure list> =
        values |> Array.ofSeq |> Refinement.create (refinement ())

    /// <summary>Returns a copy of the refined value as a standard array.</summary>
    let toArray (input: NonEmptyArray<'value>) = input.ToArray()

    /// <summary>Returns the refined value as a standard list.</summary>
    let toList (input: NonEmptyArray<'value>) = input.ToArray() |> Array.toList

    /// <summary>Converts to a non-empty list.</summary>
    let toNonEmptyList (input: NonEmptyArray<'value>) = NonEmpty(input.Head, input.Tail |> Array.toList)

    /// <summary>Converts from a non-empty list.</summary>
    let ofNonEmptyList (input: NonEmptyList<'value>) = NonEmptyArray(input.ToList() |> List.toArray)

    /// <summary>Returns the first item. Total.</summary>
    let head (input: NonEmptyArray<'value>) = input.Head

    /// <summary>Returns every item after the first.</summary>
    let tail (input: NonEmptyArray<'value>) = input.Tail

    /// <summary>Returns the final item. Total.</summary>
    let last (input: NonEmptyArray<'value>) =
        let values = input.ToArray()
        values[values.Length - 1]

    /// <summary>Returns the number of items as a plain <c>int</c>, matching <c>Array.length</c>.</summary>
    let length (input: NonEmptyArray<'value>) = input.Length

    /// <summary>Applies a mapping to every item. Non-emptiness is preserved.</summary>
    let map mapping (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.map mapping)

    /// <summary>Applies an index-aware mapping to every item. Non-emptiness is preserved.</summary>
    let mapi mapping (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.mapi mapping)

    /// <summary>Concatenates two non-empty arrays.</summary>
    let append (first: NonEmptyArray<'value>) (second: NonEmptyArray<'value>) =
        NonEmptyArray(Array.append (first.ToArray()) (second.ToArray()))

    /// <summary>Reverses the order of the items.</summary>
    let rev (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.rev)

    /// <summary>Sorts the items in ascending order.</summary>
    let sort (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.sort)

    /// <summary>Sorts the items by a projected key.</summary>
    let sortBy projection (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.sortBy projection)

    /// <summary>Sorts the items using an explicit comparison.</summary>
    let sortWith comparer (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.sortWith comparer)

    /// <summary>Combines every item with an associative operation. Total — no seed required.</summary>
    let reduce reduction (input: NonEmptyArray<'value>) = input.ToArray() |> Array.reduce reduction

    /// <summary>Folds over the items from an explicit seed.</summary>
    let fold folder state (input: NonEmptyArray<'value>) = input.ToArray() |> Array.fold folder state

    /// <summary>Returns the smallest item. Total.</summary>
    let min (input: NonEmptyArray<'value>) = reduce Operators.min input

    /// <summary>Returns the largest item. Total.</summary>
    let max (input: NonEmptyArray<'value>) = reduce Operators.max input

    /// <summary>Returns the item with the smallest projected key. Total.</summary>
    let minBy projection (input: NonEmptyArray<'value>) =
        reduce (fun left right -> if projection right < projection left then right else left) input

    /// <summary>Returns the item with the largest projected key. Total.</summary>
    let maxBy projection (input: NonEmptyArray<'value>) =
        reduce (fun left right -> if projection right > projection left then right else left) input

    /// <summary>Filters the items, returning a standard array because emptiness is possible.</summary>
    let filter predicate (input: NonEmptyArray<'value>) = input.ToArray() |> Array.filter predicate

    /// <summary>Filters the items, returning <c>None</c> when nothing survives.</summary>
    let tryFilter predicate input = input |> filter predicate |> ofArray

    /// <summary>Returns whether any item satisfies the predicate.</summary>
    let exists predicate (input: NonEmptyArray<'value>) = input.ToArray() |> Array.exists predicate

    /// <summary>Returns whether every item satisfies the predicate.</summary>
    let forall predicate (input: NonEmptyArray<'value>) = input.ToArray() |> Array.forall predicate

    /// <summary>Applies an action to every item.</summary>
    let iter action (input: NonEmptyArray<'value>) = input.ToArray() |> Array.iter action

    /// <summary>Prepends an item to an already non-empty array.</summary>
    let consTo head (input: NonEmptyArray<'value>) =
        NonEmptyArray(Array.append [| head |] (input.ToArray()))

    /// <summary>Pairs every item with its index. Non-emptiness is preserved.</summary>
    let indexed (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.indexed)

    /// <summary>Maps each item to a non-empty array and concatenates the results.</summary>
    let collect (mapping: 'value -> NonEmptyArray<'result>) (input: NonEmptyArray<'value>) =
        NonEmptyArray(input.ToArray() |> Array.collect (mapping >> toArray))

    /// <summary>Concatenates a non-empty array of non-empty arrays.</summary>
    let concat (input: NonEmptyArray<NonEmptyArray<'value>>) = collect id input

    /// <summary>Sorts the items in descending order.</summary>
    let sortDescending (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.sortDescending)

    /// <summary>Removes duplicate items, preserving first-seen order. Non-emptiness is preserved.</summary>
    let distinct (input: NonEmptyArray<'value>) = NonEmptyArray(input.ToArray() |> Array.distinct)

    /// <summary>Pairs items positionally, truncating to the shorter input. Total.</summary>
    let zip (first: NonEmptyArray<'first>) (second: NonEmptyArray<'second>) =
        let left = first.ToArray()
        let right = second.ToArray()
        let shared = Operators.min left.Length right.Length
        NonEmptyArray(Array.init shared (fun index -> left[index], right[index]))

    /// <summary>Splits a non-empty array of pairs into a pair of non-empty arrays.</summary>
    let unzip (input: NonEmptyArray<'first * 'second>) =
        let first, second = input.ToArray() |> Array.unzip
        NonEmptyArray first, NonEmptyArray second

    /// <summary>Combines every item from the right. Total — no seed required.</summary>
    let reduceBack reduction (input: NonEmptyArray<'value>) = input.ToArray() |> Array.reduceBack reduction

    /// <summary>Folds over the items from the right.</summary>
    let foldBack folder (input: NonEmptyArray<'value>) state =
        Array.foldBack folder (input.ToArray()) state

    /// <summary>Partitions the items into matching and non-matching standard arrays.</summary>
    let partition predicate (input: NonEmptyArray<'value>) = input.ToArray() |> Array.partition predicate

    /// <summary>Returns whether the array contains the item.</summary>
    let contains value (input: NonEmptyArray<'value>) = input.ToArray() |> Array.contains value

    /// <summary>Returns the first matching item, if any.</summary>
    let tryFind predicate (input: NonEmptyArray<'value>) = input.ToArray() |> Array.tryFind predicate

    /// <summary>Returns the refined value as a sequence.</summary>
    let toSeq (input: NonEmptyArray<'value>) = input.ToArray() |> Array.toSeq

    /// <summary>
    /// Applies a fallible mapping to every item, accumulating every failure rather than
    /// stopping at the first.
    /// </summary>
    let traverseResult (mapping: 'value -> Result<'result, 'failure list>) (input: NonEmptyArray<'value>) =
        input
        |> toNonEmptyList
        |> NonEmptyList.traverseResult mapping
        |> Result.map ofNonEmptyList

    /// <summary>Collects a non-empty array of results, accumulating every failure.</summary>
    let sequenceResult (input: NonEmptyArray<Result<'value, 'failure list>>) = traverseResult id input

    /// <summary>Applies a mapping that may yield nothing, succeeding only when every item does.</summary>
    let traverseOption (mapping: 'value -> 'result option) (input: NonEmptyArray<'value>) =
        input
        |> toNonEmptyList
        |> NonEmptyList.traverseOption mapping
        |> Option.map ofNonEmptyList

    /// <summary>Collects a non-empty array of options, succeeding only when every item is present.</summary>
    let sequenceOption (input: NonEmptyArray<'value option>) = traverseOption id input

    /// <summary>Groups items by a key. Every group is non-empty by construction.</summary>
    let groupBy projection (input: NonEmptyArray<'value>) =
        input.ToArray()
        |> Array.groupBy projection
        |> Array.map (fun (key, items) -> key, NonEmptyArray items)
        |> Map.ofArray

    /// <summary>
    /// Splits into consecutive runs of the given size, treating a size below one as one.
    /// Every chunk is non-empty.
    /// </summary>
    let chunkBySize size (input: NonEmptyArray<'value>) =
        input.ToArray()
        |> Array.chunkBySize (Operators.max 1 size)
        |> Array.map NonEmptyArray
        |> NonEmptyArray
