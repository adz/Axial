namespace Axial.Refined

#nowarn "0064" // SRTP dispatch intentionally constrains its marker type while preserving source and destination types.

open System
open System.Collections.Generic
open System.Globalization
open Axial.Constraint

/// <summary>A string that is not null, empty, or whitespace.</summary>
type NonBlankString =
    private
    | NonBlankString of string

    /// <summary>Returns the underlying string value.</summary>
    member this.Value =
        let (NonBlankString value) = this
        value

    override this.ToString() =
        this.Value

/// <summary>A list with no duplicate items, preserving first-seen order.</summary>
type DistinctList<'value when 'value: equality> =
    private
    | DistinctList of 'value list

    /// <summary>Returns the refined value as a standard list.</summary>
    member this.ToList() =
        let (DistinctList values) = this
        values

    interface seq<'value> with
        member this.GetEnumerator() =
            (this.ToList() :> seq<'value>).GetEnumerator()

        member this.GetEnumerator() =
            (this.ToList() :> System.Collections.IEnumerable).GetEnumerator()

/// Text refined value constructors and helpers.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Text =
    let nonBlankStringRefinement = Refinement.define Constraint.present NonBlankString _.Value
    let nonBlankString value = Refinement.create nonBlankStringRefinement value

/// Operations over text known to carry non-whitespace content.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NonBlankString =
    let refinement = Text.nonBlankStringRefinement

    /// <summary>Returns the underlying string value.</summary>
    let value (input: NonBlankString) = input.Value

    /// <summary>Admits text that is not null, empty, or whitespace.</summary>
    let create value = Refinement.create refinement value

    /// <summary>Applies a mapping and re-admits the result, which may no longer be inhabited.</summary>
    let map mapping (input: NonBlankString) = input.Value |> mapping |> create

    // Invariant-preserving operations. Each of these cannot produce blank text from
    // non-blank input, so none of them needs to return a Result.

    /// <summary>Concatenates two inhabited strings. Total — the result is still inhabited.</summary>
    let append (left: NonBlankString) (right: NonBlankString) = NonBlankString(left.Value + right.Value)

    /// <summary>Concatenates with a separator. Total.</summary>
    let join (separator: string) (values: NonEmptyList<NonBlankString>) =
        values |> NonEmptyList.toList |> List.map value |> String.concat separator |> NonBlankString

    /// <summary>Trims surrounding whitespace. Total — trimming inhabited text leaves it inhabited.</summary>
    let trim (input: NonBlankString) = NonBlankString(input.Value.Trim())

    /// <summary>Converts to upper case. Total.</summary>
    let toUpper (input: NonBlankString) = NonBlankString(input.Value.ToUpperInvariant())

    /// <summary>Converts to lower case. Total.</summary>
    let toLower (input: NonBlankString) = NonBlankString(input.Value.ToLowerInvariant())

    /// <summary>Returns the length as a plain <c>int</c>, matching <c>String.length</c>.</summary>
    let length (input: NonBlankString) = input.Value.Length


    /// <summary>
    /// Splits on a separator, discarding blank segments. Returns a non-empty list because
    /// inhabited text always yields at least one inhabited segment.
    /// </summary>
    let split (separator: string) (input: NonBlankString) =
        input.Value.Split([| separator |], StringSplitOptions.None)
        |> Array.filter (fun segment -> not (String.IsNullOrWhiteSpace segment))
        |> Array.map NonBlankString
        |> Array.toList
        |> function
            | head :: tail -> NonEmpty(head, tail)
            | [] -> NonEmpty(trim input, [])

/// Character predicate helpers.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Character =
    let isAsciiDigit value = value >= '0' && value <= '9'
    let isAsciiHexDigit value = (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F')
    let isLowercase (value: char) = Char.IsLower value
    let isUppercase (value: char) = Char.IsUpper value
    let isWhitespace (value: char) = Char.IsWhiteSpace value
    let isControl (value: char) = Char.IsControl value
    let isNumeric (value: char) = Char.IsNumber value

/// Collection refined value constructors and helpers.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Collection =
    let nonEmptyListRefinement<'value> () = NonEmptyList.refinement<'value> ()
    let nonEmptyArrayRefinement<'value> () = NonEmptyArray.refinement<'value> ()

    /// Admits a non-empty array from a list, which is the shape structured wire formats use.
    let nonEmptyArrayFromListRefinement<'value> () = NonEmptyArray.listRefinement<'value> ()

    let distinctListRefinement<'value when 'value: equality> () =
        let constraint': Constraint<'value list> = Constraint.distinct<'value list>
        Refinement.define constraint' DistinctList _.ToList()
    let nonEmptyList values = values |> Seq.toList |> Refinement.create (nonEmptyListRefinement ())
    let nonEmptyArray values = values |> Seq.toArray |> Refinement.create (nonEmptyArrayRefinement ())
    let distinctList values = values |> Seq.toList |> Refinement.create (distinctListRefinement ())

/// Operations over lists known to hold no duplicates.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module DistinctList =
    let refinement<'value when 'value: equality> () = Collection.distinctListRefinement<'value> ()

    /// <summary>Returns the refined value as a standard list.</summary>
    let toList (input: DistinctList<'value>) = input.ToList()

    /// <summary>Admits a list, failing when it holds duplicates.</summary>
    let create values = Collection.distinctList values

    /// <summary>Removes duplicates rather than rejecting them, preserving first-seen order. Total.</summary>
    let ofSeq values = values |> Seq.toList |> List.distinct |> DistinctList

    /// <summary>The list with no items.</summary>
    let empty<'value when 'value: equality> : DistinctList<'value> = DistinctList []

    /// <summary>Returns the number of items as a plain <c>int</c>.</summary>
    let length (input: DistinctList<'value>) = input.ToList() |> List.length


    /// <summary>Returns whether the item is present.</summary>
    let contains item (input: DistinctList<'value>) = input.ToList() |> List.contains item

    /// <summary>
    /// Builds a map keyed by a projection of each item, failing when the projection sends
    /// two items to the same key. Distinct items do not imply distinct projections.
    /// </summary>
    let toMapBy projection (input: DistinctList<'value>) =
        let items = input.ToList()
        let keys = items |> List.map projection

        if List.length (List.distinct keys) = List.length keys then
            Ok(items |> List.map (fun item -> projection item, item) |> Map.ofList)
        else
            Error(Atomic(Expected(UniquenessAtom, None)))

    /// <summary>
    /// Builds a map from a distinct list of pairs, failing when two pairs share a key.
    /// </summary>
    /// <remarks>
    /// Distinctness holds over whole pairs, not over keys: <c>[ 1, "a"; 1, "b" ]</c> is a
    /// legitimate <c>DistinctList</c> whose entries would collide in a map. The check is
    /// what makes the conversion lossless — <c>Map.ofList</c> would silently keep one.
    /// For the unconditional guarantee use <c>toSet</c>, where distinct elements always
    /// produce a set of the same size.
    /// </remarks>
    let toMap (input: DistinctList<'key * 'value>) =
        let pairs = input.ToList()
        let keys = pairs |> List.map fst

        if List.length (List.distinct keys) = List.length keys then
            Ok(Map.ofList pairs)
        else
            Error(Atomic(Expected(UniquenessAtom, None)))

    /// <summary>
    /// Builds a set. Total and lossless — this is the operation that justifies the type,
    /// because distinct items always produce a set of the same size, while
    /// <c>Set.ofList</c> on an ordinary list silently collapses duplicates.
    /// </summary>
    let toSet (input: DistinctList<'value>) = input.ToList() |> Set.ofList

    // Closed operations. Each preserves distinctness, so none returns a Result.

    /// <summary>Adds an item, ignoring it when already present. Total.</summary>
    let add item (input: DistinctList<'value>) =
        let values = input.ToList()
        if List.contains item values then input else DistinctList(values @ [ item ])

    /// <summary>Removes an item. Total.</summary>
    let remove item (input: DistinctList<'value>) =
        DistinctList(input.ToList() |> List.filter (fun existing -> existing <> item))

    /// <summary>Returns the items of either list, without duplicates. Total.</summary>
    let union (first: DistinctList<'value>) (second: DistinctList<'value>) =
        DistinctList(first.ToList() @ second.ToList() |> List.distinct)

    /// <summary>Returns the items present in both lists. Total.</summary>
    let intersect (first: DistinctList<'value>) (second: DistinctList<'value>) =
        let other = second.ToList()
        DistinctList(first.ToList() |> List.filter (fun item -> List.contains item other))

    /// <summary>Returns the items of the first list absent from the second. Total.</summary>
    let difference (first: DistinctList<'value>) (second: DistinctList<'value>) =
        let other = second.ToList()
        DistinctList(first.ToList() |> List.filter (fun item -> not (List.contains item other)))

    /// <summary>Keeps the matching items. Total — filtering cannot introduce duplicates.</summary>
    let filter predicate (input: DistinctList<'value>) =
        DistinctList(input.ToList() |> List.filter predicate)

    /// <summary>
    /// Applies a mapping and removes any duplicates it introduces, since a mapping need
    /// not be injective. Total.
    /// </summary>
    let map mapping (input: DistinctList<'value>) =
        DistinctList(input.ToList() |> List.map mapping |> List.distinct)

/// Choice combinators for ordinary fallible conversion functions.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Choice =
    let orElse leftMap left rightMap right fallbackError input =
        match left input with Ok value -> Ok(leftMap value) | Error _ -> match right input with Ok value -> Ok(rightMap value) | Error _ -> Error fallbackError
    let tryAny fallbackError strategies input =
        if isNull (box strategies) then Error fallbackError
        else
            match strategies |> Seq.tryPick (fun strategy -> match strategy input with Ok value -> Some value | Error _ -> None) with
            | Some value -> Ok value
            | None -> Error fallbackError

/// Convenience smart constructors for built-in refined values.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Refine =
    let nonBlankString = Text.nonBlankString
    let finiteFloat = FiniteFloat.create
    let finiteFloat32 = FiniteFloat32.create
    let unitInterval = UnitInterval.create
    let interval lower upper = Interval.create lower upper
    let bounded bounds value = Bounded.create bounds value
    let nonEmptyList values = Collection.nonEmptyList values
    let nonEmptyArray values = Collection.nonEmptyArray values
    let distinctList values = Collection.distinctList values
