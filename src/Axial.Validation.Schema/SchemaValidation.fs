namespace Axial.Validation.Schema

open System
open Axial.ErrorHandling
open Axial.Schema

/// <summary>
/// Marks the package that owns schema input, diagnostics, validation, and rules interpreters.
/// </summary>
/// <remarks>
/// <para>
/// The interpreter surface is intentionally introduced in focused slices after the core schema metadata model is proven.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module SchemaValidation =
    /// <summary>Identifies the schema validation integration package.</summary>
    let packageName = "Axial.Validation.Schema"

/// <summary>Functions for lowering portable schema constraint metadata to executable value checks.</summary>
/// <remarks>
/// <para>
/// Schema constraints stay inspectable in <c>Axial.Schema</c>. This integration module turns the subset that has
/// value-level meaning into path-free <see cref="T:Axial.ErrorHandling.Check`1" /> programs.
/// </para>
/// <para>
/// Constraints such as <c>optional</c> remain metadata-only. Constraints that belong to another value shape return
/// <c>None</c> from the per-constraint lowerers and are ignored by the list lowerers.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module SchemaConstraintCheck =
    let private ensureConstraint (constraint': SchemaConstraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

    let private ensureConstraints constraints =
        if isNull (box constraints) then
            nullArg (nameof constraints)

        let constraints = constraints |> Seq.toList

        constraints
        |> List.iter (fun constraint' ->
            if isNull constraint' then
                nullArg (nameof constraints))

        constraints

    let private tryArgument<'value> name constraint' =
        match SchemaConstraint.tryFindArgument name constraint' with
        | Some (:? 'value as value) -> Some value
        | _ -> None

    let private tryBounds<'value> constraint' =
        match tryArgument<'value> "minimum" constraint', tryArgument<'value> "maximum" constraint' with
        | Some minimum, Some maximum -> Some(minimum, maximum)
        | _ -> None

    /// <summary>Lowers one schema constraint to a string check when the constraint has text-level meaning.</summary>
    let tryText (constraint': SchemaConstraint) : Check<string> option =
        ensureConstraint constraint'

        match SchemaConstraint.code constraint' with
        | "required" -> Some Check.String.present
        | "minLength" -> tryArgument<int> "minimum" constraint' |> Option.map Check.String.minLength
        | "maxLength" -> tryArgument<int> "maximum" constraint' |> Option.map Check.String.maxLength
        | "lengthBetween" ->
            tryBounds<int> constraint'
            |> Option.map (fun (minimum, maximum) -> Check.String.lengthBetween minimum maximum)
        | "email" -> Some Check.String.email
        | "pattern" -> tryArgument<string> "pattern" constraint' |> Option.map Check.String.matches
        | "oneOf" ->
            tryArgument<string array> "choices" constraint'
            |> Option.map (fun choices -> Check.String.oneOf choices)
        | _ -> None

    /// <summary>Lowers schema constraints with text-level meaning into one string check.</summary>
    let text (constraints: SchemaConstraint seq) : Check<string> =
        ensureConstraints constraints
        |> Seq.choose tryText
        |> Seq.toList
        |> Check.all

    let private betweenCheck minimum maximum : Check<'value> =
        fun value ->
            if value >= minimum && value <= maximum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.Between(string minimum, string maximum), Some(string value)) ]

    let private greaterThanCheck minimum : Check<'value> =
        fun value ->
            if value > minimum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.GreaterThan(string minimum), Some(string value)) ]

    let private lessThanCheck maximum : Check<'value> =
        fun value ->
            if value < maximum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.LessThan(string maximum), Some(string value)) ]

    let private atLeastCheck minimum : Check<'value> =
        fun value ->
            if value >= minimum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.AtLeast(string minimum), Some(string value)) ]

    let private atMostCheck maximum : Check<'value> =
        fun value ->
            if value <= maximum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.AtMost(string maximum), Some(string value)) ]

    /// <summary>Lowers one schema constraint to an ordered-value check when the constraint has range-level meaning.</summary>
    let tryOrdered<'value when 'value: comparison> (constraint': SchemaConstraint) : Check<'value> option =
        ensureConstraint constraint'

        match SchemaConstraint.code constraint' with
        | "between" ->
            tryBounds<'value> constraint'
            |> Option.map (fun (minimum, maximum) -> betweenCheck minimum maximum)
        | "greaterThan" -> tryArgument<'value> "minimum" constraint' |> Option.map greaterThanCheck
        | "lessThan" -> tryArgument<'value> "maximum" constraint' |> Option.map lessThanCheck
        | "atLeast" -> tryArgument<'value> "minimum" constraint' |> Option.map atLeastCheck
        | "atMost" -> tryArgument<'value> "maximum" constraint' |> Option.map atMostCheck
        | _ -> None

    /// <summary>Lowers schema constraints with range-level meaning into one ordered-value check.</summary>
    let ordered<'value when 'value: comparison> (constraints: SchemaConstraint seq) : Check<'value> =
        ensureConstraints constraints
        |> Seq.choose tryOrdered<'value>
        |> Seq.toList
        |> Check.all

    /// <summary>Lowers one schema constraint to a sequence check when the constraint has sequence-level meaning.</summary>
    let trySequence<'value when 'value: equality> (constraint': SchemaConstraint) : Check<seq<'value>> option =
        ensureConstraint constraint'

        match SchemaConstraint.code constraint' with
        | "count" -> tryArgument<int> "expected" constraint' |> Option.map Check.Seq.count
        | "minCount" -> tryArgument<int> "minimum" constraint' |> Option.map Check.Seq.minCount
        | "maxCount" -> tryArgument<int> "maximum" constraint' |> Option.map Check.Seq.maxCount
        | "countBetween" ->
            tryBounds<int> constraint'
            |> Option.map (fun (minimum, maximum) -> Check.Seq.countBetween minimum maximum)
        | "distinct" -> Some Check.Seq.noDuplicates
        | _ -> None

    /// <summary>Lowers schema constraints with sequence-level meaning into one sequence check.</summary>
    let sequence<'value when 'value: equality> (constraints: SchemaConstraint seq) : Check<seq<'value>> =
        ensureConstraints constraints
        |> Seq.choose trySequence<'value>
        |> Seq.toList
        |> Check.all
