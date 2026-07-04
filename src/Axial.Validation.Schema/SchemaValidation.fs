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

/// <summary>Functions for running executable value checks against refined and primitive value schemas.</summary>
/// <remarks>
/// <para>
/// Refined value schemas describe named domain values, such as an <c>Email</c> refined over raw text, while their
/// executable constraints are expressed against the underlying primitive representation. This interpreter runs
/// <see cref="T:Axial.ErrorHandling.Check`1" /> programs against a schema's values by projecting each trusted value
/// through the schema's refinement layers with <see cref="M:Axial.Schema.Value.inspectUnderlying``2" /> and running
/// the primitive-level check on the result. Primitive value schemas work the same way with an identity projection.
/// </para>
/// <para>
/// The metadata lowerers gather constraint metadata from every refinement layer with
/// <see cref="M:Axial.Schema.Value.allConstraints``1" /> and lower it through
/// <see cref="T:Axial.Validation.Schema.SchemaConstraintCheck" />, so raw-layer and refined-layer constraints run as
/// one check program.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module ValueSchemaCheck =
    /// <summary>
    /// Adapts a check over a schema's underlying primitive representation into a check over the schema's values.
    /// </summary>
    /// <remarks>
    /// This is the general adapter for arbitrary <see cref="T:Axial.ErrorHandling.Check`1" /> programs, including
    /// programs composed with <c>Check.all</c>, <c>Check.any</c>, and <c>Check.not</c>. The projection to the
    /// underlying primitive representation is created eagerly, so a projection type that does not match the schema's
    /// underlying primitive kind fails here rather than on each checked value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="check" /> or <paramref name="schema" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the check's value type does not match the schema's underlying primitive kind.
    /// </exception>
    let fromUnderlying (check: Check<'primitive>) (schema: ValueSchema<'value>) : Check<'value> =
        if isNull (box check) then
            nullArg (nameof check)

        if isNull (box schema) then
            nullArg (nameof schema)

        let inspect = Value.inspectUnderlying<'value, 'primitive> schema
        fun value -> check (inspect value)

    /// <summary>
    /// Lowers the text-meaning constraint metadata carried by every layer of a value schema into one executable check
    /// over the schema's values.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the schema's underlying primitive kind is not text.
    /// </exception>
    let text (schema: ValueSchema<'value>) : Check<'value> =
        if isNull (box schema) then
            nullArg (nameof schema)

        fromUnderlying (SchemaConstraintCheck.text (Value.allConstraints schema)) schema

    /// <summary>
    /// Lowers the range-meaning constraint metadata carried by every layer of a value schema into one executable check
    /// over the schema's values.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the ordered primitive type does not match the schema's underlying primitive kind.
    /// </exception>
    let ordered<'primitive, 'value when 'primitive: comparison> (schema: ValueSchema<'value>) : Check<'value> =
        if isNull (box schema) then
            nullArg (nameof schema)

        fromUnderlying (SchemaConstraintCheck.ordered<'primitive> (Value.allConstraints schema)) schema
