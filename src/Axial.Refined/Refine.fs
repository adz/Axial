namespace Axial.Refined

#nowarn "0064" // SRTP dispatch intentionally constrains its marker type while preserving source and destination types.

open System
open System.Collections.Generic
open System.Globalization
open Axial.Check

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

/// <summary>A string that has no leading or trailing whitespace.</summary>
type TrimmedString =
    private
    | TrimmedString of string

    /// <summary>Returns the underlying string value.</summary>
    member this.Value =
        let (TrimmedString value) = this
        value

    override this.ToString() =
        this.Value

/// <summary>A string whose length is within a caller-supplied inclusive range.</summary>
type BoundedString =
    private
    | BoundedString of value: string * minLength: int * maxLength: int

    /// <summary>Returns the underlying string value.</summary>
    member this.Value =
        let (BoundedString(value, _, _)) = this
        value

    /// <summary>Returns the minimum accepted length used when this value was refined.</summary>
    member this.MinLength =
        let (BoundedString(_, minLength, _)) = this
        minLength

    /// <summary>Returns the maximum accepted length used when this value was refined.</summary>
    member this.MaxLength =
        let (BoundedString(_, _, maxLength)) = this
        maxLength

    override this.ToString() =
        this.Value

/// <summary>An ASCII slug containing lowercase letters, digits, and hyphens.</summary>
type Slug =
    private
    | Slug of string

    /// <summary>Returns the underlying slug text.</summary>
    member this.Value =
        let (Slug value) = this
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

/// <summary>An integer greater than or equal to zero.</summary>
type NonNegativeInt =
    private
    | NonNegativeInt of int

    /// <summary>Returns the underlying integer value.</summary>
    member this.Value =
        let (NonNegativeInt value) = this
        value

    override this.ToString() =
        string this.Value

/// <summary>An integer that is not zero.</summary>
type NonZeroInt =
    private
    | NonZeroInt of int

    /// <summary>Returns the underlying integer value.</summary>
    member this.Value =
        let (NonZeroInt value) = this
        value

    override this.ToString() =
        string this.Value

/// <summary>An integer less than zero.</summary>
type NegativeInt =
    private
    | NegativeInt of int

    /// <summary>Returns the underlying integer value.</summary>
    member this.Value =
        let (NegativeInt value) = this
        value

    override this.ToString() =
        string this.Value

/// <summary>An integer less than or equal to zero.</summary>
type NonPositiveInt =
    private
    | NonPositiveInt of int

    /// <summary>Returns the underlying integer value.</summary>
    member this.Value =
        let (NonPositiveInt value) = this
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

/// <summary>An array that contains at least one item.</summary>
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

    /// <summary>Returns a copy of the refined value as a standard array.</summary>
    member this.ToArray() =
        let (NonEmptyArray values) = this
        Array.copy values

    interface seq<'value> with
        member this.GetEnumerator() =
            (this.ToArray() :> seq<'value>).GetEnumerator()

        member this.GetEnumerator() =
            (this.ToArray() :> System.Collections.IEnumerable).GetEnumerator()

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

/// <summary>A list whose count is within a caller-supplied inclusive range.</summary>
type BoundedList<'value> =
    private
    | BoundedList of values: 'value list * minCount: int * maxCount: int

    /// <summary>Returns the minimum accepted count used when this value was refined.</summary>
    member this.MinCount =
        let (BoundedList(_, minCount, _)) = this
        minCount

    /// <summary>Returns the maximum accepted count used when this value was refined.</summary>
    member this.MaxCount =
        let (BoundedList(_, _, maxCount)) = this
        maxCount

    /// <summary>Returns the refined value as a standard list.</summary>
    member this.ToList() =
        let (BoundedList(values, _, _)) = this
        values

    interface seq<'value> with
        member this.GetEnumerator() =
            (this.ToList() :> seq<'value>).GetEnumerator()

        member this.GetEnumerator() =
            (this.ToList() :> System.Collections.IEnumerable).GetEnumerator()

/// <summary>An array whose count is within a caller-supplied inclusive range.</summary>
type BoundedArray<'value> =
    private
    | BoundedArray of values: 'value array * minCount: int * maxCount: int

    /// <summary>Returns the minimum accepted count used when this value was refined.</summary>
    member this.MinCount =
        let (BoundedArray(_, minCount, _)) = this
        minCount

    /// <summary>Returns the maximum accepted count used when this value was refined.</summary>
    member this.MaxCount =
        let (BoundedArray(_, _, maxCount)) = this
        maxCount

    /// <summary>Returns a copy of the refined value as a standard array.</summary>
    member this.ToArray() =
        let (BoundedArray(values, _, _)) = this
        Array.copy values

    interface seq<'value> with
        member this.GetEnumerator() =
            (this.ToArray() :> seq<'value>).GetEnumerator()

        member this.GetEnumerator() =
            (this.ToArray() :> System.Collections.IEnumerable).GetEnumerator()

/// <summary>A date and time range where <c>Start &lt;= End</c>.</summary>
type DateTimeOffsetRange =
    private {
        StartValue: DateTimeOffset
        EndValue: DateTimeOffset
    }

    /// <summary>Returns the inclusive start of the range.</summary>
    member this.Start =
        this.StartValue

    /// <summary>Returns the inclusive end of the range.</summary>
    member this.End =
        this.EndValue

#if NET8_0_OR_GREATER
/// <summary>A date-only range where <c>Start &lt;= End</c>.</summary>
/// <remarks>netstandard2.1: not available.</remarks>
type DateOnlyRange =
    private {
        StartValue: DateOnly
        EndValue: DateOnly
    }

    /// <summary>Returns the inclusive start of the range.</summary>
    member this.Start =
        this.StartValue

    /// <summary>Returns the inclusive end of the range.</summary>
    member this.End =
        this.EndValue
#endif

/// Defines admission into an invariant-carrying value and its total reverse projection.
[<Sealed>]
type Refinement<'underlying, 'refined> internal
    (check: Check<'underlying>, constraints: Constraint<'underlying> list, construct: 'underlying -> 'refined, project: 'refined -> 'underlying) =
    member internal _.Check = check
    member internal _.Constraints = constraints
    member internal _.Construct = construct
    member internal _.Project = project

/// Creates and applies reusable refinement definitions.
[<RequireQualifiedAccess>]
module Refinement =
    let private ensureFunction name value = if isNull (box value) then nullArg name

    /// Defines a refinement from one portable constraint.
    let define (constraint': Constraint<'underlying>) (construct: 'underlying -> 'refined) (project: 'refined -> 'underlying) =
        if isNull constraint' then nullArg (nameof constraint')
        ensureFunction (nameof construct) construct
        ensureFunction (nameof project) project
        Refinement(Constraint.check constraint', [ constraint' ], construct, project)

    /// Defines a refinement from one or more portable constraints.
    let defineAll (constraints: Constraint<'underlying> list) (construct: 'underlying -> 'refined) (project: 'refined -> 'underlying) =
        if isNull (box constraints) then nullArg (nameof constraints)
        if List.isEmpty constraints then invalidArg (nameof constraints) "A refinement must contain at least one constraint."
        ensureFunction (nameof construct) construct
        ensureFunction (nameof project) project
        Refinement(Constraint.checkAll constraints, constraints, construct, project)

    /// Defines a metadata-free refinement from an executable check.
    let defineWithCheck (check: Check<'underlying>) (construct: 'underlying -> 'refined) (project: 'refined -> 'underlying) =
        ensureFunction (nameof check) check
        ensureFunction (nameof construct) construct
        ensureFunction (nameof project) project
        Refinement(check, [], construct, project)

    /// Constructs a refined value after its check succeeds.
    let create (refinement: Refinement<'underlying, 'refined>) (underlying: 'underlying) : Result<'refined, CheckFailure list> =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Check underlying |> Result.map (fun () -> refinement.Construct underlying)

    /// Returns the canonical underlying representation.
    let underlying (refinement: Refinement<'underlying, 'refined>) (value: 'refined) =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Project value

    /// Returns portable constraints retained by the refinement.
    let constraints (refinement: Refinement<'underlying, 'refined>) =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Constraints

module private Bounds =
    let valid minimum maximum = minimum >= 0 && maximum >= minimum
    let failure = [ CheckFailure.Custom "invalidBounds" ]

/// Numeric refined value constructors and helpers.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Numeric =
    let positiveIntRefinement = Refinement.define (Constraint.greaterThan 0) PositiveInt _.Value
    let nonNegativeIntRefinement = Refinement.define (Constraint.atLeast 0) NonNegativeInt _.Value
    let nonZeroIntRefinement = Refinement.define (Constraint.notEqualTo 0) NonZeroInt _.Value
    let negativeIntRefinement = Refinement.define (Constraint.lessThan 0) NegativeInt _.Value
    let nonPositiveIntRefinement = Refinement.define (Constraint.atMost 0) NonPositiveInt _.Value
    let positiveInt value = Refinement.create positiveIntRefinement value
    let nonNegativeInt value = Refinement.create nonNegativeIntRefinement value
    let nonZeroInt value = Refinement.create nonZeroIntRefinement value
    let negativeInt value = Refinement.create negativeIntRefinement value
    let nonPositiveInt value = Refinement.create nonPositiveIntRefinement value

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PositiveInt =
    let refinement = Numeric.positiveIntRefinement
    let value (input: PositiveInt) = input.Value
    let create value = Refinement.create refinement value
    let map mapping (input: PositiveInt) = input.Value |> mapping |> create
    let replace value _input = create value

/// Text refined value constructors and helpers.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Text =
    let private slugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$"
    let nonBlankStringRefinement = Refinement.define Constraint.present NonBlankString _.Value
    let trimmedStringRefinement = Refinement.define Constraint.trimmed TrimmedString _.Value
    let slugRefinement = Refinement.defineAll [ Constraint.present; Constraint.pattern slugPattern ] Slug _.Value
    let nonBlankString value = Refinement.create nonBlankStringRefinement value
    let trimmedString value = Refinement.create trimmedStringRefinement value
    let slug value = Refinement.create slugRefinement value
    let boundedString minLength maxLength value =
        if not (Bounds.valid minLength maxLength) then Error Bounds.failure
        else
            Refinement.defineAll [ Constraint.present; Constraint.lengthBetween minLength maxLength ]
                (fun value -> BoundedString(value, minLength, maxLength)) _.Value
            |> fun refinement -> Refinement.create refinement value

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NonBlankString =
    let refinement = Text.nonBlankStringRefinement
    let value (input: NonBlankString) = input.Value
    let create value = Refinement.create refinement value
    let map mapping (input: NonBlankString) = input.Value |> mapping |> create

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
    let nonEmptyListRefinement<'value> () : Refinement<'value list, NonEmptyList<'value>> =
        let constraint': Constraint<'value list> = Constraint.minLength 1
        Refinement.define
            constraint'
            (function head :: tail -> NonEmptyList(head, tail) | [] -> failwith "unreachable")
            _.ToList()

    let nonEmptyArrayRefinement<'value> () : Refinement<'value array, NonEmptyArray<'value>> =
        let constraint': Constraint<'value array> = Constraint.minLength 1
        Refinement.define
            constraint'
            NonEmptyArray
            _.ToArray()

    let distinctListRefinement<'value when 'value: equality> () =
        let constraint': Constraint<'value list> = Constraint.distinct<'value> |> Constraint.forList
        Refinement.define constraint' DistinctList _.ToList()
    let nonEmptyList values = values |> Seq.toList |> Refinement.create (nonEmptyListRefinement ())
    let nonEmptyArray values = values |> Seq.toArray |> Refinement.create (nonEmptyArrayRefinement ())
    let distinctList values = values |> Seq.toList |> Refinement.create (distinctListRefinement ())
    let boundedList minCount maxCount values =
        if not (Bounds.valid minCount maxCount) then Error Bounds.failure
        else
            let refinement = Refinement.define (Constraint.lengthBetween minCount maxCount)
                                (fun values -> BoundedList(values, minCount, maxCount)) _.ToList()
            values |> Seq.toList |> Refinement.create refinement
    let boundedArray minCount maxCount values =
        if not (Bounds.valid minCount maxCount) then Error Bounds.failure
        else
            let refinement = Refinement.define (Constraint.lengthBetween minCount maxCount)
                                (fun values -> BoundedArray(values, minCount, maxCount)) _.ToArray()
            values |> Seq.toArray |> Refinement.create refinement
    let exactlyOne values =
        let values = Seq.toList values
        Check.Seq.count 1 values |> Result.map (fun () -> List.head values)
    let atMostOne values =
        let values = Seq.toList values
        Check.Seq.maxCount 1 values |> Result.map (fun () -> List.tryHead values)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NonEmptyList =
    let refinement<'value> () = Collection.nonEmptyListRefinement<'value> ()
    let toList (input: NonEmptyList<'value>) = input.ToList()
    let create values = Collection.nonEmptyList values
    let cons head tail = NonEmptyList(head, tail)
    let map mapping (input: NonEmptyList<'value>) = input.ToList() |> List.map mapping |> function head :: tail -> NonEmptyList(head, tail) | [] -> failwith "unreachable"
    let filter predicate (input: NonEmptyList<'value>) = input.ToList() |> List.filter predicate
    let tryFilter predicate input = input |> filter predicate |> create

/// Temporal refined value constructors and helpers.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Temporal =
    let dateTimeOffsetRange start finish : Result<DateTimeOffsetRange, CheckFailure list> =
        if start <= finish then Ok { StartValue = start; EndValue = finish }
        else Error [ CheckFailure.Custom "dateTimeOffsetRange" ]
#if NET8_0_OR_GREATER
    let dateOnlyRange start finish : Result<DateOnlyRange, CheckFailure list> =
        if start <= finish then Ok { StartValue = start; EndValue = finish }
        else Error [ CheckFailure.Custom "dateOnlyRange" ]
#endif

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
    let trimmedString = Text.trimmedString
    let boundedString = Text.boundedString
    let slug = Text.slug
    let positiveInt = Numeric.positiveInt
    let nonNegativeInt = Numeric.nonNegativeInt
    let nonZeroInt = Numeric.nonZeroInt
    let negativeInt = Numeric.negativeInt
    let nonPositiveInt = Numeric.nonPositiveInt
    let nonEmptyList values = Collection.nonEmptyList values
    let nonEmptyArray values = Collection.nonEmptyArray values
    let distinctList values = Collection.distinctList values
    let boundedList = Collection.boundedList
    let boundedArray = Collection.boundedArray
    let exactlyOne = Collection.exactlyOne
    let atMostOne = Collection.atMostOne
    let dateTimeOffsetRange = Temporal.dateTimeOffsetRange
#if NET8_0_OR_GREATER
    let dateOnlyRange = Temporal.dateOnlyRange
#endif
