namespace Axial.Refined

open System

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

/// <summary>An integer greater than zero.</summary>
type PositiveInt =
    private
    | PositiveInt of int

    /// <summary>Returns the underlying integer value.</summary>
    member this.Value =
        let (PositiveInt value) = this
        value

    override this.ToString() =
        string this.Value

/// <summary>A list that contains at least one item.</summary>
type NonEmptyList<'value> =
    private
    | NonEmptyList of head: 'value * tail: 'value list

    /// <summary>Returns the first item.</summary>
    member this.Head =
        let (NonEmptyList(head, _)) = this
        head

    /// <summary>Returns the remaining items.</summary>
    member this.Tail =
        let (NonEmptyList(_, tail)) = this
        tail

    /// <summary>Returns the refined value as a standard list.</summary>
    member this.ToList() =
        this.Head :: this.Tail

    interface seq<'value> with
        member this.GetEnumerator() =
            (this.ToList() :> seq<'value>).GetEnumerator()

        member this.GetEnumerator() =
            (this.ToList() :> System.Collections.IEnumerable).GetEnumerator()

/// <summary>Smart constructors for built-in structural refined values.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Refine =
    /// <summary>Builds a non-blank string.</summary>
    let nonBlankString (value: string) : Result<NonBlankString, RefinementError> =
        if String.IsNullOrWhiteSpace value then
            Error(RefinementError.MissingValue "NonBlankString")
        else
            Ok(NonBlankString value)

    /// <summary>Builds a positive integer.</summary>
    let positiveInt (value: int) : Result<PositiveInt, RefinementError> =
        if value > 0 then
            Ok(PositiveInt value)
        else
            Error(RefinementError.OutOfRange("PositiveInt", "Expected a value greater than zero."))

    /// <summary>Builds a non-empty list from a sequence.</summary>
    let nonEmptyList (values: seq<'value>) : Result<NonEmptyList<'value>, RefinementError> =
        if isNull (box values) then
            Error(RefinementError.MissingValue "NonEmptyList")
        else
            match values |> Seq.toList with
            | [] -> Error(RefinementError.InvalidStructure("NonEmptyList", "Expected at least one item."))
            | head :: tail -> Ok(NonEmptyList(head, tail))
