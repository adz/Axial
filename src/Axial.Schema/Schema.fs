namespace Axial.Schema

open System
open System.Collections.Generic

/// <summary>
/// Represents the source-facing name of a schema field.
/// </summary>
/// <remarks>
/// <para>
/// External field names are the names interpreters use at data boundaries, such as structured data keys, JSON property names,
/// diagnostic paths, generated documentation, and UI field identifiers.
/// </para>
/// <para>
/// The stored value is exact and is not normalized. Construction rejects null, empty, and whitespace-only names so
/// schema definitions cannot describe an unusable boundary field.
/// </para>
/// </remarks>
[<Sealed; AllowNullLiteral>]
type ExternalFieldName internal (value: string) =
    /// <summary>Gets the exact external field name.</summary>
    member _.Value = value

    override _.ToString() = value

/// <summary>Functions for creating and inspecting external schema field names.</summary>
[<RequireQualifiedAccess>]
module ExternalFieldName =
    /// <summary>Creates an external schema field name from an exact boundary-facing name.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="value" /> is empty or contains only whitespace.
    /// </exception>
    let create (value: string) =
        if isNull value then
            nullArg (nameof value)

        if String.IsNullOrWhiteSpace value then
            invalidArg (nameof value) "External field names must not be empty or whitespace."

        ExternalFieldName value

    /// <summary>Returns the exact boundary-facing string stored in an external schema field name.</summary>
    let value (name: ExternalFieldName) =
        if isNull name then
            nullArg (nameof name)

        name.Value

/// <summary>
/// Represents the zero-based position of a schema field in a model constructor and ordered interpreter output.
/// </summary>
/// <remarks>
/// Field order is explicit schema metadata. It is independent of external field names so interpreters do not need to
/// infer construction or display order from names, reflection, map ordering, or declaration order.
/// </remarks>
[<Struct>]
type FieldOrder internal (value: int) =
    /// <summary>Gets the zero-based field position.</summary>
    member _.Value = value

    override _.ToString() = string value

/// <summary>Functions for creating and inspecting schema field order metadata.</summary>
[<RequireQualifiedAccess>]
module FieldOrder =
    /// <summary>Creates zero-based schema field order metadata.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="value" /> is negative.</exception>
    let create value =
        if value < 0 then
            Platform.argumentOutOfRange (nameof value) (box value) "Field order must be zero or greater."

        FieldOrder value

    /// <summary>Returns the zero-based field position.</summary>
    let value (order: FieldOrder) = order.Value

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

[<ReferenceEquality>]
type internal ConstructorApplication<'model> =
    { ArgumentCount: int
      ApplyTrusted: obj array -> 'model
      TryApplyTrusted: obj array -> Result<'model, string> }

module internal ConstructorApplication =
    let ensureArgumentCount expected (arguments: obj array) =
        if isNull arguments then
            nullArg (nameof arguments)

        if arguments.Length <> expected then
            invalidArg (nameof arguments) $"Expected {expected} constructor argument(s), but received {arguments.Length}."

    let create0 (construct: unit -> 'model) =
        if isNull (box construct) then
            nullArg (nameof construct)

        { ArgumentCount = 0
          ApplyTrusted =
            fun arguments ->
                ensureArgumentCount 0 arguments
                construct ()
          TryApplyTrusted =
            fun arguments ->
                ensureArgumentCount 0 arguments
                Ok(construct ()) }

    let create1 (construct: 'a -> 'model) =
        if isNull (box construct) then
            nullArg (nameof construct)

        { ArgumentCount = 1
          ApplyTrusted =
            fun arguments ->
                ensureArgumentCount 1 arguments
                construct (unbox<'a> arguments[0])
          TryApplyTrusted =
            fun arguments ->
                ensureArgumentCount 1 arguments
                Ok(construct (unbox<'a> arguments[0])) }

    let create2 (construct: 'a -> 'b -> 'model) =
        if isNull (box construct) then
            nullArg (nameof construct)

        { ArgumentCount = 2
          ApplyTrusted =
            fun arguments ->
                ensureArgumentCount 2 arguments
                construct (unbox<'a> arguments[0]) (unbox<'b> arguments[1])
          TryApplyTrusted =
            fun arguments ->
                ensureArgumentCount 2 arguments
                Ok(construct (unbox<'a> arguments[0]) (unbox<'b> arguments[1])) }

    let create3 (construct: 'a -> 'b -> 'c -> 'model) =
        if isNull (box construct) then
            nullArg (nameof construct)

        { ArgumentCount = 3
          ApplyTrusted =
            fun arguments ->
                ensureArgumentCount 3 arguments
                construct (unbox<'a> arguments[0]) (unbox<'b> arguments[1]) (unbox<'c> arguments[2])
          TryApplyTrusted =
            fun arguments ->
                ensureArgumentCount 3 arguments
                Ok(construct (unbox<'a> arguments[0]) (unbox<'b> arguments[1]) (unbox<'c> arguments[2])) }

    let apply (application: ConstructorApplication<'model>) (arguments: obj array) =
        if isNull (box application) then
            nullArg (nameof application)

        application.ApplyTrusted arguments

    let tryApply (application: ConstructorApplication<'model>) (arguments: obj array) =
        if isNull (box application) then
            nullArg (nameof application)

        application.TryApplyTrusted arguments

/// <summary>Identifies the intrinsic primitive shape of a schema value.</summary>
/// <remarks>
/// Primitive kinds are portable metadata for interpreters such as input parsers, codecs, JSON Schema emitters, UI
/// renderers, and documentation generators. They describe the trusted .NET value shape without selecting any
/// particular serialized representation.
/// </remarks>
type PrimitiveValueKind =
    /// <summary>Text represented as <see cref="T:System.String" />.</summary>
    | Text
    /// <summary>A 32-bit signed integer represented as <see cref="T:System.Int32" />.</summary>
    | Int
    /// <summary>A decimal number represented as <see cref="T:System.Decimal" />.</summary>
    | Decimal
    /// <summary>A Boolean value represented as <see cref="T:System.Boolean" />.</summary>
    | Bool
    /// <summary>A calendar date represented as <see cref="T:System.DateOnly" />.</summary>
    | Date
    /// <summary>An instant-like date and time represented as <see cref="T:System.DateTimeOffset" />.</summary>
    | DateTime
    /// <summary>A globally unique identifier represented as <see cref="T:System.Guid" />.</summary>
    | Guid

/// <summary>Identifies the portable named format of a schema value, such as <c>email</c>.</summary>
/// <remarks>
/// <para>
/// Format metadata names the boundary-facing interpretation of a value beyond its primitive kind. Interpreters use it
/// without running validation logic: JSON Schema emitters lower it to the <c>format</c> keyword, UI renderers select
/// input controls from it, and documentation generators describe the expected shape with it.
/// </para>
/// <para>
/// A format is annotation metadata, not a constraint. Declaring a format does not attach any executable check;
/// validation-facing requirements such as <see cref="P:Axial.Schema.Constraint.email" /> remain separate
/// constraint metadata.
/// </para>
/// </remarks>
[<Struct>]
type SchemaFormat internal (name: string) =
    /// <summary>Gets the stable interpreter-facing format name.</summary>
    member _.Name = name

    override _.ToString() = name

/// <summary>Functions for creating and inspecting schema value format metadata.</summary>
[<RequireQualifiedAccess>]
module SchemaFormat =
    /// <summary>Creates portable schema format metadata from a stable interpreter-facing name.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="name" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="name" /> is empty or contains only whitespace.
    /// </exception>
    let create (name: string) =
        if isNull name then
            nullArg (nameof name)

        if String.IsNullOrWhiteSpace name then
            invalidArg (nameof name) "Schema format names must not be empty or whitespace."

        SchemaFormat name

    /// <summary>The well-known email address format.</summary>
    let email = create "email"

    /// <summary>Returns the stable interpreter-facing format name.</summary>
    let name (format: SchemaFormat) = format.Name

/// <summary>
/// Holds the type-erased construction and inspection functions for a refined/domain value schema.
/// </summary>
/// <remarks>
/// <para>
/// Both functions are mandatory. Construction interpreters such as input parsing and codec decoding need
/// <see cref="P:Axial.Schema.RefinedValueOps.Construct" />, while inspection interpreters such as validation of
/// existing models, codec encoding, JSON Schema, UI, and documentation generation need
/// <see cref="P:Axial.Schema.RefinedValueOps.Inspect" />. Enforcing the pairing here means no construction path can
/// create a construct-only or inspect-only refined value schema.
/// </para>
/// <para>
/// This is a plain reference type rather than a record so it keeps reference equality: the boxed functions it wraps
/// cannot support structural equality, and reference equality is enough for the internal schema-shape union.
/// </para>
/// </remarks>
type internal RefinedValueOps(construct: obj -> Result<obj, SchemaError list>, inspect: obj -> obj) =
    do
        if isNull (box construct) then
            nullArg (nameof construct)

        if isNull (box inspect) then
            nullArg (nameof inspect)

    member _.Construct = construct
    member _.Inspect = inspect

type internal ValueSchemaDefinition =
    { Shape: ValueSchemaShape
      Format: SchemaFormat option
      Constraints: Constraint list
      Description: string option
      Default: obj option }

and internal ValueSchemaShape =
    | PrimitiveValueDefinition of PrimitiveValueKind
    /// <summary>A named refined/domain value built from a raw value schema plus construction and inspection functions.</summary>
    | RefinedValueDefinition of raw: ValueSchemaDefinition * ops: RefinedValueOps
    /// <summary>
    /// A nested model value described by another type-erased model schema, plus the boxed original
    /// <c>Schema&lt;'nested&gt;</c> so constructor-specialized interpreters such as codecs can recover the typed field
    /// chain with <c>Schema.compilePlan</c> instead of falling back to boxed <c>obj array</c> dispatch.
    /// </summary>
    | NestedValueDefinition of nested: ModelSchemaDefinition<obj> * source: obj
    /// <summary>A collection value whose items are each described by the same item value schema.</summary>
    | ManyValueDefinition of CollectionValueDefinition
    /// <summary>A tagged union value whose case payloads are each described by explicit value schemas.</summary>
    | UnionValueDefinition of TaggedUnionValueDefinition
    /// <summary>An internally-tagged union value whose case payloads are spliced beside the discriminator field.</summary>
    | UnionInlineValueDefinition of InlineTaggedUnionValueDefinition
    /// <summary>A bare-string enum value for payload-less union cases.</summary>
    | EnumValueDefinition of TaggedEnumValueDefinition
    /// <summary>An optional value: absent input is a legal <c>None</c>, present input parses through the payload schema.</summary>
    | OptionValueDefinition of OptionalValueDefinition
    /// <summary>A dictionary value whose entries are each described by the same item value schema, keyed by text.</summary>
    | MapValueDefinition of MapValueDefinition
    /// <summary>A memoized deferred value definition used to close recursive model graphs.</summary>
    | LazyValueDefinition of DeferredValueDefinition

and [<ReferenceEquality>] internal DeferredValueDefinition =
    { Force: unit -> ValueSchemaDefinition }

/// <summary>
/// Holds the type-erased payload value schema for an optional value, plus closures that move between the boxed
/// payload representation and the boxed <c>'value option</c> the model field carries.
/// </summary>
/// <remarks>
/// The closures are captured at <c>Schema.optionOf</c> call sites where the payload CLR type is still statically known,
/// so interpreters such as input parsing and codecs can wrap parsed payloads into <c>Some</c> and unwrap existing
/// trusted options without runtime reflection.
/// </remarks>
and [<ReferenceEquality>] internal OptionalValueDefinition =
    { Payload: ValueSchemaDefinition
      WrapSome: obj -> obj
      NoneValue: obj
      TryUnwrap: obj -> obj option }

/// <summary>
/// Holds the type-erased item value schema for a collection value, plus a closure that boxes a list of parsed,
/// type-erased items back into the collection's original CLR item list type.
/// </summary>
/// <remarks>
/// The boxing closure is captured at <c>Schema.many</c> call sites where the item CLR type is still statically known,
/// so interpreters such as input parsing can build a correctly-typed <c>'item list</c> without runtime reflection.
/// </remarks>
and [<ReferenceEquality>] internal CollectionValueDefinition =
    { Item: ValueSchemaDefinition
      BoxItems: obj list -> obj
      /// <summary>
      /// Reintroduces the statically-known item type to an interpreter. The closure is captured at
      /// <c>Schema.manyOf</c> call sites, so interpreters such as codecs can build typed item plans
      /// (for example a <c>Decoder&lt;'item list&gt;</c>) without reflection or per-item boxing.
      /// </summary>
      AcceptItem: ICollectionItemInterpreter -> obj }

/// <summary>Rebuilds typed collection-item plans from a type-erased collection value schema.</summary>
/// <remarks>
/// Implemented by interpreters that need the collection's statically-known item type back, such as codec compilers.
/// <see cref="P:Axial.Schema.CollectionValueDefinition.AcceptItem" /> invokes
/// <see cref="M:Axial.Schema.ICollectionItemInterpreter.Item``1" /> with the original <c>'item</c> type argument, and
/// the interpreter returns its typed plan boxed as <c>obj</c>; callers know the concrete plan type statically because
/// they know the collection field's own type.
/// </remarks>
and internal ICollectionItemInterpreter =
    abstract member Item<'item> : item: ValueSchemaDefinition -> obj

/// <summary>
/// Holds the type-erased item value schema for a dictionary/map value, plus a closure that boxes a list of
/// parsed, type-erased key/value entries back into the map's original CLR <c>Map&lt;string,'item&gt;</c> type.
/// </summary>
/// <remarks>
/// Keys are always text: a JSON object's field names are the map's keys, so there is no separate key schema.
/// The boxing closure is captured at <c>Schema.mapOf</c> call sites where the item CLR type is still statically
/// known, so interpreters such as input parsing can build a correctly-typed <c>Map&lt;string,'item&gt;</c> without
/// reflection.
/// </remarks>
and [<ReferenceEquality>] internal MapValueDefinition =
    { Item: ValueSchemaDefinition
      BoxEntries: (string * obj) list -> obj
      /// <summary>
      /// Projects a trusted, boxed <c>Map&lt;string,'item&gt;</c> back into a type-erased key/value entry list.
      /// Captured at <c>Schema.map</c>/<c>Schema.mapWith</c> call sites where the item CLR type is still statically known, so interpreters
      /// such as model validation and codec encoding can walk entries without reflection.
      /// </summary>
      Entries: obj -> (string * obj) list
      /// <summary>
      /// Reintroduces the statically-known item type to an interpreter. The closure is captured at
      /// <c>Schema.mapOf</c> call sites, so interpreters such as codecs can build typed item plans without
      /// reflection or per-item boxing.
      /// </summary>
      AcceptItem: ICollectionItemInterpreter -> obj }

and [<ReferenceEquality>] internal UnionCaseValueDefinition =
    { Tag: string
      Payload: ValueSchemaDefinition
      Construct: obj -> obj
      TryInspect: obj -> obj option }

and [<ReferenceEquality>] internal TaggedUnionValueDefinition =
    { DiscriminatorField: ExternalFieldName
      PayloadField: ExternalFieldName
      Cases: UnionCaseValueDefinition list }

/// <summary>
/// A tagged union case whose payload is spliced beside the discriminator field rather than nested under a
/// separate payload field. The payload must be a nested model value schema so its fields are known upfront.
/// </summary>
and [<ReferenceEquality>] internal InlineTaggedUnionValueDefinition =
    { DiscriminatorField: ExternalFieldName
      Cases: UnionCaseValueDefinition list }

and [<ReferenceEquality>] internal EnumCaseValueDefinition = { Tag: string; Value: obj }

and [<ReferenceEquality>] internal TaggedEnumValueDefinition = { Cases: EnumCaseValueDefinition list }

and [<ReferenceEquality>] internal FieldDescriptor<'model> =
    { ExternalName: ExternalFieldName
      Order: FieldOrder
      Getter: 'model -> obj
      ValueSchema: ValueSchemaDefinition
      Constraints: Constraint list }

and [<ReferenceEquality>] internal ModelSchemaDefinition<'model> =
    { Constructor: ConstructorApplication<'model>
      Fields: FieldDescriptor<'model> list
      Description: string option }

/// <summary>Describes one tagged union case for <c>Schema.union</c>.</summary>
[<Sealed>]
type UnionCase<'union> internal (definition: UnionCaseValueDefinition) =
    member internal _.Definition = definition

/// <summary>Describes one payload-less case for <c>Schema.enumOf</c>.</summary>
[<Sealed>]
type EnumCase<'enum> internal (definition: EnumCaseValueDefinition) =
    member internal _.Definition = definition

type internal SchemaDefinition<'model> =
    | PendingDefinition
    | ModelDefinition of ModelSchemaDefinition<'model>
    | ValueDefinition of ValueSchemaDefinition

/// <summary>Type-erases a model schema definition so it can be stored as a nested value schema shape.</summary>
module internal ModelSchemaErasure =
    let erase (definition: ModelSchemaDefinition<'model>) : ModelSchemaDefinition<obj> =
        { Constructor =
            { ArgumentCount = definition.Constructor.ArgumentCount
              ApplyTrusted = fun arguments -> definition.Constructor.ApplyTrusted arguments |> box
              TryApplyTrusted =
                fun arguments ->
                    definition.Constructor.TryApplyTrusted arguments
                    |> Result.map box }
          Fields =
            definition.Fields
            |> List.map (fun field ->
                { ExternalName = field.ExternalName
                  Order = field.Order
                  Getter = fun (model: obj) -> field.Getter (unbox<'model> model)
                  ValueSchema = field.ValueSchema
                  Constraints = field.Constraints })
          Description = definition.Description }

type internal FieldDefinition<'model, 'value> =
    { ExternalName: ExternalFieldName
      Order: FieldOrder
      Getter: 'model -> 'value
      ValueSchema: ValueSchemaDefinition
      Constraints: Constraint list }

/// <summary>
/// Describes one typed field of a trusted model for schema interpreters.
/// </summary>
/// <remarks>
/// <para>
/// A field definition records typed field metadata without tying that metadata to input parsing, diagnostics,
/// validation, codecs, UI generation, or workflow execution. The field's external name is the portable boundary-facing
/// name interpreters use for structured data lookup, diagnostic paths, codecs, generated documentation, and UI metadata.
/// Its getter reads the field value from an already trusted model so inspection interpreters can observe existing
/// values without using reflection.
/// </para>
/// <para>
/// Constructor application, ordering, and public construction helpers are introduced by the schema operations that
/// follow this core type.
/// </para>
/// </remarks>
[<Sealed>]
type Field<'model, 'value> internal (definition: FieldDefinition<'model, 'value>) =
    member internal _.Definition = definition

/// <summary>Holds an interpreter-specific typed record-plan fragment while compiling a schema.</summary>
/// <remarks>
/// Schema interpreters use this as the typed accumulator returned by
/// <see cref="T:Axial.Schema.IRecordPlanCompiler`2" />. The value is intentionally opaque to <c>Axial.Schema</c>;
/// each interpreter owns the concrete plan shape it stores here.
/// </remarks>
type IRecordPlanState<'model, 'constructorIn, 'constructorOut> =
    /// <summary>Gets the interpreter-owned typed record-plan fragment.</summary>
    abstract member Value: obj

/// <summary>
/// Builds an interpreter-specific typed record plan from a model schema's authored shape.
/// </summary>
/// <remarks>
/// The built <see cref="T:Axial.Schema.Schema`1" /> keeps this typed shape alongside its type-erased
/// <c>FieldDescriptor</c> metadata. Interpreters that need constructor-specialized plans, such as codecs, can walk the
/// record plan through this compiler without asking callers to re-supply fields or constructors and without lowering
/// construction to <c>obj array</c> dispatch.
/// </remarks>
type IRecordPlanCompiler<'model, 'result> =
    /// <summary>Starts a record plan for a constructor with no consumed fields.</summary>
    abstract member OnEnd<'constructor> : unit -> IRecordPlanState<'model, 'constructor, 'constructor>

    /// <summary>Appends one typed field to an interpreter-specific record plan.</summary>
    abstract member OnField<'constructorIn, 'field, 'next> :
        order: int *
        field: Field<'model, 'field> *
        head: IRecordPlanState<'model, 'constructorIn, 'field -> 'next> ->
            IRecordPlanState<'model, 'constructorIn, 'next>

    /// <summary>Completes a record plan with the original typed constructor.</summary>
    abstract member OnComplete<'constructor, 'constructed> :
        constructor: 'constructor *
        plan: IRecordPlanState<'model, 'constructor, 'constructed> *
        finish: ('constructed -> Result<'model, string>) -> 'result

type IShapeFields<'model, 'constructor, 'remaining> =
    abstract member GetFields: int -> obj list * int
    abstract member Apply: constructor: obj * arguments: obj array -> obj
    abstract member Build<'result> :
        factory: IRecordPlanCompiler<'model, 'result> -> IRecordPlanState<'model, 'constructor, 'remaining>

type internal ShapeFieldsEmpty<'model, 'constructor>() =
    interface IShapeFields<'model, 'constructor, 'constructor> with
        member _.GetFields(index) = [], index
        member _.Apply(constructor, _) = constructor
        member _.Build(factory) = factory.OnEnd()

type internal ShapeFieldsAppend<'model, 'constructor, 'field, 'next, 'head
    when 'head :> IShapeFields<'model, 'constructor, 'field -> 'next>>
    internal
    (
        head: 'head,
        field: FieldDefinition<'model, 'field>
    ) =

    interface IShapeFields<'model, 'constructor, 'next> with
        member _.GetFields(index) =
            let fields, nextIndex = (head :> IShapeFields<'model, 'constructor, 'field -> 'next>).GetFields index

            let descriptor =
                { FieldDescriptor.ExternalName = field.ExternalName
                  Order = FieldOrder.create nextIndex
                  Getter = fun model -> field.Getter model |> box
                  ValueSchema = field.ValueSchema
                  Constraints = field.Constraints }

            fields @ [ box descriptor ], nextIndex + 1

        member _.Apply(constructor, arguments) =
            let fieldIndex = (head :> IShapeFields<'model, 'constructor, 'field -> 'next>).GetFields(0) |> snd
            let appliedHead =
                (head :> IShapeFields<'model, 'constructor, 'field -> 'next>).Apply(constructor, arguments)

            let typedConstructor = unbox<'field -> 'next> appliedHead
            typedConstructor (unbox<'field> arguments[fieldIndex]) |> box

        member _.Build(factory) =
            let headNode = head :> IShapeFields<'model, 'constructor, 'field -> 'next>
            let headResult = headNode.Build(factory)
            let order = headNode.GetFields(0) |> snd

            let typedField =
                Field(
                    { field with
                        Order = FieldOrder.create order }
                )

            factory.OnField(order, typedField, headResult)

/// <summary>
/// Internal closing state that combines a constructor with the typed fields recovered from an object shape.
/// </summary>
/// <remarks>
/// Constructor-last arity dispatch creates this value after matching the shape's phantom field types against the
/// constructor. It is not an authoring surface; it carries the typed plan into compiled interpreters.
/// </remarks>
type internal ShapeClosure<'model, 'constructor, 'remaining, 'chain
    when 'chain :> IShapeFields<'model, 'constructor, 'remaining>>
    internal
    (
        constructor: 'constructor,
        fields: 'chain
    ) =
    member internal _.Constructor = constructor
    member internal _.Fields = fields

type internal ICompiledRecordPlan<'model> =
    abstract member CompilePlan<'result> : factory: IRecordPlanCompiler<'model, 'result> -> 'result

type internal CompiledRecordPlan<'model, 'constructor, 'constructed, 'fields
    when 'fields :> IShapeFields<'model, 'constructor, 'constructed>>
    (
        constructor: 'constructor,
        fields: 'fields,
        finish: 'constructed -> Result<'model, string>
    ) =

    interface ICompiledRecordPlan<'model> with
        member _.CompilePlan(factory) =
            if isNull (box factory) then
                nullArg (nameof factory)

            let result = fields.Build(factory)
            factory.OnComplete(constructor, result, finish)

module internal ModelSchemaDefinition =
    let private ensureContiguousOrders (fields: FieldDescriptor<'model> list) =
        let orders = fields |> List.map (fun field -> field.Order.Value) |> List.sort

        let expected = [ 0 .. (List.length fields - 1) ]

        if orders <> expected then
            invalidArg (nameof fields) "Model schema fields must use contiguous zero-based field order."

    let create (constructor: ConstructorApplication<'model>) (fields: FieldDescriptor<'model> list) =
        if isNull (box constructor) then
            nullArg (nameof constructor)

        if isNull (box fields) then
            nullArg (nameof fields)

        fields
        |> List.iter (fun field ->
            if isNull (box field) then
                nullArg (nameof fields))

        if fields.Length <> constructor.ArgumentCount then
            invalidArg
                (nameof fields)
                $"Expected {constructor.ArgumentCount} ordered field(s), but received {fields.Length}."

        ensureContiguousOrders fields

        fields
        |> List.iter (fun field ->
            match field.ValueSchema.Shape with
            | OptionValueDefinition _ when
                field.Constraints
                |> List.exists (fun constraint' -> constraint'.Code = "required")
                ->
                invalidArg (nameof fields) "Optional fields cannot carry the required constraint."
            | _ -> ())

        { Constructor = constructor
          Fields = fields |> List.sortBy (fun field -> field.Order.Value)
          Description = None }

/// <summary>
/// Describes a typed value's portable structure and construction for schema interpreters.
/// </summary>
/// <remarks>
/// <para>
/// A schema records shape and construction metadata without tying that metadata to input parsing,
/// diagnostics, validation, codecs, UI generation, or workflow execution.
/// </para>
/// <para>
/// Primitive, collection, optional, union, refined, and record declarations all produce <c>Schema&lt;'value&gt;</c>.
/// Object declarations start with <c>Schema.define</c>, add fields through <c>Syntax</c>, and finish with
/// <c>Syntax.construct</c> or <c>Syntax.constructResult</c>.
/// </para>
/// </remarks>
// No class-level `as this` self-identifier here: combined with the secondary constructor it makes
// F# emit safe-initialization IL that Fable cannot compile. The one member that needs the instance
// uses a member-level self-identifier instead.
[<Sealed>]
type Schema<'model> internal (definition: SchemaDefinition<'model>, recordPlan: ICompiledRecordPlan<'model> option) =
    internal new(definition: SchemaDefinition<'model>) = Schema(definition, None)

    member internal _.Definition = definition
    member internal _.RecordPlan = recordPlan

    member internal this.ValueDefinition =
        match definition with
        | ValueDefinition value -> value
        | ModelDefinition model ->
            { Shape = NestedValueDefinition(ModelSchemaErasure.erase model, box this)
              Format = None
              Constraints = []
              Description = None
              Default = None }
        | PendingDefinition -> invalidOp "Expected a completed schema definition."

/// <summary>Functions for defining explicit tagged union schema cases.</summary>
[<RequireQualifiedAccess>]
module UnionCase =
    /// <summary>
    /// Describes one tagged union case from a tag, a payload constructor, a payload extractor, and a payload schema.
    /// </summary>
    /// <remarks>
    /// Union schemas are explicit and reflection-free. The constructor builds the union case after the payload parses,
    /// while the extractor lets validation and encoding-oriented interpreters identify the active case of an existing
    /// trusted union value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="tag" /> is empty or whitespace.</exception>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="tag" />, <paramref name="construct" />, <paramref name="tryPayload" />, or
    /// <paramref name="payload" /> is null.
    /// </exception>
    let create tag (construct: 'payload -> 'union) (tryPayload: 'union -> 'payload option) (payload: Schema<'payload>) : UnionCase<'union> =
        if isNull tag then
            nullArg (nameof tag)

        if isNull (box construct) then
            nullArg (nameof construct)

        if isNull (box tryPayload) then
            nullArg (nameof tryPayload)

        if isNull (box payload) then
            nullArg (nameof payload)

        UnionCase(
            { Tag = ExternalFieldName.create tag |> ExternalFieldName.value
              Payload = payload.ValueDefinition
              Construct = fun value -> value |> unbox<'payload> |> construct |> box
              TryInspect = fun value -> value |> unbox<'union> |> tryPayload |> Option.map box }
        )

/// <summary>Functions for defining explicit payload-less enum schema cases.</summary>
[<RequireQualifiedAccess>]
module EnumCase =
    /// <summary>Describes one payload-less enum case from a tag and the union case value it represents.</summary>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="tag" /> is empty or whitespace.</exception>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="tag" /> is null.</exception>
    let create tag (value: 'enum) : EnumCase<'enum> =
        if isNull tag then
            nullArg (nameof tag)

        EnumCase({ Tag = ExternalFieldName.create tag |> ExternalFieldName.value; Value = box value })

/// <summary>Functions for creating and inspecting value schemas.</summary>
[<RequireQualifiedAccess>]
module internal Value =
    let private primitive kind =
        Schema(ValueDefinition
            { Shape = PrimitiveValueDefinition kind
              Format = None
              Constraints = []
              Description = None

              Default = None }
        )

    /// <summary>Describes text represented as <see cref="T:System.String" />.</summary>
    let text : Schema<string> = primitive PrimitiveValueKind.Text

    /// <summary>Describes a 32-bit signed integer represented as <see cref="T:System.Int32" />.</summary>
    let ``int`` : Schema<int> = primitive PrimitiveValueKind.Int

    /// <summary>Describes a decimal number represented as <see cref="T:System.Decimal" />.</summary>
    let ``decimal`` : Schema<decimal> = primitive PrimitiveValueKind.Decimal

    /// <summary>Describes a Boolean value represented as <see cref="T:System.Boolean" />.</summary>
    let ``bool`` : Schema<bool> = primitive PrimitiveValueKind.Bool

#if NET8_0_OR_GREATER
    /// <summary>Describes a calendar date represented as <see cref="T:System.DateOnly" />.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    let date : Schema<DateOnly> = primitive PrimitiveValueKind.Date
#endif

    /// <summary>Describes an instant-like date and time represented as <see cref="T:System.DateTimeOffset" />.</summary>
    let dateTime : Schema<DateTimeOffset> = primitive PrimitiveValueKind.DateTime

    /// <summary>Describes a globally unique identifier represented as <see cref="T:System.Guid" />.</summary>
    let guid : Schema<Guid> = primitive PrimitiveValueKind.Guid

    /// <summary>Defers a nested model schema so the model can refer to itself.</summary>
    /// <remarks>
    /// The thunk is evaluated at most once. Use this only to close a recursive model graph; ordinary nested models
    /// should use <see cref="M:Axial.Schema.Schema.nested``1" /> because their schema is already available.
    /// </remarks>
    /// <param name="schema">A thunk returning the built schema when an interpreter reaches the recursive value.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null or returns null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when the thunk returns an unbuilt schema.</exception>
    /// <example>
    /// <code>
    /// let rec categorySchema () =
    ///     Schema.define&lt;Category&gt;
    ///     |&gt; field "name" _.Name
    ///     |&gt; fieldWith (Schema.listWith (Schema.defer categorySchema)) "children" _.Children
    ///     |&gt; construct (fun name children -&gt; { Name = name; Children = children })
    /// </code>
    /// </example>
    let lazyOf (schema: unit -> Schema<'model>) : Schema<'model> =
        if isNull (box schema) then nullArg (nameof schema)

        let definition =
            lazy
                (let modelSchema = schema ()
                 if isNull (box modelSchema) then nullArg (nameof schema)
                 match modelSchema.Definition with
                 | PendingDefinition -> invalidArg (nameof schema) "Expected the deferred function to return a built model schema."
                 | ValueDefinition _ -> invalidArg (nameof schema) "Expected the deferred function to return a built model schema, not a value schema."
                 | ModelDefinition model ->
                     { Shape = NestedValueDefinition(ModelSchemaErasure.erase model, box modelSchema)
                       Format = None
                       Constraints = []
                       Description = None
                       Default = None })

        Schema(ValueDefinition
            { Shape = LazyValueDefinition { Force = fun () -> definition.Value }
              Format = None
              Constraints = []
              Description = None
              Default = None }
        )

    /// <summary>Returns the intrinsic primitive kind for a primitive value schema.</summary>
    /// <remarks>
    /// This accessor is intentionally strict about the schema being primitive itself. Use
    /// <see cref="M:Axial.Schema.Schema.underlyingPrimitiveKind``1" /> to see through refinement layers to the primitive
    /// foundation of a refined value schema.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is a refined value schema.</exception>
    let primitiveKind (schema: Schema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.ValueDefinition.Shape with
        | PrimitiveValueDefinition kind -> kind
        | RefinedValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is a refined value schema."
        | NestedValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is a nested model value schema."
        | ManyValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is a collection value schema."
        | UnionValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is a union value schema."
        | UnionInlineValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is a union-inline value schema."
        | EnumValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is an enum value schema."
        | OptionValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is an optional value schema."
        | MapValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is a map value schema."
        | LazyValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is a deferred model value schema."

    /// <summary>
    /// Describes a named refined/domain value schema by pairing a raw value schema with a value-preserving
    /// construction function and an inspection function that recovers the raw representation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both functions are required, and this is deliberately the only way to author a refined value schema: a
    /// construct-only schema would hide refined values from inspection interpreters that read existing trusted models,
    /// and an inspect-only schema would leave construction interpreters unable to produce the refined value from
    /// parsed structured data.
    /// </para>
    /// <para>
    /// <paramref name="construct" /> is expected to run only after the raw value has already satisfied whatever
    /// checks or constraints an interpreter attaches to the raw value schema; it is not itself expected to fail.
    /// <paramref name="inspect" /> lets interpreters that only understand the raw representation, such as codecs,
    /// diagnostics, JSON Schema, UI, and documentation generators, still operate over the refined value.
    /// </para>
    /// <para>
    /// Refined value schemas built this way are portable metadata, matching primitive value schemas: they can be
    /// combined with <see cref="M:Axial.Schema.Schema.withConstraint``1" /> and used as the value schema for
    /// <c>Syntax.fieldWith</c> like any other <see cref="T:Axial.Schema.Schema`1" />.
    /// </para>
    /// <para>
    /// The everyday raw schema is a primitive value schema, especially <see cref="P:Axial.Schema.Schema.text" /> for
    /// domain values such as email addresses and names. The primitive foundation stays inspectable through the
    /// refinement: <see cref="M:Axial.Schema.Schema.underlyingPrimitiveKind``1" /> reports the intrinsic primitive kind
    /// beneath any number of refinement layers, and <see cref="M:Axial.Schema.Schema.rawConstraints``1" /> returns the
    /// constraint metadata carried by the raw schema, so interpreters can parse, render, and document the raw
    /// representation before constructing the refined value. Format metadata declared with
    /// <see cref="M:Axial.Schema.Schema.withFormat``1" /> stays visible the same way:
    /// <see cref="M:Axial.Schema.Schema.format``1" /> reports the nearest declared format through refinement layers.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="construct" />, <paramref name="inspect" />, or <paramref name="raw" /> is null.
    /// </exception>
    let refined (construct: 'raw -> 'value) (inspect: 'value -> 'raw) (raw: Schema<'raw>) : Schema<'value> =
        if isNull (box construct) then
            nullArg (nameof construct)

        if isNull (box inspect) then
            nullArg (nameof inspect)

        if isNull (box raw) then
            nullArg (nameof raw)

        let ops =
            RefinedValueOps(
                (fun value -> value |> unbox<'raw> |> construct |> box |> Ok),
                (fun value -> value |> unbox<'value> |> inspect |> box)
            )

        Schema(ValueDefinition
            { Shape = RefinedValueDefinition(raw.ValueDefinition, ops)
              Format = None
              Constraints = []
              Description = None

              Default = None }
        )

    let refine
        (construct: 'raw -> Result<'value, 'error>)
        (mapError: 'error -> SchemaError list)
        (inspect: 'value -> 'raw)
        (raw: Schema<'raw>)
        : Schema<'value> =
        if isNull (box construct) then nullArg (nameof construct)
        if isNull (box mapError) then nullArg (nameof mapError)
        if isNull (box inspect) then nullArg (nameof inspect)
        if isNull (box raw) then nullArg (nameof raw)

        let ops =
            RefinedValueOps(
                (fun value ->
                    match construct (unbox<'raw> value) with
                    | Ok refined -> Ok(box refined)
                    | Error error -> Error(mapError error)),
                (fun value -> value |> unbox<'value> |> inspect |> box)
            )

        Schema(ValueDefinition
            { Shape = RefinedValueDefinition(raw.ValueDefinition, ops)
              Format = None
              Constraints = []
              Description = None
              Default = None })

    /// <summary>Describes a nested model value from an already built nested model schema.</summary>
    /// <remarks>
    /// Nested value schemas let a field carry another trusted model, such as an address nested inside a customer.
    /// Interpreters that see through primitive and refined value schema layers, such as
    /// <see cref="M:Axial.Schema.Schema.underlyingPrimitiveKind``1" />, do not see through a nested value schema because a
    /// nested model has no underlying primitive representation of its own; interpreters that understand nested models,
    /// such as input parsing, inspect the nested model schema directly instead.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is not a completed model schema.</exception>
    let nested (schema: Schema<'nested>) : Schema<'nested> =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition with
        | PendingDefinition -> invalidArg (nameof schema) "Expected a built model schema."
        | ValueDefinition _ -> invalidArg (nameof schema) "Expected a built model schema, not a value schema."
        | ModelDefinition model ->
            Schema(ValueDefinition
                { Shape = NestedValueDefinition(ModelSchemaErasure.erase model, box schema)
                  Format = None
                  Constraints = []
                  Description = None

                  Default = None }
            )

    /// <summary>Describes a collection of values from an already built item value schema.</summary>
    /// <remarks>
    /// <para>
    /// <c>manyOf</c> is the general collection constructor. Use it when each item is a primitive, refined/domain value,
    /// nested model value, or another collection value schema. Collection-level constraints such as <c>minCount</c>
    /// attach to the returned schema; item-level constraints stay on <paramref name="itemSchema" /> and interpreters
    /// attach their diagnostics to item index paths.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="itemSchema" /> is null.</exception>
    let manyOf (itemSchema: Schema<'item>) : Schema<'item list> =
        if isNull (box itemSchema) then
            nullArg (nameof itemSchema)

        let boxItems (items: obj list) : obj = items |> List.map unbox<'item> |> box

        let acceptItem (interpreter: ICollectionItemInterpreter) =
            interpreter.Item<'item> itemSchema.ValueDefinition

        Schema(ValueDefinition
            { Shape =
                ManyValueDefinition
                    { Item = itemSchema.ValueDefinition
                      BoxItems = boxItems
                      AcceptItem = acceptItem }
              Format = None
              Constraints = []
              Description = None

              Default = None }
        )

    /// <summary>Describes a collection of nested model values from an already built item model schema.</summary>
    /// <remarks>
    /// Many value schemas let a field carry an ordered collection of another trusted model, such as a customer's
    /// contact methods. Interpreters that see through primitive and refined value schema layers, such as
    /// <see cref="M:Axial.Schema.Schema.underlyingPrimitiveKind``1" />, do not see through a many value schema because a
    /// collection has no underlying primitive representation of its own; interpreters that understand collections,
    /// such as input parsing, inspect the item model schema directly instead.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="itemSchema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="itemSchema" /> is not a completed model schema.</exception>
    let many (itemSchema: Schema<'item>) : Schema<'item list> =
        let itemValueSchema = nested itemSchema
        manyOf itemValueSchema

    /// <summary>Describes a JSON object as a dictionary from an already built item value schema.</summary>
    /// <remarks>
    /// Keys are always text: the object's field names become the map's keys, so there is no separate key schema.
    /// <c>Schema.mapWith</c> is the explicit dictionary constructor; each entry's value is described by
    /// <paramref name="itemSchema" />, and interpreters attach diagnostics to entry key paths.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="itemSchema" /> is null.</exception>
    let map (itemSchema: Schema<'item>) : Schema<Map<string, 'item>> =
        if isNull (box itemSchema) then
            nullArg (nameof itemSchema)

        let boxEntries (entries: (string * obj) list) : obj =
            entries |> List.map (fun (key, value) -> key, unbox<'item> value) |> Map.ofList |> box

        let entries (value: obj) : (string * obj) list =
            value |> unbox<Map<string, 'item>> |> Map.toList |> List.map (fun (key, item) -> key, box item)

        let acceptItem (interpreter: ICollectionItemInterpreter) =
            interpreter.Item<'item> itemSchema.ValueDefinition

        Schema(ValueDefinition
            { Shape =
                MapValueDefinition
                    { Item = itemSchema.ValueDefinition
                      BoxEntries = boxEntries
                      Entries = entries
                      AcceptItem = acceptItem }
              Format = None
              Constraints = []
              Description = None
              Default = None }
        )

    /// <summary>Adds a constraint to every item described by a list schema.</summary>
    let constrainItems (constraint': Constraint) (schema: Schema<'item list>) : Schema<'item list> =
        if isNull (box constraint') then nullArg (nameof constraint')
        if isNull (box schema) then nullArg (nameof schema)

        match schema.Definition with
        | ValueDefinition definition ->
            match definition.Shape with
            | ManyValueDefinition collection ->
                let item = { collection.Item with Constraints = collection.Item.Constraints @ [ constraint' ] }
                let acceptItem (interpreter: ICollectionItemInterpreter) = interpreter.Item<'item> item
                Schema(ValueDefinition { definition with Shape = ManyValueDefinition { collection with Item = item; AcceptItem = acceptItem } })
            | _ -> invalidArg (nameof schema) "Expected a list schema."
        | _ -> invalidArg (nameof schema) "Expected a list schema."

    /// <summary>Adds a constraint to every value described by a string-keyed map schema.</summary>
    let constrainValues (constraint': Constraint) (schema: Schema<Map<string, 'item>>) : Schema<Map<string, 'item>> =
        if isNull (box constraint') then nullArg (nameof constraint')
        if isNull (box schema) then nullArg (nameof schema)

        match schema.Definition with
        | ValueDefinition definition ->
            match definition.Shape with
            | MapValueDefinition collection ->
                let item = { collection.Item with Constraints = collection.Item.Constraints @ [ constraint' ] }
                let acceptItem (interpreter: ICollectionItemInterpreter) = interpreter.Item<'item> item
                Schema(ValueDefinition { definition with Shape = MapValueDefinition { collection with Item = item; AcceptItem = acceptItem } })
            | _ -> invalidArg (nameof schema) "Expected a map schema."
        | _ -> invalidArg (nameof schema) "Expected a map schema."

    /// <summary>
    /// Describes a tagged union value using explicit cases and object input with discriminator and payload fields.
    /// </summary>
    /// <remarks>
    /// Input interpreters expect an object with <paramref name="discriminatorField" /> containing the case tag and
    /// <paramref name="payloadField" /> containing the case payload, such as
    /// <c>{ type = "card"; value = { ... } }</c>. Payload schemas may be primitive, refined, nested model,
    /// collection, or another union value schema.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="discriminatorField" />, <paramref name="payloadField" />, or
    /// <paramref name="cases" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when a field name is empty, no cases are supplied, or case tags are duplicated.
    /// </exception>
    let union discriminatorField payloadField (cases: UnionCase<'union> list) : Schema<'union> =
        if isNull (box cases) then
            nullArg (nameof cases)

        cases
        |> List.iter (fun case ->
            if isNull (box case) then
                nullArg (nameof cases))

        if List.isEmpty cases then
            invalidArg (nameof cases) "Union schemas must contain at least one case."

        let tags = cases |> List.map (fun case -> case.Definition.Tag)
        let duplicates = tags |> List.countBy id |> List.filter (fun (_, count) -> count > 1)

        if not (List.isEmpty duplicates) then
            invalidArg (nameof cases) "Union case tags must be unique."

        Schema(ValueDefinition
            { Shape =
                UnionValueDefinition
                    { DiscriminatorField = ExternalFieldName.create discriminatorField
                      PayloadField = ExternalFieldName.create payloadField
                      Cases = cases |> List.map _.Definition }
              Format = None
              Constraints = []
              Description = None

              Default = None }
        )

    /// <summary>
    /// Describes a tagged union value using explicit cases whose payload fields are spliced beside the discriminator
    /// field in the same object, serde/zod style — for example <c>{ type = "card"; number = "..." }</c> instead of
    /// <c>Schema.union</c>'s externally-wrapped <c>{ type = "card"; value = { number = "..." } }</c>.
    /// </summary>
    /// <remarks>
    /// Every case payload must be built with <see cref="M:Axial.Schema.Schema.nested``1" /> so its field names are known
    /// upfront, and no payload field name may collide with <paramref name="discriminatorField" />; both are checked at
    /// construction time so the ambiguity can never reach input parsing or codec compilation.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="discriminatorField" /> or <paramref name="cases" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when a field name is empty, no cases are supplied, case tags are duplicated, a case payload is not a
    /// nested model value schema, or a case payload field name collides with <paramref name="discriminatorField" />.
    /// </exception>
    let unionInline discriminatorField (cases: UnionCase<'union> list) : Schema<'union> =
        if isNull (box cases) then
            nullArg (nameof cases)

        cases
        |> List.iter (fun case ->
            if isNull (box case) then
                nullArg (nameof cases))

        if List.isEmpty cases then
            invalidArg (nameof cases) "Union schemas must contain at least one case."

        let tags = cases |> List.map (fun case -> case.Definition.Tag)
        let duplicates = tags |> List.countBy id |> List.filter (fun (_, count) -> count > 1)

        if not (List.isEmpty duplicates) then
            invalidArg (nameof cases) "Union case tags must be unique."

        let discriminatorName = ExternalFieldName.create discriminatorField
        let discriminatorText = ExternalFieldName.value discriminatorName

        cases
        |> List.iter (fun case ->
            match case.Definition.Payload.Shape with
            | NestedValueDefinition(model, _) ->
                if model.Fields |> List.exists (fun field -> ExternalFieldName.value field.ExternalName = discriminatorText) then
                    invalidArg
                        (nameof cases)
                        (sprintf
                            "Union-inline case \"%s\" payload field names must not collide with the discriminator field \"%s\"."
                            case.Definition.Tag
                            discriminatorText)
            | PrimitiveValueDefinition _
            | RefinedValueDefinition _
            | ManyValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _
            | EnumValueDefinition _
            | OptionValueDefinition _
            | LazyValueDefinition _
            | MapValueDefinition _ ->
                invalidArg
                    (nameof cases)
                    (sprintf "Union-inline case \"%s\" payload must be an object schema built with Schema record." case.Definition.Tag))
        Schema(ValueDefinition
            { Shape =
                UnionInlineValueDefinition
                    { DiscriminatorField = discriminatorName
                      Cases = cases |> List.map _.Definition }
              Format = None
              Constraints = []
              Description = None

              Default = None }
        )

    /// <summary>Describes a bare-string enum value for payload-less union cases, lowering to JSON Schema <c>enum</c>.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="cases" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when no cases are supplied or case tags are duplicated.
    /// </exception>
    let enumOf (cases: EnumCase<'enum> list) : Schema<'enum> =
        if isNull (box cases) then
            nullArg (nameof cases)

        cases
        |> List.iter (fun case ->
            if isNull (box case) then
                nullArg (nameof cases))

        if List.isEmpty cases then
            invalidArg (nameof cases) "Enum schemas must contain at least one case."

        let tags = cases |> List.map (fun case -> case.Definition.Tag)
        let duplicates = tags |> List.countBy id |> List.filter (fun (_, count) -> count > 1)

        if not (List.isEmpty duplicates) then
            invalidArg (nameof cases) "Enum case tags must be unique."

        Schema(ValueDefinition
            { Shape = EnumValueDefinition { Cases = cases |> List.map _.Definition }
              Format = None
              Constraints = []
              Description = None

              Default = None }
        )

    /// <summary>Describes an optional value so <c>'field option</c> models are schema-describable.</summary>
    /// <remarks>
    /// <para>
    /// Optional value schemas make absence a legal parse result rather than a diagnostic: input parsing maps missing
    /// or null structured data to <c>None</c> and parses present input through <paramref name="payload" /> into <c>Some</c>,
    /// with the payload schema's constraints running on the payload. Codecs decode an absent or <c>null</c> JSON field
    /// to <c>None</c> and omit <c>None</c> fields when encoding, and JSON Schema generation leaves optional fields out
    /// of the object's <c>required</c> list.
    /// </para>
    /// <para>
    /// Optionality is a single boundary layer, not a nestable wrapper: <c>optionOf (optionOf ...)</c> is rejected
    /// because absent input could not distinguish <c>None</c> from <c>Some None</c>. Combining <c>optionOf</c> with
    /// the <c>required</c> constraint is contradictory and is rejected here when the payload carries it, by
    /// <c>Schema.withConstraint</c> when attached to the optional schema itself, and when a shape is closed when
    /// attached at the field level.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="payload" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="payload" /> is itself an optional value schema or carries the <c>required</c>
    /// constraint on any layer.
    /// </exception>
    let optionOf (payload: Schema<'value>) : Schema<'value option> =
        if isNull (box payload) then
            nullArg (nameof payload)

        match payload.ValueDefinition.Shape with
        | OptionValueDefinition _ ->
            invalidArg (nameof payload) "Optional value schemas cannot be nested inside another optional value schema."
        | PrimitiveValueDefinition _
        | RefinedValueDefinition _
        | NestedValueDefinition _
        | ManyValueDefinition _
        | UnionValueDefinition _
        | UnionInlineValueDefinition _
        | EnumValueDefinition _
        | MapValueDefinition _ -> ()
        | LazyValueDefinition _ -> ()

        let rec carriesRequired (definition: ValueSchemaDefinition) =
            let ownRequired =
                definition.Constraints
                |> List.exists (fun constraint' -> constraint'.Code = "required")

            ownRequired
            || match definition.Shape with
               | RefinedValueDefinition(raw, _) -> carriesRequired raw
               | PrimitiveValueDefinition _
               | NestedValueDefinition _
               | ManyValueDefinition _
               | UnionValueDefinition _
               | UnionInlineValueDefinition _
               | EnumValueDefinition _
               | OptionValueDefinition _
               | MapValueDefinition _ -> false
               | LazyValueDefinition deferred -> carriesRequired (deferred.Force())

        if carriesRequired payload.ValueDefinition then
            invalidArg (nameof payload) "Optional value schemas cannot carry the required constraint."

        Schema(ValueDefinition
            { Shape =
                OptionValueDefinition
                    { Payload = payload.ValueDefinition
                      WrapSome = fun value -> value |> unbox<'value> |> Some |> box
                      NoneValue = box (None: 'value option)
                      TryUnwrap = fun value -> value |> unbox<'value option> |> Option.map box }
              Format = None
              Constraints = []
              Description = None

              Default = None }
        )

    /// <summary>Returns whether a value schema is a refined/domain value schema.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let isRefined (schema: Schema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.ValueDefinition.Shape with
        | RefinedValueDefinition _ -> true
        | PrimitiveValueDefinition _ -> false
        | NestedValueDefinition _ -> false
        | ManyValueDefinition _ -> false
        | UnionValueDefinition _ -> false
        | UnionInlineValueDefinition _ -> false
        | EnumValueDefinition _ -> false
        | OptionValueDefinition _ -> false
        | MapValueDefinition _ -> false
        | LazyValueDefinition _ -> false

    /// <summary>Returns the intrinsic primitive kind beneath any refinement layers of a value schema.</summary>
    /// <remarks>
    /// Every value schema bottoms out on a primitive value schema, so this accessor is total: it returns the kind of a
    /// primitive value schema directly and walks the raw schemas of refined value schemas, including refined values
    /// layered over other refined values, until it reaches the primitive foundation. Interpreters that only understand
    /// raw representations, such as input parsers, codecs, JSON Schema emitters, UI renderers, and documentation
    /// generators, use this to treat a refined value like its primitive representation at the boundary.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let underlyingPrimitiveKind (schema: Schema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        let rec kindOf (definition: ValueSchemaDefinition) =
            match definition.Shape with
            | PrimitiveValueDefinition kind -> kind
            | RefinedValueDefinition(raw, _) -> kindOf raw
            | NestedValueDefinition _ ->
                invalidArg (nameof schema) "Nested model value schemas have no underlying primitive kind."
            | ManyValueDefinition _ ->
                invalidArg (nameof schema) "Collection value schemas have no underlying primitive kind."
            | UnionValueDefinition _ ->
                invalidArg (nameof schema) "Union value schemas have no underlying primitive kind."
            | UnionInlineValueDefinition _ ->
                invalidArg (nameof schema) "Union-inline value schemas have no underlying primitive kind."
            | EnumValueDefinition _ ->
                invalidArg (nameof schema) "Enum value schemas have no underlying primitive kind."
            | OptionValueDefinition _ ->
                invalidArg (nameof schema) "Optional value schemas have no underlying primitive kind."
            | MapValueDefinition _ ->
                invalidArg (nameof schema) "Map value schemas have no underlying primitive kind."
            | LazyValueDefinition _ ->
                invalidArg (nameof schema) "Deferred model value schemas have no underlying primitive kind."

        kindOf schema.ValueDefinition

    /// <summary>Returns the constraint metadata carried by the raw value schema of a refined value schema.</summary>
    /// <remarks>
    /// Raw constraints describe the underlying representation that boundary interpreters see before the refined value
    /// is constructed, such as length bounds on the raw text of an email address. They are retained separately from
    /// constraints attached to the refined value schema itself with
    /// <see cref="M:Axial.Schema.Schema.withConstraint``1" />, which
    /// <see cref="M:Axial.Schema.Schema.constraints``1" /> continues to return.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is a primitive value schema.</exception>
    let rawConstraints (schema: Schema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.ValueDefinition.Shape with
        | RefinedValueDefinition(raw, _) -> raw.Constraints
        | PrimitiveValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is a primitive value schema."
        | NestedValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is a nested model value schema."
        | ManyValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is a collection value schema."
        | UnionValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is a union value schema."
        | UnionInlineValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is a union-inline value schema."
        | EnumValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is an enum value schema."
        | OptionValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is an optional value schema."
        | MapValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is a map value schema."
        | LazyValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is a deferred model value schema."

    let private underlyingClrType kind =
        match kind with
        | PrimitiveValueKind.Text -> typeof<string>
        | PrimitiveValueKind.Int -> typeof<int>
        | PrimitiveValueKind.Decimal -> typeof<decimal>
        | PrimitiveValueKind.Bool -> typeof<bool>
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date -> typeof<DateOnly>
#else
        | PrimitiveValueKind.Date ->
            invalidOp "Calendar date value schemas are not available on this target framework."
#endif
        | PrimitiveValueKind.DateTime -> typeof<DateTimeOffset>
        | PrimitiveValueKind.Guid -> typeof<Guid>

    /// <summary>
    /// Returns a function that projects a trusted value through all refinement layers to its underlying primitive
    /// representation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Like <see cref="M:Axial.Schema.Schema.underlyingPrimitiveKind``1" />, this accessor is total over value schemas:
    /// for a primitive value schema the projection returns the value itself, and for a refined value schema it applies
    /// the inspection function of every refinement layer, including refined values layered over other refined values,
    /// until it reaches the primitive foundation. Interpreters use it to observe the raw representation of an already
    /// trusted refined value — running executable value checks, encoding through codecs, producing diagnostics, and
    /// redisplaying values — without reflection and without access to the refined type's internals.
    /// </para>
    /// <para>
    /// The requested projection type is validated eagerly against the schema's underlying primitive kind, so a
    /// mismatched projection fails when the projection is created rather than on each projected value.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the requested projection type does not match the schema's underlying primitive kind.
    /// </exception>
    let inspectUnderlying<'value, 'primitive> (schema: Schema<'value>) : 'value -> 'primitive =
        if isNull (box schema) then
            nullArg (nameof schema)

        // Both targets validate that the schema is primitive-backed; the eager projection-type check is .NET-only
        // because Fable erases generics at runtime. The projection itself stays reflection-free on both targets.
        let kind = underlyingPrimitiveKind schema
        Platform.checkUnderlyingProjection<'primitive> (fun () -> underlyingClrType kind) (nameof schema)

        let rec project (definition: ValueSchemaDefinition) (value: obj) =
            match definition.Shape with
            | PrimitiveValueDefinition _ -> value
            | RefinedValueDefinition(raw, ops) -> project raw (ops.Inspect value)
            | NestedValueDefinition _ ->
                invalidArg (nameof schema) "Nested model value schemas have no underlying primitive representation."
            | ManyValueDefinition _ ->
                invalidArg (nameof schema) "Collection value schemas have no underlying primitive representation."
            | UnionValueDefinition _ ->
                invalidArg (nameof schema) "Union value schemas have no underlying primitive representation."
            | UnionInlineValueDefinition _ ->
                invalidArg (nameof schema) "Union-inline value schemas have no underlying primitive representation."
            | EnumValueDefinition _ ->
                invalidArg (nameof schema) "Enum value schemas have no underlying primitive representation."
            | OptionValueDefinition _ ->
                invalidArg (nameof schema) "Optional value schemas have no underlying primitive representation."
            | MapValueDefinition _ ->
                invalidArg (nameof schema) "Map value schemas have no underlying primitive representation."
            | LazyValueDefinition _ ->
                invalidArg (nameof schema) "Deferred model value schemas have no underlying primitive representation."

        fun value -> project schema.ValueDefinition (box value) |> unbox<'primitive>

    /// <summary>Returns a value schema carrying the supplied portable format metadata.</summary>
    /// <remarks>
    /// <para>
    /// The format names the boundary-facing interpretation of the value, such as
    /// <see cref="P:Axial.Schema.SchemaFormat.email" /> for a refined email address over
    /// <see cref="P:Axial.Schema.Schema.text" />. It is annotation metadata for interpreters — JSON Schema emitters,
    /// UI renderers, and documentation generators — and attaches no executable check; pair it with constraint
    /// metadata such as <see cref="P:Axial.Schema.Constraint.email" /> when validation is also required.
    /// </para>
    /// <para>
    /// A value schema carries at most one format. Applying <c>withFormat</c> again replaces the earlier declaration.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="format" /> carries no name.</exception>
    let withFormat (format: SchemaFormat) (schema: Schema<'value>) : Schema<'value> =
        if String.IsNullOrWhiteSpace format.Name then
            invalidArg (nameof format) "Schema format names must not be empty or whitespace."

        if isNull (box schema) then
            nullArg (nameof schema)

        Schema(ValueDefinition
            { schema.ValueDefinition with
                Format = Some format }
        )

    /// <summary>Returns the portable format metadata declared nearest to a value schema, when present.</summary>
    /// <remarks>
    /// Like <see cref="M:Axial.Schema.Schema.underlyingPrimitiveKind``1" />, this accessor sees through refinement
    /// layers: a format declared on the refined value schema itself wins, and otherwise the raw value schemas are
    /// walked toward the primitive foundation until a declaration is found. A format declared on the raw text of a
    /// refined email address therefore stays visible on the refined schema, while an outer declaration overrides it.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let format (schema: Schema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        let rec formatOf (definition: ValueSchemaDefinition) =
            match definition.Format with
            | Some _ as declared -> declared
            | None ->
                match definition.Shape with
                | RefinedValueDefinition(raw, _) -> formatOf raw
                | PrimitiveValueDefinition _ -> None
                | NestedValueDefinition _ -> None
                | ManyValueDefinition _ -> None
                | UnionValueDefinition _ -> None
                | UnionInlineValueDefinition _ -> None
                | EnumValueDefinition _ -> None
                | OptionValueDefinition _ -> None
                | MapValueDefinition _ -> None
                | LazyValueDefinition deferred -> formatOf (deferred.Force())

        formatOf schema.ValueDefinition

    /// <summary>Returns a value schema carrying the supplied description metadata.</summary>
    /// <remarks>
    /// <para>
    /// The description is annotation metadata for interpreters: JSON Schema generation lowers it to the
    /// <c>description</c> keyword at the point the value schema is used, whether as a standalone value schema or as a
    /// model field. It attaches no executable check.
    /// </para>
    /// <para>
    /// A value schema carries at most one description. Applying <c>describe</c> again replaces the earlier
    /// declaration.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="text" /> is null, empty, or whitespace.</exception>
    let describe (text: string) (schema: Schema<'value>) : Schema<'value> =
        if String.IsNullOrWhiteSpace text then
            invalidArg (nameof text) "Descriptions must not be empty or whitespace."

        if isNull (box schema) then
            nullArg (nameof schema)

        Schema(ValueDefinition
            { schema.ValueDefinition with
                Description = Some text }
        )

    /// <summary>Returns the description metadata declared nearest to a value schema, when present.</summary>
    /// <remarks>
    /// Like <see cref="M:Axial.Schema.Schema.format``1" />, this accessor sees through refinement layers: a description
    /// declared on the refined value schema itself wins, and otherwise the raw value schemas are walked toward the
    /// primitive foundation until a declaration is found.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let description (schema: Schema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        let rec descriptionOf (definition: ValueSchemaDefinition) =
            match definition.Description with
            | Some _ as declared -> declared
            | None ->
                match definition.Shape with
                | RefinedValueDefinition(raw, _) -> descriptionOf raw
                | PrimitiveValueDefinition _ -> None
                | NestedValueDefinition _ -> None
                | ManyValueDefinition _ -> None
                | UnionValueDefinition _ -> None
                | UnionInlineValueDefinition _ -> None
                | EnumValueDefinition _ -> None
                | OptionValueDefinition _ -> None
                | MapValueDefinition _ -> None
                | LazyValueDefinition deferred -> descriptionOf (deferred.Force())

        descriptionOf schema.ValueDefinition

    /// <summary>Returns a value schema carrying the supplied default-value metadata.</summary>
    /// <remarks>
    /// <para>
    /// The default is annotation metadata for interpreters: JSON Schema generation lowers it to the <c>default</c>
    /// keyword at the point the value schema is used. It attaches no executable check and does not affect parsing —
    /// missing input is still a diagnostic unless the value schema is also wrapped with <c>Schema.optionOf</c>.
    /// </para>
    /// <para>
    /// A value schema carries at most one default. Applying <c>withDefault</c> again replaces the earlier
    /// declaration.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let withDefault (value: 'value) (schema: Schema<'value>) : Schema<'value> =
        if isNull (box schema) then
            nullArg (nameof schema)

        Schema(ValueDefinition
            { schema.ValueDefinition with
                Default = Some(box value) }
        )

    /// <summary>Returns the default-value metadata declared nearest to a value schema, when present.</summary>
    /// <remarks>
    /// Like <see cref="M:Axial.Schema.Schema.format``1" />, this accessor sees through refinement layers: a default
    /// declared on the refined value schema itself wins, and otherwise the raw value schemas are walked toward the
    /// primitive foundation until a declaration is found.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let defaultValue (schema: Schema<'value>) : 'value option =
        if isNull (box schema) then
            nullArg (nameof schema)

        let rec defaultOf (definition: ValueSchemaDefinition) =
            match definition.Default with
            | Some _ as declared -> declared
            | None ->
                match definition.Shape with
                | RefinedValueDefinition(raw, _) -> defaultOf raw
                | PrimitiveValueDefinition _ -> None
                | NestedValueDefinition _ -> None
                | ManyValueDefinition _ -> None
                | UnionValueDefinition _ -> None
                | UnionInlineValueDefinition _ -> None
                | EnumValueDefinition _ -> None
                | OptionValueDefinition _ -> None
                | MapValueDefinition _ -> None
                | LazyValueDefinition deferred -> defaultOf (deferred.Force())

        defaultOf schema.ValueDefinition |> Option.map unbox<'value>

    /// <summary>Returns the portable constraint metadata attached to a value schema.</summary>
    let constraints (schema: Schema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        schema.ValueDefinition.Constraints

    /// <summary>Returns the portable constraint metadata carried by every layer of a value schema.</summary>
    /// <remarks>
    /// <para>
    /// Constraints are returned foundation-first: the primitive foundation's constraints come first, then each
    /// refinement layer outward, ending with the constraints attached to the schema itself. This matches authoring
    /// order, where raw constraints are declared before the schema is refined. For a primitive value schema the
    /// result equals <see cref="M:Axial.Schema.Schema.constraints``1" />.
    /// </para>
    /// <para>
    /// Interpreters that lower a value schema's complete constraint metadata to one executable program — such as a
    /// check over the underlying primitive representation obtained with
    /// <see cref="M:Axial.Schema.Schema.inspectUnderlying``2" /> — use this accessor so constraints declared on raw
    /// layers and on the refined schema are honored together. Per-layer inspection remains available through
    /// <see cref="M:Axial.Schema.Schema.constraints``1" /> and <see cref="M:Axial.Schema.Schema.rawConstraints``1" />.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let allConstraints (schema: Schema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        let rec gather (definition: ValueSchemaDefinition) =
            match definition.Shape with
            | PrimitiveValueDefinition _ -> definition.Constraints
            | RefinedValueDefinition(raw, _) -> gather raw @ definition.Constraints
            | NestedValueDefinition _ -> definition.Constraints
            | ManyValueDefinition _ -> definition.Constraints
            | UnionValueDefinition _ -> definition.Constraints
            | UnionInlineValueDefinition _ -> definition.Constraints
            | EnumValueDefinition _ -> definition.Constraints
            | OptionValueDefinition _ -> definition.Constraints
            | MapValueDefinition _ -> definition.Constraints
            | LazyValueDefinition deferred -> gather (deferred.Force()) @ definition.Constraints

        gather schema.ValueDefinition

    let private ensureNotRequiredOnOptional parameterName (constraint': Constraint) (schema: Schema<'value>) =
        match schema.ValueDefinition.Shape with
        | OptionValueDefinition _ when constraint'.Code = "required" ->
            invalidArg parameterName "Optional value schemas cannot carry the required constraint."
        | _ -> ()

    /// <summary>Returns a value schema with additional portable constraint metadata appended in declaration order.</summary>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the <c>required</c> constraint is attached to an optional value schema.
    /// </exception>
    let withConstraint (constraint': Constraint) (schema: Schema<'value>) : Schema<'value> =
        if isNull constraint' then
            nullArg (nameof constraint')

        if isNull (box schema) then
            nullArg (nameof schema)

        ensureNotRequiredOnOptional (nameof constraint') constraint' schema

        Schema(ValueDefinition
            { schema.ValueDefinition with
                Constraints = schema.ValueDefinition.Constraints @ [ constraint' ] }
        )

    /// <summary>Returns a value schema with additional portable constraint metadata appended in declaration order.</summary>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the <c>required</c> constraint is attached to an optional value schema.
    /// </exception>
    let withConstraints (constraints: Constraint list) (schema: Schema<'value>) : Schema<'value> =
        if isNull (box constraints) then
            nullArg (nameof constraints)

        constraints
        |> List.iter (fun constraint' ->
            if isNull constraint' then
                nullArg (nameof constraints))

        if isNull (box schema) then
            nullArg (nameof schema)

        constraints
        |> List.iter (fun constraint' -> ensureNotRequiredOnOptional (nameof constraints) constraint' schema)

        Schema(ValueDefinition
            { schema.ValueDefinition with
                Constraints = schema.ValueDefinition.Constraints @ constraints }
        )

module internal FieldDescriptorOps =
    let fromField (field: Field<'model, 'value>) : FieldDescriptor<'model> =
        if isNull (box field) then
            nullArg (nameof field)

        { FieldDescriptor.ExternalName = field.Definition.ExternalName
          Order = field.Definition.Order
          Getter = fun model -> field.Definition.Getter model |> box
          ValueSchema = field.Definition.ValueSchema
          Constraints = field.Definition.Constraints }

    let fromOrderedField order (field: Field<'model, 'value>) : FieldDescriptor<'model> =
        if isNull (box field) then
            nullArg (nameof field)

        { FieldDescriptor.ExternalName = field.Definition.ExternalName
          Order = FieldOrder.create order
          Getter = fun model -> field.Definition.Getter model |> box
          ValueSchema = field.Definition.ValueSchema
          Constraints = field.Definition.Constraints }

/// <summary>Functions for inspecting schema field metadata.</summary>
[<RequireQualifiedAccess>]
module Field =
    /// <summary>
    /// Creates a standalone typed schema field from a boundary-facing name, a trusted-model getter, and a trusted
    /// value schema.
    /// </summary>
    /// <remarks>
    /// Standalone fields are useful for advanced composition and tests that need to inspect a <c>Field</c> value
    /// directly. Ordinary object schemas use <c>Schema.define&lt;'model&gt;</c>, <c>Syntax.field</c> or
    /// <c>Syntax.fieldWith</c>, and a constructor-last closing operation.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="externalName" />, <paramref name="getter" />, or <paramref name="value" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="externalName" /> is empty or contains only whitespace.
    /// </exception>
    let create externalName (getter: 'model -> 'value) (value: Schema<'value>) : Field<'model, 'value> =
        if isNull (box getter) then
            nullArg (nameof getter)

        if isNull (box value) then
            nullArg (nameof value)

        Field(
            { ExternalName = ExternalFieldName.create externalName
              Order = FieldOrder.create 0
              Getter = getter
              ValueSchema = value.ValueDefinition
              Constraints = [] }
        )

    /// <summary>Returns the boundary-facing name for a schema field.</summary>
    let externalName (field: Field<'model, 'value>) =
        if isNull (box field) then
            nullArg (nameof field)

        field.Definition.ExternalName

    /// <summary>Returns the zero-based field order used for trusted construction and ordered interpreter output.</summary>
    let order (field: Field<'model, 'value>) =
        if isNull (box field) then
            nullArg (nameof field)

        field.Definition.Order

    /// <summary>Reads a schema field value from an existing trusted model.</summary>
    let getValue (field: Field<'model, 'value>) (model: 'model) =
        if isNull (box field) then
            nullArg (nameof field)

        field.Definition.Getter model

    /// <summary>Returns the portable constraint metadata attached to a schema field.</summary>
    let constraints (field: Field<'model, 'value>) =
        if isNull (box field) then
            nullArg (nameof field)

        field.Definition.Constraints

    /// <summary>Returns a schema field with additional portable constraint metadata appended in declaration order.</summary>
    let withConstraint (constraint': Constraint) (field: Field<'model, 'value>) =
        if isNull constraint' then
            nullArg (nameof constraint')

        if isNull (box field) then
            nullArg (nameof field)

        Field(
            { field.Definition with
                Constraints = field.Definition.Constraints @ [ constraint' ] }
        )

    /// <summary>Returns a schema field with additional portable constraint metadata appended in declaration order.</summary>
    let withConstraints (constraints: Constraint list) (field: Field<'model, 'value>) =
        if isNull (box constraints) then
            nullArg (nameof constraints)

        constraints
        |> List.iter (fun constraint' ->
            if isNull constraint' then
                nullArg (nameof constraints))

        if isNull (box field) then
            nullArg (nameof field)

        Field(
            { field.Definition with
                Constraints = field.Definition.Constraints @ constraints }
        )

/// <summary>Functions for creating and inspecting model schemas.</summary>
[<RequireQualifiedAccess>]
module internal SchemaCore =
    /// <summary>Describes text.</summary>
    let text = Value.text
    /// <summary>Describes a 32-bit signed integer.</summary>
    let ``int`` = Value.``int``
    /// <summary>Describes a decimal number.</summary>
    let ``decimal`` = Value.``decimal``
    /// <summary>Describes a Boolean value.</summary>
    let ``bool`` = Value.``bool``
#if NET8_0_OR_GREATER
    /// <summary>Describes a calendar date.</summary>
    let date = Value.date
#endif
    /// <summary>Describes a date and time with an offset.</summary>
    let dateTime = Value.dateTime
    /// <summary>Describes a globally unique identifier.</summary>
    let guid = Value.guid
    /// <summary>Describes a list whose items use the supplied schema.</summary>
    let listWith item = Value.manyOf item
    /// <summary>Describes an optional value.</summary>
    let option item = Value.optionOf item
    /// <summary>Describes a string-keyed map whose values use the supplied schema.</summary>
    let mapWith item = Value.map item
    let constrainItems constraint' schema = Value.constrainItems constraint' schema
    let constrainValues constraint' schema = Value.constrainValues constraint' schema
    /// <summary>Defers a recursive schema.</summary>
    let defer schema = Value.lazyOf schema
    /// <summary>Converts a schema through total construction and inspection functions.</summary>
    let convert construct inspect schema = Value.refined construct inspect schema
    let refine construct mapError inspect schema = Value.refine construct mapError inspect schema
    /// <summary>Describes a tagged union.</summary>
    let union discriminator payload cases = Value.union discriminator payload cases
    /// <summary>Describes an internally tagged union.</summary>
    let inlineUnion discriminator cases = Value.unionInline discriminator cases
    /// <summary>Describes a payload-less tagged enum.</summary>
    let enum cases = Value.enumOf cases
    /// <summary>Appends one portable constraint.</summary>
    let constrain constraint' schema = Value.withConstraint constraint' schema
    /// <summary>Appends portable constraints in declaration order.</summary>
    let constrainAll constraints schema = Value.withConstraints constraints schema
    /// <summary>Attaches boundary format metadata.</summary>
    let withFormat format schema = Value.withFormat format schema
    /// <summary>Attaches descriptive metadata.</summary>
    /// <summary>Attaches a default value.</summary>
    let withDefault value schema = Value.withDefault value schema
    let format schema = Value.format schema
    let description schema = Value.description schema
    let defaultValue schema = Value.defaultValue schema
    let constraints schema = Value.constraints schema
    let isRefined schema = Value.isRefined schema
    let primitiveKind schema = Value.primitiveKind schema
    let underlyingPrimitiveKind schema = Value.underlyingPrimitiveKind schema
    let rawConstraints schema = Value.rawConstraints schema
    let internal inspectUnderlying<'value, 'primitive> schema = Value.inspectUnderlying<'value, 'primitive> schema
    let internal allConstraints schema = Value.allConstraints schema
    /// <summary>
    /// Closes a structural shape whose constructor has been fully applied by its fields.
    /// </summary>
    /// <remarks>
    /// This is the arity-independent schema construction path. It preserves the type-erased model definition
    /// for metadata interpreters while deriving constructor application and field ordering from the typed shape,
    /// without adding more fixed-arity <c>Schema.mapN</c> helpers.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="closure" /> is null.</exception>
    let closeTotal (closure: ShapeClosure<'model, 'constructor, 'model, 'chain>) : Schema<'model> =
        if isNull (box closure) then
            nullArg (nameof closure)

        let fieldsShape = closure.Fields :> IShapeFields<'model, 'constructor, 'model>
        let fields, count =
            let fields, count = fieldsShape.GetFields 0
            fields |> List.map unbox<FieldDescriptor<'model>>, count

        let constructor =
            { ConstructorApplication.ArgumentCount = count
              ApplyTrusted =
                fun arguments ->
                    ConstructorApplication.ensureArgumentCount count arguments
                    fieldsShape.Apply(box closure.Constructor, arguments) |> unbox<'model>
              TryApplyTrusted =
                fun arguments ->
                    ConstructorApplication.ensureArgumentCount count arguments
                    fieldsShape.Apply(box closure.Constructor, arguments) |> unbox<'model> |> Ok }

        let specialization =
            CompiledRecordPlan<'model, 'constructor, 'model, 'chain>(closure.Constructor, closure.Fields, Ok)
            :> ICompiledRecordPlan<'model>

        Schema(ModelDefinition(ModelSchemaDefinition.create constructor fields), Some specialization)

    /// <summary>
    /// Closes a structural shape whose constructor returns
    /// <c>Result&lt;'model, 'error&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Use this when field values can parse successfully but the trusted model constructor still enforces intrinsic
    /// cross-field invariants. The supplied <paramref name="errorMessage" /> function maps constructor errors to a
    /// portable message that schema interpreters can report without making <c>Axial.Schema</c> depend on diagnostics.
    /// Schema input interpreters report constructor errors only after every field has parsed and passed intrinsic
    /// field constraints; field diagnostics gate constructor application rather than composing with constructor
    /// diagnostics from partially trusted values.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="errorMessage" /> or <paramref name="closure" /> is null.
    /// </exception>
    let closeResultWith
        (errorMessage: 'error -> string)
        (closure: ShapeClosure<'model, 'constructor, Result<'model, 'error>, 'chain>)
        : Schema<'model> =
        if isNull (box errorMessage) then
            nullArg (nameof errorMessage)

        if isNull (box closure) then
            nullArg (nameof closure)

        let fieldsShape = closure.Fields :> IShapeFields<'model, 'constructor, Result<'model, 'error>>
        let fields, count =
            let fields, count = fieldsShape.GetFields 0
            fields |> List.map unbox<FieldDescriptor<'model>>, count

        let tryApply arguments =
            ConstructorApplication.ensureArgumentCount count arguments

            match fieldsShape.Apply(box closure.Constructor, arguments) |> unbox<Result<'model, 'error>> with
            | Ok model -> Ok model
            | Error error -> Error(errorMessage error)

        let constructor =
            { ConstructorApplication.ArgumentCount = count
              ApplyTrusted =
                fun arguments ->
                    match tryApply arguments with
                    | Ok model -> model
                    | Error message -> invalidOp message
              TryApplyTrusted = tryApply }

        let specialization =
            CompiledRecordPlan<'model, 'constructor, Result<'model, 'error>, 'chain>(
                closure.Constructor,
                closure.Fields,
                Result.mapError errorMessage
            )
            :> ICompiledRecordPlan<'model>

        Schema(ModelDefinition(ModelSchemaDefinition.create constructor fields), Some specialization)

    /// <summary>
    /// Closes a structural shape whose constructor returns
    /// <c>Result&lt;'model, string&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Constructor errors must already be rendered as user-facing intrinsic-invariant messages.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="closure" /> is null.</exception>
    let closeResult
        (closure: ShapeClosure<'model, 'constructor, Result<'model, string>, 'chain>)
        : Schema<'model> =
        closeResultWith id closure

    /// <summary>
    /// Compiles a built model schema's retained typed shape into an interpreter-specific record plan.
    /// </summary>
    /// <remarks>
    /// This is the constructor-specialized companion to the type-erased schema metadata exposed through ordinary
    /// schema inspection. It is intended for interpreters such as codecs that need to compile direct record plans from
    /// a <c>Schema&lt;'model&gt;</c> value without asking callers to re-supply the constructor or typed fields.
    /// Schemas closed with <c>Syntax.construct</c> or <c>Syntax.constructResult</c> carry this typed view.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="factory" /> or
    /// <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when the schema has no retained typed record plan.</exception>
    let compilePlan (factory: IRecordPlanCompiler<'model, 'result>) (schema: Schema<'model>) : 'result =
        if isNull (box factory) then
            nullArg (nameof factory)

        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.RecordPlan with
        | Some specialization -> specialization.CompilePlan factory
        | None -> invalidArg (nameof schema) "The schema does not carry a typed record plan."

    /// <summary>Returns a built model schema carrying the supplied description metadata.</summary>
    /// <remarks>
    /// The description is annotation metadata for interpreters: JSON Schema generation lowers it to the document's
    /// root <c>title</c> keyword. It attaches no executable check. A model schema carries at most one description;
    /// applying <c>describe</c> again replaces the earlier declaration.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="text" /> is null, empty, or whitespace, or when <paramref name="schema" /> was not
    /// a completed model schema.
    /// </exception>
    let describe (text: string) (schema: Schema<'model>) : Schema<'model> =
        if String.IsNullOrWhiteSpace text then
            invalidArg (nameof text) "Descriptions must not be empty or whitespace."

        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition with
        | PendingDefinition -> invalidArg (nameof schema) "Expected a built model schema."
        | ModelDefinition definition ->
            Schema(ModelDefinition { definition with Description = Some text }, schema.RecordPlan)
        | ValueDefinition _ -> Value.describe text schema
