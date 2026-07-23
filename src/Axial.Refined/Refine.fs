namespace Axial.Refined

#nowarn "0064" // SRTP dispatch intentionally constrains its marker type while preserving source and destination types.

open System
open System.Collections.Generic
open System.Globalization
open Axial.ErrorHandling

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

/// <summary>Runs an executable <see cref="T:Axial.ErrorHandling.Check`1" /> program before calling a refined value
/// constructor, reusing <see cref="T:Axial.ErrorHandling.CheckFailure" /> as the sole failure vocabulary so refined
/// constructors never reimplement the small checks <c>Axial.ErrorHandling.Check</c> already provides.</summary>
module private Checked =
    let withCheck (target: string) (check: Check<'raw>) (construct: 'raw -> 'refined) (value: 'raw) : Result<'refined, RefinementError> =
        match check value with
        | Ok value -> Ok(construct value)
        | Error failures -> Error(RefinementError.CheckFailed(target, failures))

    let withChecks (target: string) (checks: Check<'raw> list) (construct: 'raw -> 'refined) (value: 'raw) : Result<'refined, RefinementError> =
        withCheck target (Check.all checks) construct value

module private Bounds =
    /// <summary>Validates the caller-supplied bounds themselves, before any value is checked against them.</summary>
    let validateRange target minLength maxLength =
        if minLength < 0 then
            Error(RefinementError.InvalidStructure(target, "Expected minimum length to be greater than or equal to zero."))
        elif maxLength < minLength then
            Error(RefinementError.InvalidStructure(target, "Expected minimum length to be less than or equal to maximum length."))
        else
            Ok()

/// <summary>Numeric refined value constructors and helpers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Numeric =
    /// <summary>Builds a positive integer.</summary>
    let positiveInt (value: int) : Result<PositiveInt, RefinementError> =
        Checked.withCheck "PositiveInt" Check.Number.positive PositiveInt value

    /// <summary>Builds a non-negative integer.</summary>
    let nonNegativeInt (value: int) : Result<NonNegativeInt, RefinementError> =
        Checked.withCheck "NonNegativeInt" Check.Number.nonNegative NonNegativeInt value

    /// <summary>Builds a non-zero integer.</summary>
    let nonZeroInt (value: int) : Result<NonZeroInt, RefinementError> =
        Checked.withCheck "NonZeroInt" (Check.notEqualTo 0) NonZeroInt value

    /// <summary>Builds a negative integer.</summary>
    let negativeInt (value: int) : Result<NegativeInt, RefinementError> =
        Checked.withCheck "NegativeInt" Check.Number.negative NegativeInt value

    /// <summary>Builds a non-positive integer.</summary>
    let nonPositiveInt (value: int) : Result<NonPositiveInt, RefinementError> =
        Checked.withCheck "NonPositiveInt" Check.Number.nonPositive NonPositiveInt value

/// <summary>Operations over <see cref="PositiveInt" />.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PositiveInt =
    /// <summary>Returns the underlying integer value.</summary>
    let value (input: PositiveInt) =
        input.Value

    /// <summary>Builds a positive integer.</summary>
    let create value =
        Numeric.positiveInt value

    /// <summary>Transforms the value and re-certifies the positive integer invariant.</summary>
    let map (mapping: int -> int) (input: PositiveInt) =
        input.Value
        |> mapping
        |> create

    /// <summary>Replaces the value and re-certifies the positive integer invariant.</summary>
    let replace value _input =
        create value

/// <summary>Text refined value constructors and helpers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Text =
    let private notTrimmed : Check<string> =
        fun value ->
            if isNull value then Error [ Required ]
            elif value.Trim() <> value then Error [ InvalidFormat "trimmed" ]
            else Ok value

    let private slugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$"

    /// <summary>Builds a non-blank string.</summary>
    let nonBlankString (value: string) : Result<NonBlankString, RefinementError> =
        Checked.withCheck "NonBlankString" Check.String.present NonBlankString value

    /// <summary>Builds a string that has no leading or trailing whitespace.</summary>
    let trimmedString (value: string) : Result<TrimmedString, RefinementError> =
        Checked.withCheck "TrimmedString" notTrimmed TrimmedString value

    /// <summary>Builds a string whose length is within an inclusive range.</summary>
    let boundedString minLength maxLength (value: string) : Result<BoundedString, RefinementError> =
        Bounds.validateRange "BoundedString" minLength maxLength
        |> Result.bind (fun () ->
            Checked.withChecks
                "BoundedString"
                [ Check.String.present; Check.String.lengthBetween minLength maxLength ]
                (fun value -> BoundedString(value, minLength, maxLength))
                value)

    /// <summary>Builds an ASCII slug made of lowercase letters, digits, and hyphens.</summary>
    let slug (value: string) : Result<Slug, RefinementError> =
        Checked.withChecks "Slug" [ Check.String.present; Check.String.matches slugPattern ] Slug value

/// <summary>Operations over <see cref="NonBlankString" />.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NonBlankString =
    /// <summary>Returns the underlying string value.</summary>
    let value (input: NonBlankString) =
        input.Value

    /// <summary>Builds a non-blank string.</summary>
    let create value =
        Text.nonBlankString value

    /// <summary>Transforms the value and re-certifies the non-blank invariant.</summary>
    let map (mapping: string -> string) (input: NonBlankString) =
        input.Value
        |> mapping
        |> create

/// <summary>Character predicate helpers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Character =
    /// <summary>Returns true when the character is an ASCII digit.</summary>
    let isAsciiDigit value =
        value >= '0' && value <= '9'

    /// <summary>Returns true when the character is an ASCII hexadecimal digit.</summary>
    let isAsciiHexDigit value =
        (value >= '0' && value <= '9')
        || (value >= 'a' && value <= 'f')
        || (value >= 'A' && value <= 'F')

    /// <summary>Returns true when the character is lowercase according to invariant Unicode casing.</summary>
    let isLowercase (value: char) =
        Char.IsLower value

    /// <summary>Returns true when the character is uppercase according to invariant Unicode casing.</summary>
    let isUppercase (value: char) =
        Char.IsUpper value

    /// <summary>Returns true when the character is whitespace.</summary>
    let isWhitespace (value: char) =
        Char.IsWhiteSpace value

    /// <summary>Returns true when the character is a control character.</summary>
    let isControl (value: char) =
        Char.IsControl value

    /// <summary>Returns true when the character is numeric according to Unicode character data.</summary>
    let isNumeric (value: char) =
        Char.IsNumber value

/// <summary>Collection refined value constructors and helpers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Collection =
    /// <summary>Builds a non-empty list from a sequence.</summary>
    let nonEmptyList (values: seq<'value>) : Result<NonEmptyList<'value>, RefinementError> =
        Checked.withCheck
            "NonEmptyList"
            Check.Seq.notEmpty
            (fun values ->
                match values |> Seq.toList with
                | head :: tail -> NonEmptyList(head, tail)
                | [] -> failwith "NonEmptyList: unreachable, Check.Seq.notEmpty already guaranteed at least one item")
            values

    /// <summary>Builds a non-empty array from a sequence.</summary>
    let nonEmptyArray (values: seq<'value>) : Result<NonEmptyArray<'value>, RefinementError> =
        Checked.withCheck "NonEmptyArray" Check.Seq.notEmpty (Seq.toArray >> NonEmptyArray) values

    /// <summary>Builds a list that contains no duplicate items.</summary>
    let distinctList (values: seq<'value>) : Result<DistinctList<'value>, RefinementError> =
        Checked.withCheck "DistinctList" Check.Seq.noDuplicates (Seq.toList >> DistinctList) values

    /// <summary>Builds a list whose count is within an inclusive range.</summary>
    let boundedList minCount maxCount (values: seq<'value>) : Result<BoundedList<'value>, RefinementError> =
        Bounds.validateRange "BoundedList" minCount maxCount
        |> Result.bind (fun () ->
            Checked.withCheck
                "BoundedList"
                (Check.Seq.countBetween minCount maxCount)
                (fun values -> BoundedList(Seq.toList values, minCount, maxCount))
                values)

    /// <summary>Builds an array whose count is within an inclusive range.</summary>
    let boundedArray minCount maxCount (values: seq<'value>) : Result<BoundedArray<'value>, RefinementError> =
        Bounds.validateRange "BoundedArray" minCount maxCount
        |> Result.bind (fun () ->
            Checked.withCheck
                "BoundedArray"
                (Check.Seq.countBetween minCount maxCount)
                (fun values -> BoundedArray(Seq.toArray values, minCount, maxCount))
                values)

    /// <summary>Extracts the only item from a sequence.</summary>
    /// <remarks>
    /// Cardinality is a collection-level structural fact, not a value-level constraint on a single element, so this
    /// lives here rather than as a <c>Check</c>: <c>Check.Seq.count 1</c> proves the fact and keeps the sequence,
    /// while this extracts the element itself, the same distinction <see cref="M:Axial.Refined.Refine.withCheck" />
    /// draws between proving and constructing.
    /// </remarks>
    let exactlyOne (values: seq<'value>) : Result<'value, RefinementError> =
        Checked.withCheck "ExactlyOne" (Check.Seq.count 1) Seq.head values

    /// <summary>Extracts zero or one item from a sequence.</summary>
    let atMostOne (values: seq<'value>) : Result<'value option, RefinementError> =
        Checked.withCheck "AtMostOne" (Check.Seq.maxCount 1) Seq.tryHead values

/// <summary>Operations over <see cref="NonEmptyList{value}" />.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NonEmptyList =
    /// <summary>Returns the refined value as a standard list.</summary>
    let toList (input: NonEmptyList<'value>) =
        input.ToList()

    /// <summary>Builds a non-empty list from a sequence.</summary>
    let create values =
        Collection.nonEmptyList values

    /// <summary>Prepends a head item to a list, producing a non-empty list without failure.</summary>
    let cons head tail =
        NonEmptyList(head, tail)

    /// <summary>Transforms each item while preserving non-emptiness.</summary>
    let map (mapping: 'value -> 'next) (input: NonEmptyList<'value>) =
        input.ToList()
        |> List.map mapping
        |> function
            | [] -> failwith "NonEmptyList.map: impossible empty result"
            | head :: tail -> NonEmptyList(head, tail)

    /// <summary>Filters the list, returning a standard list because filtering can remove every item.</summary>
    let filter predicate (input: NonEmptyList<'value>) =
        input.ToList()
        |> List.filter predicate

    /// <summary>Filters the list and re-certifies that at least one item remains.</summary>
    let tryFilter predicate input =
        input
        |> filter predicate
        |> create

/// <summary>Temporal refined value constructors and helpers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Temporal =
    /// <summary>Builds a date and time range where <c>Start &lt;= End</c>.</summary>
    let dateTimeOffsetRange start finish : Result<DateTimeOffsetRange, RefinementError> =
        if start <= finish then
            Ok { StartValue = start; EndValue = finish }
        else
            Error(RefinementError.InvalidStructure("DateTimeOffsetRange", "Expected Start to be less than or equal to End."))

#if NET8_0_OR_GREATER
    /// <summary>Builds a date-only range where <c>Start &lt;= End</c>.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    let dateOnlyRange start finish : Result<DateOnlyRange, RefinementError> =
        if start <= finish then
            Ok { StartValue = start; EndValue = finish }
        else
            Error(RefinementError.InvalidStructure("DateOnlyRange", "Expected Start to be less than or equal to End."))
#endif

/// <summary>Parser-choice combinators for constructing your own domain unions.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Choice =
    /// <summary>Tries the left parser first, then the right parser, mapping either success into your output type.</summary>
    let orElse
        (leftMap: 'left -> 'output)
        (left: 'raw -> Result<'left, 'error>)
        (rightMap: 'right -> 'output)
        (right: 'raw -> Result<'right, 'error>)
        (fallbackError: 'error)
        (input: 'raw)
        : Result<'output, 'error> =
        match left input with
        | Ok value -> Ok(leftMap value)
        | Error _ ->
            match right input with
            | Ok value -> Ok(rightMap value)
            | Error _ -> Error fallbackError

    /// <summary>Tries parser strategies in order and returns the first success.</summary>
    let tryAny
        (fallbackError: 'error)
        (strategies: seq<'raw -> Result<'output, 'error>>)
        (input: 'raw)
        : Result<'output, 'error> =
        if isNull (box strategies) then
            Error fallbackError
        else
            strategies
            |> Seq.tryPick (fun refine ->
                match refine input with
                | Ok value -> Some value
                | Error _ -> None)
            |> function
                | Some value -> Ok value
                | None -> Error fallbackError

/// <summary>Defines fallible construction of a refined value and total inspection of its raw representation.</summary>
[<Sealed>]
type Refinement<'raw, 'value> internal (create: 'raw -> Result<'value, RefinementError>, inspect: 'value -> 'raw) =
    member internal _.Create = create
    member internal _.Inspect = inspect

/// <summary>Creates and applies reusable bidirectional refinement definitions.</summary>
[<RequireQualifiedAccess>]
module Refinement =
    /// <summary>Defines a refinement from its smart constructor and raw-value projection.</summary>
    /// <example>
    /// <code>
    /// let email =
    ///     Refinement.define Email.create Email.value
    /// </code>
    /// </example>
    let define
        (create: 'raw -> Result<'value, RefinementError>)
        (inspect: 'value -> 'raw)
        : Refinement<'raw, 'value> =
        if isNull (box create) then nullArg (nameof create)
        if isNull (box inspect) then nullArg (nameof inspect)
        Refinement(create, inspect)

    /// <summary>Runs the refinement's smart constructor.</summary>
    /// <example>
    /// <code>
    /// let result = Refinement.create Email.refinement raw
    /// </code>
    /// </example>
    let create (refinement: Refinement<'raw, 'value>) (raw: 'raw) : Result<'value, RefinementError> =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Create raw

    /// <summary>Returns the raw representation stored by a refined value.</summary>
    /// <example>
    /// <code>
    /// let raw = Refinement.inspect Email.refinement email
    /// </code>
    /// </example>
    let inspect (refinement: Refinement<'raw, 'value>) (value: 'value) : 'raw =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Inspect value

/// <summary>
/// Compile-time dispatch for type-directed refinements. A destination type participates by defining one static
/// <c>Refinement</c> member for each supported raw type.
/// </summary>
/// <example>
/// <code>
/// type CustomerId = private CustomerId of int
///
/// type CustomerId with
///     static member Refinement(_: string, _: CustomerId) =
///         CustomerId.refinement
/// </code>
/// </example>
type RefinementFrom =
    static member inline Resolve() : Refinement<^raw, ^value> =
        let inline call (target: ^value, input: ^raw, dispatch: ^dispatch) =
            ((^value or ^dispatch):
                (static member Refinement:
                    ^raw * ^value -> Refinement<^raw, ^value>)
                    (input, target))

        call (
            Unchecked.defaultof<^value>,
            Unchecked.defaultof<^raw>,
            Unchecked.defaultof<RefinementFrom>
        )

    static member inline Bind
        (
            raw: ^raw,
            binder: ^value -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        let refinement: Refinement<^raw, ^value> = RefinementFrom.Resolve()
        Refinement.create refinement raw
        |> Result.bind binder

/// <summary>Smart constructors for built-in structural refined values.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Refine =
    /// <summary>
    /// Resolves the <c>Refinement</c> definition for the raw value and expected destination type, then runs its smart
    /// constructor. A destination type participates by defining a static <c>Refinement</c> member.
    /// </summary>
    /// <example>
    /// <code>
    /// let id : Result&lt;int, RefinementError&gt; = Refine.from "42"
    /// </code>
    /// </example>
    let inline from (raw: ^raw) : Result<^value, RefinementError> =
        let refinement: Refinement<^raw, ^value> = RefinementFrom.Resolve()
        Refinement.create refinement raw

    /// <summary>Builds a refined value by running a reusable <see cref="T:Axial.ErrorHandling.Check`1" /> program
    /// before calling the constructor. Failures carry the check's own <see cref="T:Axial.ErrorHandling.CheckFailure" />
    /// values, so callers never need to reinterpret or re-describe them.</summary>
    let withCheck (target: string) (check: Check<'raw>) (construct: 'raw -> 'refined) (value: 'raw) : Result<'refined, RefinementError> =
        Checked.withCheck target check construct value

    /// <summary>Builds a refined value by running every supplied <see cref="T:Axial.ErrorHandling.Check`1" /> program
    /// before calling the constructor, accumulating all failures via <c>Check.all</c>.</summary>
    let withChecks (target: string) (checks: Check<'raw> list) (construct: 'raw -> 'refined) (value: 'raw) : Result<'refined, RefinementError> =
        Checked.withChecks target checks construct value

    /// <summary>Builds a non-blank string.</summary>
    let nonBlankString value =
        Text.nonBlankString value

    /// <summary>Builds a string that has no leading or trailing whitespace.</summary>
    let trimmedString value =
        Text.trimmedString value

    /// <summary>Builds a string whose length is within an inclusive range.</summary>
    let boundedString minLength maxLength value =
        Text.boundedString minLength maxLength value

    /// <summary>Builds an ASCII slug.</summary>
    let slug value =
        Text.slug value

    /// <summary>Builds a positive integer.</summary>
    let positiveInt value =
        Numeric.positiveInt value

    /// <summary>Builds a non-negative integer.</summary>
    let nonNegativeInt value =
        Numeric.nonNegativeInt value

    /// <summary>Builds a non-zero integer.</summary>
    let nonZeroInt value =
        Numeric.nonZeroInt value

    /// <summary>Builds a negative integer.</summary>
    let negativeInt value =
        Numeric.negativeInt value

    /// <summary>Builds a non-positive integer.</summary>
    let nonPositiveInt value =
        Numeric.nonPositiveInt value

    /// <summary>Builds a non-empty list from a sequence.</summary>
    let nonEmptyList values =
        Collection.nonEmptyList values

    /// <summary>Builds a non-empty array from a sequence.</summary>
    let nonEmptyArray values =
        Collection.nonEmptyArray values

    /// <summary>Builds a distinct list from a sequence.</summary>
    let distinctList values =
        Collection.distinctList values

    /// <summary>Builds a bounded list from a sequence.</summary>
    let boundedList minCount maxCount values =
        Collection.boundedList minCount maxCount values

    /// <summary>Builds a bounded array from a sequence.</summary>
    let boundedArray minCount maxCount values =
        Collection.boundedArray minCount maxCount values

    /// <summary>Builds a date and time range where <c>Start &lt;= End</c>.</summary>
    let dateTimeOffsetRange start finish =
        Temporal.dateTimeOffsetRange start finish

    /// <summary>Extracts the only item from a sequence.</summary>
    let exactlyOne values =
        Collection.exactlyOne values

    /// <summary>Extracts zero or one item from a sequence.</summary>
    let atMostOne values =
        Collection.atMostOne values

#if NET8_0_OR_GREATER
    /// <summary>Builds a date-only range where <c>Start &lt;= End</c>.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    let dateOnlyRange start finish =
        Temporal.dateOnlyRange start finish
#endif

/// <summary>Computation expression builder for fail-fast structural refinement.</summary>
/// <exclude/>
type RefineBuilder() =
    member _.Return(value: 'value) : Result<'value, RefinementError> =
        Ok value

    member _.ReturnFrom(result: Result<'value, RefinementError>) : Result<'value, RefinementError> =
        result

    member _.ReturnFrom(result: Result<'value, ParseError>) : Result<'value, RefinementError> =
        result |> Result.mapError RefinementError.ParseFailed

    member _.Zero() : Result<unit, RefinementError> =
        Ok ()

    member inline _.Bind
        (
            value: ^raw,
            binder: ^value -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        RefinementFrom.Bind(value, binder)

    member _.Delay(factory: unit -> Result<'value, RefinementError>) : Result<'value, RefinementError> =
        factory ()

    member _.Run(result: Result<'value, RefinementError>) : Result<'value, RefinementError> =
        result

    member _.Combine
        (
            first: Result<unit, RefinementError>,
            second: Result<'value, RefinementError>
        ) : Result<'value, RefinementError> =
        Result.bind (fun () -> second) first

module private BuiltInRefinementInternals =
    let parse parser value =
        parser value
        |> Result.mapError RefinementError.ParseFailed

    let parsing parser inspect =
        Refinement.define (parse parser) inspect

// Keep these instances after Refine.from and RefineBuilder. F# must preserve their inline SRTP constraints before it
// sees a concrete overload; otherwise it specializes the supposedly generic facade to the first instance.
type RefinementFrom with
    static member Refinement(_: Result<'value, RefinementError>, _: 'value) =
        Refinement.define id Ok

    static member Refinement(_: Result<'value, ParseError>, _: 'value) =
        Refinement.define
            (Result.mapError RefinementError.ParseFailed)
            Ok

    static member Refinement(_: string, _: int) =
        BuiltInRefinementInternals.parsing Parse.int (fun value -> value.ToString(CultureInfo.InvariantCulture))

    static member Refinement(_: string, _: int64) =
        BuiltInRefinementInternals.parsing Parse.long (fun value -> value.ToString(CultureInfo.InvariantCulture))

    static member Refinement(_: string, _: decimal) =
        BuiltInRefinementInternals.parsing Parse.decimal (fun value -> value.ToString(CultureInfo.InvariantCulture))

    static member Refinement(_: string, _: float) =
        BuiltInRefinementInternals.parsing Parse.float (fun value -> value.ToString("R", CultureInfo.InvariantCulture))

    static member Refinement(_: string, _: bool) =
        BuiltInRefinementInternals.parsing Parse.bool (fun value ->
            if value then "true" else "false")

    static member Refinement(_: string, _: Guid) =
        BuiltInRefinementInternals.parsing Parse.guid (fun value -> value.ToString("D"))

    static member Refinement(_: string, _: DateTime) =
        BuiltInRefinementInternals.parsing Parse.dateTime (fun value ->
            value.ToString("O", CultureInfo.InvariantCulture))

    static member Refinement(_: string, _: DateTimeOffset) =
        BuiltInRefinementInternals.parsing Parse.dateTimeOffset (fun value ->
            value.ToString("O", CultureInfo.InvariantCulture))

#if NET8_0_OR_GREATER
    static member Refinement(_: string, _: DateOnly) =
        BuiltInRefinementInternals.parsing Parse.dateOnly (fun value ->
            value.ToString("O", CultureInfo.InvariantCulture))

    static member Refinement(_: string, _: TimeOnly) =
        BuiltInRefinementInternals.parsing Parse.timeOnly (fun value ->
            value.ToString("O", CultureInfo.InvariantCulture))
#endif

    static member Refinement(_: string, _: NonBlankString) =
        Refinement.define Refine.nonBlankString _.Value

    static member Refinement(_: string, _: TrimmedString) =
        Refinement.define Refine.trimmedString _.Value

    static member Refinement(_: string, _: Slug) =
        Refinement.define Refine.slug _.Value

    static member Refinement(_: string * int * int, _: BoundedString) =
        Refinement.define
            (fun (value, minLength, maxLength) -> Refine.boundedString minLength maxLength value)
            (fun value -> value.Value, value.MinLength, value.MaxLength)

    static member Refinement(_: int, _: PositiveInt) =
        Refinement.define Refine.positiveInt _.Value

    static member Refinement(_: int, _: NonNegativeInt) =
        Refinement.define Refine.nonNegativeInt _.Value

    static member Refinement(_: int, _: NonZeroInt) =
        Refinement.define Refine.nonZeroInt _.Value

    static member Refinement(_: int, _: NegativeInt) =
        Refinement.define Refine.negativeInt _.Value

    static member Refinement(_: int, _: NonPositiveInt) =
        Refinement.define Refine.nonPositiveInt _.Value

    static member Refinement(_: seq<'value>, _: NonEmptyList<'value>) =
        Refinement.define Refine.nonEmptyList (fun value -> value.ToList() :> seq<'value>)

    static member Refinement(_: 'value list, _: NonEmptyList<'value>) =
        Refinement.define Refine.nonEmptyList _.ToList()

    static member Refinement(_: seq<'value>, _: NonEmptyArray<'value>) =
        Refinement.define Refine.nonEmptyArray (fun value -> value.ToArray() :> seq<'value>)

    static member Refinement(_: 'value list, _: NonEmptyArray<'value>) =
        Refinement.define Refine.nonEmptyArray (fun value -> value.ToArray() |> Array.toList)

    static member Refinement(_: 'value array, _: NonEmptyArray<'value>) =
        Refinement.define Refine.nonEmptyArray _.ToArray()

    static member Refinement(_: seq<'value>, _: DistinctList<'value>) =
        Refinement.define Refine.distinctList (fun value -> value.ToList() :> seq<'value>)

    static member Refinement(_: 'value list, _: DistinctList<'value>) =
        Refinement.define Refine.distinctList _.ToList()

    static member Refinement(_: 'value list * int * int, _: BoundedList<'value>) =
        Refinement.define
            (fun (values, minCount, maxCount) -> Refine.boundedList minCount maxCount values)
            (fun value -> value.ToList(), value.MinCount, value.MaxCount)

    static member Refinement(_: 'value array * int * int, _: BoundedArray<'value>) =
        Refinement.define
            (fun (values, minCount, maxCount) -> Refine.boundedArray minCount maxCount values)
            (fun value -> value.ToArray(), value.MinCount, value.MaxCount)

    static member Refinement(_: DateTimeOffset * DateTimeOffset, _: DateTimeOffsetRange) =
        Refinement.define
            (fun (start, finish) -> Refine.dateTimeOffsetRange start finish)
            (fun value -> value.Start, value.End)

#if NET8_0_OR_GREATER
    static member Refinement(_: DateOnly * DateOnly, _: DateOnlyRange) =
        Refinement.define
            (fun (start, finish) -> Refine.dateOnlyRange start finish)
            (fun value -> value.Start, value.End)
#endif
