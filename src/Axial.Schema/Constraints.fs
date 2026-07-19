// The portable constraint vocabulary: one Constraint = a code ("minLength"), typed metadata for
// interpreters, and arguments. Constraints carry no checking logic — SchemaValidation.fs interprets
// them at parse/check time, Axial.Schema.JsonSchema lowers them to keywords, and Syntax (Shape.fs)
// wraps them in typed Constraint<'value> for authoring.
namespace Axial.Schema

open System
open System.Collections.Generic

/// <summary>Identifies the portable, typed meaning of a schema constraint.</summary>
/// <remarks>
/// <para>
/// Constraint metadata is the interpreter-facing shape for diagnostics, JSON Schema, UI, and documentation generation.
/// It keeps well-known constraints pattern-matchable without forcing interpreters to decode stable codes and boxed
/// argument dictionaries for the common cases. The stable code and argument map remain available for wire formats,
/// custom constraints, and forward-compatible tooling.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
type ConstraintMetadata =
    /// <summary>A boundary value must be supplied.</summary>
    | Required
    /// <summary>A boundary value may be omitted.</summary>
    | Optional
    /// <summary>A text value must have at least the supplied length.</summary>
    | MinLength of minimum: int
    /// <summary>A text value must have at most the supplied length.</summary>
    | MaxLength of maximum: int
    /// <summary>A text value length must fall inside the supplied inclusive bounds.</summary>
    | LengthBetween of minimum: int * maximum: int
    /// <summary>A text value must use Axial's pragmatic email format.</summary>
    | Email
    /// <summary>A text value must have no leading or trailing whitespace.</summary>
    | Trimmed
    /// <summary>A text value must match the supplied regular expression pattern.</summary>
    | Pattern of pattern: string
    /// <summary>A text value must equal one of the supplied choices.</summary>
    | OneOf of choices: string list
    /// <summary>A value must not equal the supplied unexpected value.</summary>
    | NotEqualTo of unexpected: obj
    /// <summary>An ordered value must fall inside the supplied inclusive bounds.</summary>
    | Between of minimum: obj * maximum: obj
    /// <summary>An ordered value must be greater than the supplied exclusive lower bound.</summary>
    | GreaterThan of minimum: obj
    /// <summary>An ordered value must be less than the supplied exclusive upper bound.</summary>
    | LessThan of maximum: obj
    /// <summary>An ordered value must be greater than or equal to the supplied lower bound.</summary>
    | AtLeast of minimum: obj
    /// <summary>An ordered value must be less than or equal to the supplied upper bound.</summary>
    | AtMost of maximum: obj
    /// <summary>A collection value must contain exactly the supplied count.</summary>
    | Count of expected: int
    /// <summary>A collection value must contain at least the supplied count.</summary>
    | MinCount of minimum: int
    /// <summary>A collection value must contain at most the supplied count.</summary>
    | MaxCount of maximum: int
    /// <summary>A collection value count must fall inside the supplied inclusive bounds.</summary>
    | CountBetween of minimum: int * maximum: int
    /// <summary>A collection value must contain no duplicate items.</summary>
    | Distinct
    /// <summary>A collection value must contain the supplied item.</summary>
    | Contains of item: obj
    /// <summary>A numeric value must be an integer multiple of the supplied divisor.</summary>
    | MultipleOf of divisor: obj
    /// <summary>A custom or not-yet-known constraint identified by its stable code.</summary>
    | Custom of code: string

/// <summary>
/// Describes a portable schema constraint as inspectable metadata.
/// </summary>
/// <remarks>
/// <para>
/// Schema constraints are declarative data for interpreters. They are intentionally separate from executable check
/// functions so input parsers, diagnostics, JSON Schema emitters, UI renderers, and documentation generators can inspect
/// the same constraint without running validation logic.
/// </para>
/// <para>
/// The generic metadata shape comes before the named constraint helpers. Later helpers such as required, max length, and
/// numeric ranges can create these values with stable codes and arguments while still lowering to executable checks in
/// validation-oriented interpreters.
/// </para>
/// </remarks>
[<Sealed; AllowNullLiteral>]
type Constraint internal (
    code: string,
    metadata: ConstraintMetadata,
    arguments: IReadOnlyDictionary<string, obj>,
    message: string option
) =
    /// <summary>Gets the stable interpreter-facing constraint code.</summary>
    member _.Code = code

    /// <summary>Gets the typed interpreter-facing constraint metadata.</summary>
    member _.Metadata = metadata

    /// <summary>Gets the structured constraint arguments keyed by stable interpreter-facing names.</summary>
    member _.Arguments = arguments

    /// <summary>Gets the author-supplied message override, when one was attached with <see cref="M:Axial.Schema.Constraint.withMessage" />.</summary>
    member _.Message = message

    override _.ToString() = code

/// <summary>Functions for creating and inspecting schema constraint metadata.</summary>
[<RequireQualifiedAccess>]
module Constraint =
    let private ensureName parameterName (value: string) =
        if isNull value then
            nullArg parameterName

        if String.IsNullOrWhiteSpace value then
            invalidArg parameterName "Schema constraint names must not be empty or whitespace."

    let private ensureNonNegative parameterName value =
        if value < 0 then
            Platform.argumentOutOfRange parameterName (box value) "Schema constraint bounds must be zero or greater."

    let private ensureOrderedBounds parameterName minimum maximum =
        if minimum > maximum then
            invalidArg parameterName "Schema constraint minimum bounds must be less than or equal to maximum bounds."

    let private freezeArguments (values: Dictionary<string, obj>) = Platform.freezeDictionary values

    let private emptyArguments =
        freezeArguments (Dictionary<string, obj>())

    let private createKnown code metadata =
        ensureName (nameof code) code
        Constraint(code, metadata, emptyArguments, None)

    let private createKnownWithArguments code metadata (arguments: (string * obj) seq) =
        ensureName (nameof code) code

        if isNull (box arguments) then
            nullArg (nameof arguments)

        let values = Dictionary<string, obj>()

        arguments
        |> Seq.iter (fun (name, value) ->
            ensureName (nameof arguments) name
            values.Add(name, value))

        Constraint(code, metadata, freezeArguments values, None)

    /// <summary>Creates portable custom schema constraint metadata with no arguments.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="code" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="code" /> is empty or contains only whitespace.
    /// </exception>
    let create code =
        ensureName (nameof code) code
        Constraint(code, ConstraintMetadata.Custom code, emptyArguments, None)

    /// <summary>Creates portable custom schema constraint metadata with structured arguments.</summary>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="code" />, <paramref name="arguments" />, or an argument name is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="code" /> or an argument name is empty or contains only whitespace.
    /// </exception>
    let createWithArguments code (arguments: (string * obj) seq) =
        ensureName (nameof code) code

        if isNull (box arguments) then
            nullArg (nameof arguments)

        let values = Dictionary<string, obj>()

        arguments
        |> Seq.iter (fun (name, value) ->
            ensureName (nameof arguments) name
            values.Add(name, value))

        Constraint(
            code,
            ConstraintMetadata.Custom code,
            freezeArguments values,
            None
        )

    /// <summary>Requires a value to be supplied by boundary interpreters.</summary>
    let required = createKnown "required" ConstraintMetadata.Required

    /// <summary>Marks a value as optional for boundary interpreters.</summary>
    let optional = createKnown "optional" ConstraintMetadata.Optional

    /// <summary>Requires a text value to have at least the supplied length.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="minimum" /> is negative.</exception>
    let minLength minimum =
        ensureNonNegative (nameof minimum) minimum
        createKnownWithArguments "minLength" (ConstraintMetadata.MinLength minimum) [ "minimum", box minimum ]

    /// <summary>Requires a text value to have at most the supplied length.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="maximum" /> is negative.</exception>
    let maxLength maximum =
        ensureNonNegative (nameof maximum) maximum
        createKnownWithArguments "maxLength" (ConstraintMetadata.MaxLength maximum) [ "maximum", box maximum ]

    /// <summary>Requires a text value to have a length inside the supplied inclusive bounds.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minimum" /> or <paramref name="maximum" /> is negative.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="minimum" /> is greater than <paramref name="maximum" />.
    /// </exception>
    let lengthBetween minimum maximum =
        ensureNonNegative (nameof minimum) minimum
        ensureNonNegative (nameof maximum) maximum
        ensureOrderedBounds (nameof minimum) minimum maximum
        createKnownWithArguments
            "lengthBetween"
            (ConstraintMetadata.LengthBetween(minimum, maximum))
            [ "minimum", box minimum; "maximum", box maximum ]

    /// <summary>Requires a text value to match Axial's pragmatic email format.</summary>
    let email = createKnown "email" ConstraintMetadata.Email

    /// <summary>Requires a text value to have no leading or trailing whitespace.</summary>
    let trimmed = createKnown "trimmed" ConstraintMetadata.Trimmed

    /// <summary>Requires a text value to match the supplied regular expression pattern.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="pattern" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="pattern" /> is empty or contains only whitespace.
    /// </exception>
    let pattern pattern =
        ensureName (nameof pattern) pattern
        createKnownWithArguments "pattern" (ConstraintMetadata.Pattern pattern) [ "pattern", box pattern ]

    /// <summary>Requires a text value to equal one of the supplied choices.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="choices" /> is null.</exception>
    let oneOf (choices: string seq) =
        if isNull (box choices) then
            nullArg (nameof choices)

        let choices = choices |> Seq.toList
        createKnownWithArguments
            "oneOf"
            (ConstraintMetadata.OneOf choices)
            [ "choices", choices |> List.toArray |> box ]

    /// <summary>Requires a value to not equal the supplied unexpected value.</summary>
    let notEqualTo unexpected =
        createKnownWithArguments
            "notEqualTo"
            (ConstraintMetadata.NotEqualTo(box unexpected))
            [ "unexpected", box unexpected ]

    /// <summary>Requires a value to be inside the supplied inclusive numeric bounds.</summary>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="minimum" /> is greater than <paramref name="maximum" />.
    /// </exception>
    let between minimum maximum =
        ensureOrderedBounds (nameof minimum) minimum maximum
        createKnownWithArguments
            "between"
            (ConstraintMetadata.Between(box minimum, box maximum))
            [ "minimum", box minimum; "maximum", box maximum ]

    /// <summary>Requires a value to be greater than the supplied exclusive numeric lower bound.</summary>
    let greaterThan minimum =
        createKnownWithArguments
            "greaterThan"
            (ConstraintMetadata.GreaterThan(box minimum))
            [ "minimum", box minimum ]

    /// <summary>Requires a value to be less than the supplied exclusive numeric upper bound.</summary>
    let lessThan maximum =
        createKnownWithArguments
            "lessThan"
            (ConstraintMetadata.LessThan(box maximum))
            [ "maximum", box maximum ]

    /// <summary>Requires a value to be greater than or equal to the supplied numeric lower bound.</summary>
    let atLeast minimum =
        createKnownWithArguments
            "atLeast"
            (ConstraintMetadata.AtLeast(box minimum))
            [ "minimum", box minimum ]

    /// <summary>Requires a value to be less than or equal to the supplied numeric upper bound.</summary>
    let atMost maximum =
        createKnownWithArguments
            "atMost"
            (ConstraintMetadata.AtMost(box maximum))
            [ "maximum", box maximum ]

    /// <summary>Requires a collection value to contain exactly the supplied count.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="expected" /> is negative.</exception>
    let count expected =
        ensureNonNegative (nameof expected) expected
        createKnownWithArguments "count" (ConstraintMetadata.Count expected) [ "expected", box expected ]

    /// <summary>Requires a collection value to contain at least the supplied count.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="minimum" /> is negative.</exception>
    let minCount minimum =
        ensureNonNegative (nameof minimum) minimum
        createKnownWithArguments "minCount" (ConstraintMetadata.MinCount minimum) [ "minimum", box minimum ]

    /// <summary>Requires a collection value to contain at most the supplied count.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="maximum" /> is negative.</exception>
    let maxCount maximum =
        ensureNonNegative (nameof maximum) maximum
        createKnownWithArguments "maxCount" (ConstraintMetadata.MaxCount maximum) [ "maximum", box maximum ]

    /// <summary>Requires a collection value to contain a count inside the supplied inclusive bounds.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minimum" /> or <paramref name="maximum" /> is negative.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="minimum" /> is greater than <paramref name="maximum" />.
    /// </exception>
    let countBetween minimum maximum =
        ensureNonNegative (nameof minimum) minimum
        ensureNonNegative (nameof maximum) maximum
        ensureOrderedBounds (nameof minimum) minimum maximum
        createKnownWithArguments
            "countBetween"
            (ConstraintMetadata.CountBetween(minimum, maximum))
            [ "minimum", box minimum; "maximum", box maximum ]

    /// <summary>Requires a numeric value to be an integer multiple of the supplied divisor.</summary>
    let multipleOf divisor =
        createKnownWithArguments "multipleOf" (ConstraintMetadata.MultipleOf(box divisor)) [ "divisor", box divisor ]

    /// <summary>Requires a collection value to contain no duplicate items.</summary>
    let distinct = createKnown "distinct" ConstraintMetadata.Distinct

    /// <summary>Requires a collection value to contain the supplied item.</summary>
    let contains (item: 'value) =
        createKnownWithArguments "contains" (ConstraintMetadata.Contains(box item)) [ "item", box item ]

    /// <summary>Requires a value to be greater than zero. Alias for <see cref="M:Axial.Schema.Constraint.greaterThan" /> with a generic zero bound.</summary>
    let inline positive<'value when 'value: (static member Zero: 'value)> () =
        greaterThan LanguagePrimitives.GenericZero<'value>

    /// <summary>Requires a value to be greater than or equal to zero. Alias for <see cref="M:Axial.Schema.Constraint.atLeast" /> with a generic zero bound.</summary>
    let inline nonNegative<'value when 'value: (static member Zero: 'value)> () =
        atLeast LanguagePrimitives.GenericZero<'value>

    /// <summary>Requires a value to be less than zero. Alias for <see cref="M:Axial.Schema.Constraint.lessThan" /> with a generic zero bound.</summary>
    let inline negative<'value when 'value: (static member Zero: 'value)> () =
        lessThan LanguagePrimitives.GenericZero<'value>

    /// <summary>Requires a value to be less than or equal to zero. Alias for <see cref="M:Axial.Schema.Constraint.atMost" /> with a generic zero bound.</summary>
    let inline nonPositive<'value when 'value: (static member Zero: 'value)> () =
        atMost LanguagePrimitives.GenericZero<'value>

    /// <summary>Returns the stable interpreter-facing constraint code.</summary>
    let code (constraint': Constraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

        constraint'.Code

    /// <summary>Returns the typed interpreter-facing constraint metadata.</summary>
    let metadata (constraint': Constraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

        constraint'.Metadata

    /// <summary>Returns the structured constraint arguments.</summary>
    let arguments (constraint': Constraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

        constraint'.Arguments

    /// <summary>Returns a structured constraint argument when present.</summary>
    let tryFindArgument name (constraint': Constraint) =
        ensureName (nameof name) name

        if isNull constraint' then
            nullArg (nameof constraint')

        match constraint'.Arguments.TryGetValue name with
        | true, value -> Some value
        | false, _ -> None

    /// <summary>Returns the author-supplied message override, when one was attached with <see cref="M:Axial.Schema.Constraint.withMessage" />.</summary>
    let message (constraint': Constraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

        constraint'.Message

    /// <summary>
    /// Attaches a custom author-supplied message to a schema constraint, overriding the default message an
    /// interpreter would otherwise produce for it.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="message" /> or <paramref name="constraint'" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="message" /> is empty or contains only whitespace.
    /// </exception>
    let withMessage (message: string) (constraint': Constraint) =
        ensureName (nameof message) message

        if isNull constraint' then
            nullArg (nameof constraint')

        Constraint(constraint'.Code, constraint'.Metadata, constraint'.Arguments, Some message)
