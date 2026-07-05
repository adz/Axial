namespace Axial.Schema

open System
open System.Collections.Generic

/// <summary>
/// Represents the source-facing name of a schema field.
/// </summary>
/// <remarks>
/// <para>
/// External field names are the names interpreters use at data boundaries, such as raw input keys, JSON property names,
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
#if FABLE_COMPILER
            invalidArg (nameof value) "Field order must be zero or greater."
#else
            raise (ArgumentOutOfRangeException(nameof value, value, "Field order must be zero or greater."))
#endif

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
type SchemaConstraintMetadata =
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
    /// <summary>A text value must match the supplied regular expression pattern.</summary>
    | Pattern of pattern: string
    /// <summary>A text value must equal one of the supplied choices.</summary>
    | OneOf of choices: string list
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
type SchemaConstraint internal (
    code: string,
    metadata: SchemaConstraintMetadata,
    arguments: IReadOnlyDictionary<string, obj>,
    message: string option
) =
    /// <summary>Gets the stable interpreter-facing constraint code.</summary>
    member _.Code = code

    /// <summary>Gets the typed interpreter-facing constraint metadata.</summary>
    member _.Metadata = metadata

    /// <summary>Gets the structured constraint arguments keyed by stable interpreter-facing names.</summary>
    member _.Arguments = arguments

    /// <summary>Gets the author-supplied message override, when one was attached with <see cref="M:Axial.Schema.SchemaConstraint.withMessage" />.</summary>
    member _.Message = message

    override _.ToString() = code

/// <summary>Functions for creating and inspecting schema constraint metadata.</summary>
[<RequireQualifiedAccess>]
module SchemaConstraint =
    let private ensureName parameterName (value: string) =
        if isNull value then
            nullArg parameterName

        if String.IsNullOrWhiteSpace value then
            invalidArg parameterName "Schema constraint names must not be empty or whitespace."

    let private ensureNonNegative parameterName value =
        if value < 0 then
#if FABLE_COMPILER
            invalidArg parameterName "Schema constraint bounds must be zero or greater."
#else
            raise (ArgumentOutOfRangeException(parameterName, value, "Schema constraint bounds must be zero or greater."))
#endif

    let private ensureOrderedBounds parameterName minimum maximum =
        if minimum > maximum then
            invalidArg parameterName "Schema constraint minimum bounds must be less than or equal to maximum bounds."

    let private freezeArguments (values: Dictionary<string, obj>) =
#if FABLE_COMPILER
        values :> IReadOnlyDictionary<string, obj>
#else
        System.Collections.ObjectModel.ReadOnlyDictionary<string, obj>(values) :> IReadOnlyDictionary<string, obj>
#endif

    let private emptyArguments =
        freezeArguments (Dictionary<string, obj>())

    let private createKnown code metadata =
        ensureName (nameof code) code
        SchemaConstraint(code, metadata, emptyArguments, None)

    let private createKnownWithArguments code metadata (arguments: (string * obj) seq) =
        ensureName (nameof code) code

        if isNull (box arguments) then
            nullArg (nameof arguments)

        let values = Dictionary<string, obj>()

        arguments
        |> Seq.iter (fun (name, value) ->
            ensureName (nameof arguments) name
            values.Add(name, value))

        SchemaConstraint(code, metadata, freezeArguments values, None)

    /// <summary>Creates portable custom schema constraint metadata with no arguments.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="code" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="code" /> is empty or contains only whitespace.
    /// </exception>
    let create code =
        ensureName (nameof code) code
        SchemaConstraint(code, SchemaConstraintMetadata.Custom code, emptyArguments, None)

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

        SchemaConstraint(
            code,
            SchemaConstraintMetadata.Custom code,
            freezeArguments values,
            None
        )

    /// <summary>Requires a value to be supplied by boundary interpreters.</summary>
    let required = createKnown "required" SchemaConstraintMetadata.Required

    /// <summary>Marks a value as optional for boundary interpreters.</summary>
    let optional = createKnown "optional" SchemaConstraintMetadata.Optional

    /// <summary>Requires a text value to have at least the supplied length.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="minimum" /> is negative.</exception>
    let minLength minimum =
        ensureNonNegative (nameof minimum) minimum
        createKnownWithArguments "minLength" (SchemaConstraintMetadata.MinLength minimum) [ "minimum", box minimum ]

    /// <summary>Requires a text value to have at most the supplied length.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="maximum" /> is negative.</exception>
    let maxLength maximum =
        ensureNonNegative (nameof maximum) maximum
        createKnownWithArguments "maxLength" (SchemaConstraintMetadata.MaxLength maximum) [ "maximum", box maximum ]

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
            (SchemaConstraintMetadata.LengthBetween(minimum, maximum))
            [ "minimum", box minimum; "maximum", box maximum ]

    /// <summary>Requires a text value to match Axial's pragmatic email format.</summary>
    let email = createKnown "email" SchemaConstraintMetadata.Email

    /// <summary>Requires a text value to match the supplied regular expression pattern.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="pattern" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="pattern" /> is empty or contains only whitespace.
    /// </exception>
    let pattern pattern =
        ensureName (nameof pattern) pattern
        createKnownWithArguments "pattern" (SchemaConstraintMetadata.Pattern pattern) [ "pattern", box pattern ]

    /// <summary>Requires a text value to equal one of the supplied choices.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="choices" /> is null.</exception>
    let oneOf (choices: string seq) =
        if isNull (box choices) then
            nullArg (nameof choices)

        let choices = choices |> Seq.toList
        createKnownWithArguments
            "oneOf"
            (SchemaConstraintMetadata.OneOf choices)
            [ "choices", choices |> List.toArray |> box ]

    /// <summary>Requires a value to be inside the supplied inclusive numeric bounds.</summary>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="minimum" /> is greater than <paramref name="maximum" />.
    /// </exception>
    let between minimum maximum =
        ensureOrderedBounds (nameof minimum) minimum maximum
        createKnownWithArguments
            "between"
            (SchemaConstraintMetadata.Between(box minimum, box maximum))
            [ "minimum", box minimum; "maximum", box maximum ]

    /// <summary>Requires a value to be greater than the supplied exclusive numeric lower bound.</summary>
    let greaterThan minimum =
        createKnownWithArguments
            "greaterThan"
            (SchemaConstraintMetadata.GreaterThan(box minimum))
            [ "minimum", box minimum ]

    /// <summary>Requires a value to be less than the supplied exclusive numeric upper bound.</summary>
    let lessThan maximum =
        createKnownWithArguments
            "lessThan"
            (SchemaConstraintMetadata.LessThan(box maximum))
            [ "maximum", box maximum ]

    /// <summary>Requires a value to be greater than or equal to the supplied numeric lower bound.</summary>
    let atLeast minimum =
        createKnownWithArguments
            "atLeast"
            (SchemaConstraintMetadata.AtLeast(box minimum))
            [ "minimum", box minimum ]

    /// <summary>Requires a value to be less than or equal to the supplied numeric upper bound.</summary>
    let atMost maximum =
        createKnownWithArguments
            "atMost"
            (SchemaConstraintMetadata.AtMost(box maximum))
            [ "maximum", box maximum ]

    /// <summary>Requires a collection value to contain exactly the supplied count.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="expected" /> is negative.</exception>
    let count expected =
        ensureNonNegative (nameof expected) expected
        createKnownWithArguments "count" (SchemaConstraintMetadata.Count expected) [ "expected", box expected ]

    /// <summary>Requires a collection value to contain at least the supplied count.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="minimum" /> is negative.</exception>
    let minCount minimum =
        ensureNonNegative (nameof minimum) minimum
        createKnownWithArguments "minCount" (SchemaConstraintMetadata.MinCount minimum) [ "minimum", box minimum ]

    /// <summary>Requires a collection value to contain at most the supplied count.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="maximum" /> is negative.</exception>
    let maxCount maximum =
        ensureNonNegative (nameof maximum) maximum
        createKnownWithArguments "maxCount" (SchemaConstraintMetadata.MaxCount maximum) [ "maximum", box maximum ]

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
            (SchemaConstraintMetadata.CountBetween(minimum, maximum))
            [ "minimum", box minimum; "maximum", box maximum ]

    /// <summary>Requires a collection value to contain no duplicate items.</summary>
    let distinct = createKnown "distinct" SchemaConstraintMetadata.Distinct

    /// <summary>Returns the stable interpreter-facing constraint code.</summary>
    let code (constraint': SchemaConstraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

        constraint'.Code

    /// <summary>Returns the typed interpreter-facing constraint metadata.</summary>
    let metadata (constraint': SchemaConstraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

        constraint'.Metadata

    /// <summary>Returns the structured constraint arguments.</summary>
    let arguments (constraint': SchemaConstraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

        constraint'.Arguments

    /// <summary>Returns a structured constraint argument when present.</summary>
    let tryFindArgument name (constraint': SchemaConstraint) =
        ensureName (nameof name) name

        if isNull constraint' then
            nullArg (nameof constraint')

        match constraint'.Arguments.TryGetValue name with
        | true, value -> Some value
        | false, _ -> None

    /// <summary>Returns the author-supplied message override, when one was attached with <see cref="M:Axial.Schema.SchemaConstraint.withMessage" />.</summary>
    let message (constraint': SchemaConstraint) =
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
    let withMessage (message: string) (constraint': SchemaConstraint) =
        ensureName (nameof message) message

        if isNull constraint' then
            nullArg (nameof constraint')

        SchemaConstraint(constraint'.Code, constraint'.Metadata, constraint'.Arguments, Some message)

type internal ConstructorApplication<'model> =
    { ArgumentCount: int
      ApplyTrusted: obj array -> 'model }

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
                construct () }

    let create1 (construct: 'a -> 'model) =
        if isNull (box construct) then
            nullArg (nameof construct)

        { ArgumentCount = 1
          ApplyTrusted =
            fun arguments ->
                ensureArgumentCount 1 arguments
                construct (unbox<'a> arguments[0]) }

    let create2 (construct: 'a -> 'b -> 'model) =
        if isNull (box construct) then
            nullArg (nameof construct)

        { ArgumentCount = 2
          ApplyTrusted =
            fun arguments ->
                ensureArgumentCount 2 arguments
                construct (unbox<'a> arguments[0]) (unbox<'b> arguments[1]) }

    let create3 (construct: 'a -> 'b -> 'c -> 'model) =
        if isNull (box construct) then
            nullArg (nameof construct)

        { ArgumentCount = 3
          ApplyTrusted =
            fun arguments ->
                ensureArgumentCount 3 arguments
                construct (unbox<'a> arguments[0]) (unbox<'b> arguments[1]) (unbox<'c> arguments[2]) }

    let apply (application: ConstructorApplication<'model>) (arguments: obj array) =
        if isNull (box application) then
            nullArg (nameof application)

        application.ApplyTrusted arguments

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
/// validation-facing requirements such as <see cref="P:Axial.Schema.SchemaConstraint.email" /> remain separate
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
/// cannot support structural equality, and reference equality is enough for <see cref="T:Axial.Schema.ValueSchemaShape" />
/// to remain an ordinary comparable union.
/// </para>
/// </remarks>
type internal RefinedValueOps(construct: obj -> obj, inspect: obj -> obj) =
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
      Constraints: SchemaConstraint list }

and internal ValueSchemaShape =
    | PrimitiveValueDefinition of PrimitiveValueKind
    /// <summary>A named refined/domain value built from a raw value schema plus construction and inspection functions.</summary>
    | RefinedValueDefinition of raw: ValueSchemaDefinition * ops: RefinedValueOps

type internal FieldDescriptor<'model> =
    { ExternalName: ExternalFieldName
      Order: FieldOrder
      Getter: 'model -> obj
      ValueSchema: ValueSchemaDefinition
      Constraints: SchemaConstraint list }

type internal ModelSchemaDefinition<'model> =
    { Constructor: ConstructorApplication<'model>
      Fields: FieldDescriptor<'model> list }

type internal SchemaDefinition<'model> =
    | PendingDefinition
    | ModelDefinition of ModelSchemaDefinition<'model>

type internal FieldDefinition<'model, 'value> =
    { ExternalName: ExternalFieldName
      Order: FieldOrder
      Getter: 'model -> 'value
      ValueSchema: ValueSchemaDefinition
      Constraints: SchemaConstraint list }

/// <summary>
/// Describes one typed field of a trusted model for schema interpreters.
/// </summary>
/// <remarks>
/// <para>
/// A field definition records typed field metadata without tying that metadata to input parsing, diagnostics,
/// validation, codecs, UI generation, or workflow execution. The field's external name is the portable boundary-facing
/// name interpreters use for raw input lookup, diagnostic paths, codecs, generated documentation, and UI metadata.
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

/// <summary>Holds an interpreter-specific typed chain fragment while specializing a schema field chain.</summary>
/// <remarks>
/// Schema interpreters use this as the typed accumulator returned by
/// <see cref="T:Axial.Schema.IFieldChainFactory`2" />. The value is intentionally opaque to <c>Axial.Schema</c>;
/// each interpreter owns the concrete chain shape it stores here.
/// </remarks>
type IFieldChainResult<'model, 'constructorIn, 'constructorOut> =
    /// <summary>Gets the interpreter-owned typed chain fragment.</summary>
    abstract member Value: obj

/// <summary>
/// Builds an interpreter-specific typed view of a model schema's authored field chain.
/// </summary>
/// <remarks>
/// The built <see cref="T:Axial.Schema.Schema`1" /> keeps this typed chain alongside its type-erased
/// <c>FieldDescriptor</c> metadata. Interpreters that need constructor-specialized plans, such as codecs, can walk the
/// typed chain through this factory without asking callers to re-supply fields or constructors and without lowering
/// construction to <c>obj array</c> dispatch.
/// </remarks>
type IFieldChainFactory<'model, 'result> =
    /// <summary>Starts a specialized chain for a constructor with no consumed fields.</summary>
    abstract member OnEnd<'constructor> : unit -> IFieldChainResult<'model, 'constructor, 'constructor>

    /// <summary>Appends one typed field to an interpreter-specific chain.</summary>
    abstract member OnField<'constructorIn, 'field, 'next> :
        order: int *
        field: Field<'model, 'field> *
        head: IFieldChainResult<'model, 'constructorIn, 'field -> 'next> ->
            IFieldChainResult<'model, 'constructorIn, 'next>

    /// <summary>Completes a specialized chain with the original typed constructor.</summary>
    abstract member OnComplete<'constructor> :
        constructor: 'constructor * chain: IFieldChainResult<'model, 'constructor, 'model> -> 'result

type IFieldChain<'model, 'constructor, 'remaining> =
    abstract member GetFields: int -> obj list * int
    abstract member Apply: constructor: obj * arguments: obj array -> obj
    abstract member Build<'result> :
        factory: IFieldChainFactory<'model, 'result> -> IFieldChainResult<'model, 'constructor, 'remaining>

type FieldsEnd<'model, 'constructor>() =
    interface IFieldChain<'model, 'constructor, 'constructor> with
        member _.GetFields(index) = [], index
        member _.Apply(constructor, _) = constructor
        member _.Build(factory) = factory.OnEnd()

type FieldsAppend<'model, 'constructor, 'field, 'next, 'head
    when 'head :> IFieldChain<'model, 'constructor, 'field -> 'next>>
    internal
    (
        head: 'head,
        field: FieldDefinition<'model, 'field>
    ) =

    interface IFieldChain<'model, 'constructor, 'next> with
        member _.GetFields(index) =
            let fields, nextIndex = (head :> IFieldChain<'model, 'constructor, 'field -> 'next>).GetFields index

            let descriptor =
                { FieldDescriptor.ExternalName = field.ExternalName
                  Order = FieldOrder.create nextIndex
                  Getter = fun model -> field.Getter model |> box
                  ValueSchema = field.ValueSchema
                  Constraints = field.Constraints }

            fields @ [ box descriptor ], nextIndex + 1

        member _.Apply(constructor, arguments) =
            let fieldIndex = (head :> IFieldChain<'model, 'constructor, 'field -> 'next>).GetFields(0) |> snd
            let appliedHead =
                (head :> IFieldChain<'model, 'constructor, 'field -> 'next>).Apply(constructor, arguments)

            let typedConstructor = unbox<'field -> 'next> appliedHead
            typedConstructor (unbox<'field> arguments[fieldIndex]) |> box

        member _.Build(factory) =
            let headNode = head :> IFieldChain<'model, 'constructor, 'field -> 'next>
            let headResult = headNode.Build(factory)
            let order = headNode.GetFields(0) |> snd

            let typedField =
                Field(
                    { field with
                        Order = FieldOrder.create order }
                )

            factory.OnField(order, typedField, headResult)

/// <summary>
/// Carries a trusted model constructor and a typed field chain while a model schema is being authored.
/// </summary>
/// <remarks>
/// Each <c>Schema.field</c> application consumes one argument from the remaining constructor type. The builder can
/// only be passed to <c>Schema.build</c> when the remaining constructor type is the model itself, which keeps
/// constructor/getter alignment compiler-checked and scales authoring to any field count without a hand-written
/// <c>mapN</c> family, computation expression, or source generator.
/// </remarks>
type SchemaBuilder<'model, 'constructor, 'remaining, 'chain
    when 'chain :> IFieldChain<'model, 'constructor, 'remaining>>
    internal
    (
        constructor: 'constructor,
        chain: 'chain
    ) =
    member internal _.Constructor = constructor
    member internal _.Chain = chain

type internal ISchemaSpecialization<'model> =
    abstract member Specialize<'result> : factory: IFieldChainFactory<'model, 'result> -> 'result

type internal SchemaSpecialization<'model, 'constructor, 'chain
    when 'chain :> IFieldChain<'model, 'constructor, 'model>>
    (
        constructor: 'constructor,
        chain: 'chain
    ) =

    interface ISchemaSpecialization<'model> with
        member _.Specialize(factory) =
            if isNull (box factory) then
                nullArg (nameof factory)

            let result = chain.Build(factory)
            factory.OnComplete(constructor, result)

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

        { Constructor = constructor
          Fields = fields |> List.sortBy (fun field -> field.Order.Value) }

/// <summary>
/// Describes the portable structure of a trusted model for schema interpreters.
/// </summary>
/// <remarks>
/// <para>
/// A schema definition records model structure and construction metadata without tying that metadata to input parsing,
/// diagnostics, validation, codecs, UI generation, or workflow execution.
/// </para>
/// <para>
/// The public construction path is the progressive typed builder: start with <c>Schema.recordFor&lt;'model, _&gt;</c>,
/// append primitive field steps such as <c>Schema.text "name" _.Name</c>, and finish with <c>Schema.build</c>. The
/// model-type anchor lets field getters use shorthand member access such as <c>_.Name</c>. <c>Schema.field</c> remains
/// available for explicit or custom value schemas, and <c>Schema.record</c> remains available when the model type is
/// already clear or getters are annotated explicitly. Computation expressions and source generators can layer over that
/// builder later, but they are not required for larger models.
/// </para>
/// </remarks>
[<Sealed>]
type Schema<'model> internal (definition: SchemaDefinition<'model>, specialization: ISchemaSpecialization<'model> option) =
    internal new(definition: SchemaDefinition<'model>) = Schema(definition, None)

    member internal _.Definition = definition
    member internal _.Specialization = specialization

/// <summary>
/// Describes the portable shape of a trusted value for schema interpreters.
/// </summary>
/// <remarks>
/// <para>
/// A value schema definition records primitive, refined, collection, optionality, and constraint metadata without tying
/// that metadata to input parsing, diagnostics, validation, codecs, UI generation, or workflow execution.
/// </para>
/// <para>
/// The public construction API is intentionally introduced by the primitive and constraint operations that follow this
/// core type.
/// </para>
/// </remarks>
[<Sealed>]
type ValueSchema<'value> internal (definition: ValueSchemaDefinition) =
    member internal _.Definition = definition

/// <summary>Functions for creating and inspecting value schemas.</summary>
[<RequireQualifiedAccess>]
module Value =
    let private primitive kind =
        ValueSchema(
            { Shape = PrimitiveValueDefinition kind
              Format = None
              Constraints = [] }
        )

    /// <summary>Describes text represented as <see cref="T:System.String" />.</summary>
    let text : ValueSchema<string> = primitive PrimitiveValueKind.Text

    /// <summary>Describes a 32-bit signed integer represented as <see cref="T:System.Int32" />.</summary>
    let ``int`` : ValueSchema<int> = primitive PrimitiveValueKind.Int

    /// <summary>Describes a decimal number represented as <see cref="T:System.Decimal" />.</summary>
    let ``decimal`` : ValueSchema<decimal> = primitive PrimitiveValueKind.Decimal

    /// <summary>Describes a Boolean value represented as <see cref="T:System.Boolean" />.</summary>
    let ``bool`` : ValueSchema<bool> = primitive PrimitiveValueKind.Bool

#if NET6_0_OR_GREATER
    /// <summary>Describes a calendar date represented as <see cref="T:System.DateOnly" />.</summary>
    let date : ValueSchema<DateOnly> = primitive PrimitiveValueKind.Date
#endif

    /// <summary>Describes an instant-like date and time represented as <see cref="T:System.DateTimeOffset" />.</summary>
    let dateTime : ValueSchema<DateTimeOffset> = primitive PrimitiveValueKind.DateTime

    /// <summary>Describes a globally unique identifier represented as <see cref="T:System.Guid" />.</summary>
    let guid : ValueSchema<Guid> = primitive PrimitiveValueKind.Guid

    /// <summary>Returns the intrinsic primitive kind for a primitive value schema.</summary>
    /// <remarks>
    /// This accessor is intentionally strict about the schema being primitive itself. Use
    /// <see cref="M:Axial.Schema.Value.underlyingPrimitiveKind``1" /> to see through refinement layers to the primitive
    /// foundation of a refined value schema.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is a refined value schema.</exception>
    let primitiveKind (schema: ValueSchema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition.Shape with
        | PrimitiveValueDefinition kind -> kind
        | RefinedValueDefinition _ ->
            invalidArg (nameof schema) "Expected a primitive value schema, but the schema is a refined value schema."

    /// <summary>
    /// Describes a named refined/domain value schema by pairing a raw value schema with a value-preserving
    /// construction function and an inspection function that recovers the raw representation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both functions are required, and this is deliberately the only way to author a refined value schema: a
    /// construct-only schema would hide refined values from inspection interpreters that read existing trusted models,
    /// and an inspect-only schema would leave construction interpreters unable to produce the refined value from
    /// parsed raw input.
    /// </para>
    /// <para>
    /// <paramref name="construct" /> is expected to run only after the raw value has already satisfied whatever
    /// checks or constraints an interpreter attaches to the raw value schema; it is not itself expected to fail.
    /// <paramref name="inspect" /> lets interpreters that only understand the raw representation, such as codecs,
    /// diagnostics, JSON Schema, UI, and documentation generators, still operate over the refined value.
    /// </para>
    /// <para>
    /// Refined value schemas built this way are portable metadata, matching primitive value schemas: they can be
    /// combined with <see cref="M:Axial.Schema.Value.withConstraint``1" /> and used as the value schema for
    /// <see cref="M:Axial.Schema.Schema.field``2" /> like any other <see cref="T:Axial.Schema.ValueSchema`1" />.
    /// </para>
    /// <para>
    /// The everyday raw schema is a primitive value schema, especially <see cref="P:Axial.Schema.Value.text" /> for
    /// domain values such as email addresses and names. The primitive foundation stays inspectable through the
    /// refinement: <see cref="M:Axial.Schema.Value.underlyingPrimitiveKind``1" /> reports the intrinsic primitive kind
    /// beneath any number of refinement layers, and <see cref="M:Axial.Schema.Value.rawConstraints``1" /> returns the
    /// constraint metadata carried by the raw schema, so interpreters can parse, render, and document the raw
    /// representation before constructing the refined value. Format metadata declared with
    /// <see cref="M:Axial.Schema.Value.withFormat``1" /> stays visible the same way:
    /// <see cref="M:Axial.Schema.Value.format``1" /> reports the nearest declared format through refinement layers.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="construct" />, <paramref name="inspect" />, or <paramref name="raw" /> is null.
    /// </exception>
    let refined (construct: 'raw -> 'value) (inspect: 'value -> 'raw) (raw: ValueSchema<'raw>) : ValueSchema<'value> =
        if isNull (box construct) then
            nullArg (nameof construct)

        if isNull (box inspect) then
            nullArg (nameof inspect)

        if isNull (box raw) then
            nullArg (nameof raw)

        let ops =
            RefinedValueOps(
                (fun value -> value |> unbox<'raw> |> construct |> box),
                (fun value -> value |> unbox<'value> |> inspect |> box)
            )

        ValueSchema(
            { Shape = RefinedValueDefinition(raw.Definition, ops)
              Format = None
              Constraints = [] }
        )

    /// <summary>Returns whether a value schema is a refined/domain value schema.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let isRefined (schema: ValueSchema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition.Shape with
        | RefinedValueDefinition _ -> true
        | PrimitiveValueDefinition _ -> false

    /// <summary>Returns the intrinsic primitive kind beneath any refinement layers of a value schema.</summary>
    /// <remarks>
    /// Every value schema bottoms out on a primitive value schema, so this accessor is total: it returns the kind of a
    /// primitive value schema directly and walks the raw schemas of refined value schemas, including refined values
    /// layered over other refined values, until it reaches the primitive foundation. Interpreters that only understand
    /// raw representations, such as input parsers, codecs, JSON Schema emitters, UI renderers, and documentation
    /// generators, use this to treat a refined value like its primitive representation at the boundary.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let underlyingPrimitiveKind (schema: ValueSchema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        let rec kindOf (definition: ValueSchemaDefinition) =
            match definition.Shape with
            | PrimitiveValueDefinition kind -> kind
            | RefinedValueDefinition(raw, _) -> kindOf raw

        kindOf schema.Definition

    /// <summary>Returns the constraint metadata carried by the raw value schema of a refined value schema.</summary>
    /// <remarks>
    /// Raw constraints describe the underlying representation that boundary interpreters see before the refined value
    /// is constructed, such as length bounds on the raw text of an email address. They are retained separately from
    /// constraints attached to the refined value schema itself with
    /// <see cref="M:Axial.Schema.Value.withConstraint``1" />, which
    /// <see cref="M:Axial.Schema.Value.constraints``1" /> continues to return.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is a primitive value schema.</exception>
    let rawConstraints (schema: ValueSchema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition.Shape with
        | RefinedValueDefinition(raw, _) -> raw.Constraints
        | PrimitiveValueDefinition _ ->
            invalidArg (nameof schema) "Expected a refined value schema, but the schema is a primitive value schema."

    let private underlyingClrType kind =
        match kind with
        | PrimitiveValueKind.Text -> typeof<string>
        | PrimitiveValueKind.Int -> typeof<int>
        | PrimitiveValueKind.Decimal -> typeof<decimal>
        | PrimitiveValueKind.Bool -> typeof<bool>
#if NET6_0_OR_GREATER
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
    /// Like <see cref="M:Axial.Schema.Value.underlyingPrimitiveKind``1" />, this accessor is total over value schemas:
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
    let inspectUnderlying<'value, 'primitive> (schema: ValueSchema<'value>) : 'value -> 'primitive =
        if isNull (box schema) then
            nullArg (nameof schema)

        let expected = underlyingClrType (underlyingPrimitiveKind schema)

        if typeof<'primitive> <> expected then
            invalidArg
                (nameof schema)
                $"Expected the underlying primitive type {expected.Name}, but the requested projection type is {typeof<'primitive>.Name}."

        let rec project (definition: ValueSchemaDefinition) (value: obj) =
            match definition.Shape with
            | PrimitiveValueDefinition _ -> value
            | RefinedValueDefinition(raw, ops) -> project raw (ops.Inspect value)

        fun value -> project schema.Definition (box value) |> unbox<'primitive>

    /// <summary>Returns a value schema carrying the supplied portable format metadata.</summary>
    /// <remarks>
    /// <para>
    /// The format names the boundary-facing interpretation of the value, such as
    /// <see cref="P:Axial.Schema.SchemaFormat.email" /> for a refined email address over
    /// <see cref="P:Axial.Schema.Value.text" />. It is annotation metadata for interpreters — JSON Schema emitters,
    /// UI renderers, and documentation generators — and attaches no executable check; pair it with constraint
    /// metadata such as <see cref="P:Axial.Schema.SchemaConstraint.email" /> when validation is also required.
    /// </para>
    /// <para>
    /// A value schema carries at most one format. Applying <c>withFormat</c> again replaces the earlier declaration.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="format" /> carries no name.</exception>
    let withFormat (format: SchemaFormat) (schema: ValueSchema<'value>) =
        if String.IsNullOrWhiteSpace format.Name then
            invalidArg (nameof format) "Schema format names must not be empty or whitespace."

        if isNull (box schema) then
            nullArg (nameof schema)

        ValueSchema(
            { schema.Definition with
                Format = Some format }
        )

    /// <summary>Returns the portable format metadata declared nearest to a value schema, when present.</summary>
    /// <remarks>
    /// Like <see cref="M:Axial.Schema.Value.underlyingPrimitiveKind``1" />, this accessor sees through refinement
    /// layers: a format declared on the refined value schema itself wins, and otherwise the raw value schemas are
    /// walked toward the primitive foundation until a declaration is found. A format declared on the raw text of a
    /// refined email address therefore stays visible on the refined schema, while an outer declaration overrides it.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let format (schema: ValueSchema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        let rec formatOf (definition: ValueSchemaDefinition) =
            match definition.Format with
            | Some _ as declared -> declared
            | None ->
                match definition.Shape with
                | RefinedValueDefinition(raw, _) -> formatOf raw
                | PrimitiveValueDefinition _ -> None

        formatOf schema.Definition

    /// <summary>Returns the portable constraint metadata attached to a value schema.</summary>
    let constraints (schema: ValueSchema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        schema.Definition.Constraints

    /// <summary>Returns the portable constraint metadata carried by every layer of a value schema.</summary>
    /// <remarks>
    /// <para>
    /// Constraints are returned foundation-first: the primitive foundation's constraints come first, then each
    /// refinement layer outward, ending with the constraints attached to the schema itself. This matches authoring
    /// order, where raw constraints are declared before the schema is refined. For a primitive value schema the
    /// result equals <see cref="M:Axial.Schema.Value.constraints``1" />.
    /// </para>
    /// <para>
    /// Interpreters that lower a value schema's complete constraint metadata to one executable program — such as a
    /// check over the underlying primitive representation obtained with
    /// <see cref="M:Axial.Schema.Value.inspectUnderlying``2" /> — use this accessor so constraints declared on raw
    /// layers and on the refined schema are honored together. Per-layer inspection remains available through
    /// <see cref="M:Axial.Schema.Value.constraints``1" /> and <see cref="M:Axial.Schema.Value.rawConstraints``1" />.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let allConstraints (schema: ValueSchema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        let rec gather (definition: ValueSchemaDefinition) =
            match definition.Shape with
            | PrimitiveValueDefinition _ -> definition.Constraints
            | RefinedValueDefinition(raw, _) -> gather raw @ definition.Constraints

        gather schema.Definition

    /// <summary>Returns a value schema with additional portable constraint metadata appended in declaration order.</summary>
    let withConstraint (constraint': SchemaConstraint) (schema: ValueSchema<'value>) =
        if isNull constraint' then
            nullArg (nameof constraint')

        if isNull (box schema) then
            nullArg (nameof schema)

        ValueSchema(
            { schema.Definition with
                Constraints = schema.Definition.Constraints @ [ constraint' ] }
        )

    /// <summary>Returns a value schema with additional portable constraint metadata appended in declaration order.</summary>
    let withConstraints (constraints: SchemaConstraint list) (schema: ValueSchema<'value>) =
        if isNull (box constraints) then
            nullArg (nameof constraints)

        constraints
        |> List.iter (fun constraint' ->
            if isNull constraint' then
                nullArg (nameof constraints))

        if isNull (box schema) then
            nullArg (nameof schema)

        ValueSchema(
            { schema.Definition with
                Constraints = schema.Definition.Constraints @ constraints }
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
    /// directly. Ordinary record schemas should use <c>Schema.recordFor&lt;'model, _&gt;</c>, pipeline
    /// <c>Schema.field</c> steps, and <c>Schema.build</c>.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="externalName" />, <paramref name="getter" />, or <paramref name="value" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="externalName" /> is empty or contains only whitespace.
    /// </exception>
    let create externalName (getter: 'model -> 'value) (value: ValueSchema<'value>) : Field<'model, 'value> =
        if isNull (box getter) then
            nullArg (nameof getter)

        if isNull (box value) then
            nullArg (nameof value)

        Field(
            { ExternalName = ExternalFieldName.create externalName
              Order = FieldOrder.create 0
              Getter = getter
              ValueSchema = value.Definition
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
    let withConstraint (constraint': SchemaConstraint) (field: Field<'model, 'value>) =
        if isNull constraint' then
            nullArg (nameof constraint')

        if isNull (box field) then
            nullArg (nameof field)

        Field(
            { field.Definition with
                Constraints = field.Definition.Constraints @ [ constraint' ] }
        )

    /// <summary>Returns a schema field with additional portable constraint metadata appended in declaration order.</summary>
    let withConstraints (constraints: SchemaConstraint list) (field: Field<'model, 'value>) =
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
module Schema =
    /// <summary>
    /// Starts a progressive typed model schema builder from a trusted curried constructor.
    /// </summary>
    /// <remarks>
    /// Each following <c>Schema.field</c> step consumes one argument from the constructor type. A partially-applied
    /// builder will not type-check with <c>Schema.build</c>; the final remaining type must be the model. This builder
    /// replaces the earlier fixed-arity <c>Schema.map2</c>/<c>Schema.map3</c> proof shape. Use
    /// <c>Schema.recordFor&lt;'model, _&gt;</c> when field getters need shorthand member access such as <c>_.Name</c>; plain
    /// <c>Schema.record</c> often requires annotating getter lambdas so F# can infer the model type.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="constructor" /> is null.</exception>
    let record (constructor: 'constructor) : SchemaBuilder<'model, 'constructor, 'constructor, FieldsEnd<'model, 'constructor>> =
        if isNull (box constructor) then
            nullArg (nameof constructor)

        SchemaBuilder(constructor, FieldsEnd<'model, 'constructor>())

    /// <summary>
    /// Starts a progressive typed model schema builder while explicitly anchoring the model type.
    /// </summary>
    /// <remarks>
    /// This is the everyday builder entry point for record schemas because the model-type anchor lets following field
    /// getters use shorthand member access:
    /// <code>
    /// Schema.recordFor&lt;Customer, _&gt; create
    /// |&gt; Schema.field "name" _.Name Value.text
    /// |&gt; Schema.build
    /// </code>
    /// It preserves the same typed field chain as <c>Schema.record</c>; each field still consumes one constructor
    /// argument and <c>Schema.build</c> still requires the constructor to be fully applied.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="constructor" /> is null.</exception>
    let recordFor<'model, 'constructor>
        (constructor: 'constructor)
        : SchemaBuilder<'model, 'constructor, 'constructor, FieldsEnd<'model, 'constructor>> =
        if isNull (box constructor) then
            nullArg (nameof constructor)

        SchemaBuilder(constructor, FieldsEnd<'model, 'constructor>())

    /// <summary>
    /// Appends a typed field to a progressive schema builder.
    /// </summary>
    /// <remarks>
    /// The field value type must match the next constructor argument. The returned builder carries the remaining
    /// constructor type after that argument has been consumed, so field order and constructor application stay aligned
    /// by ordinary F# type-checking.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="externalName" />, <paramref name="getter" />, <paramref name="value" />, or
    /// <paramref name="builder" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when <paramref name="externalName" /> is empty or contains only whitespace.
    /// </exception>
    let field
        externalName
        (getter: 'model -> 'field)
        (value: ValueSchema<'field>)
        (builder: SchemaBuilder<'model, 'constructor, 'field -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, 'field, 'next, 'chain>> =
        if isNull (box getter) then
            nullArg (nameof getter)

        if isNull (box value) then
            nullArg (nameof value)

        if isNull (box builder) then
            nullArg (nameof builder)

        let definition =
            { ExternalName = ExternalFieldName.create externalName
              Order = FieldOrder.create 0
              Getter = getter
              ValueSchema = value.Definition
              Constraints = [] }

        SchemaBuilder(builder.Constructor, FieldsAppend(builder.Chain, definition))

    /// <summary>
    /// Appends a typed field with field-level constraint metadata to a progressive schema builder.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="constraints" />, a constraint entry, <paramref name="externalName" />,
    /// <paramref name="getter" />, <paramref name="value" />, or <paramref name="builder" /> is null.
    /// </exception>
    let fieldWith
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> 'field)
        (value: ValueSchema<'field>)
        (builder: SchemaBuilder<'model, 'constructor, 'field -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, 'field, 'next, 'chain>> =
        if isNull (box constraints) then
            nullArg (nameof constraints)

        constraints
        |> List.iter (fun constraint' ->
            if isNull constraint' then
                nullArg (nameof constraints))

        if isNull (box getter) then
            nullArg (nameof getter)

        if isNull (box value) then
            nullArg (nameof value)

        if isNull (box builder) then
            nullArg (nameof builder)

        let definition =
            { ExternalName = ExternalFieldName.create externalName
              Order = FieldOrder.create 0
              Getter = getter
              ValueSchema = value.Definition
              Constraints = constraints }

        SchemaBuilder(builder.Constructor, FieldsAppend(builder.Chain, definition))

    /// <summary>Appends a text field represented as <see cref="T:System.String" /> to a progressive schema builder.</summary>
    let text
        externalName
        (getter: 'model -> string)
        (builder: SchemaBuilder<'model, 'constructor, string -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, string, 'next, 'chain>> =
        field externalName getter Value.text builder

    /// <summary>Appends a 32-bit signed integer field represented as <see cref="T:System.Int32" /> to a progressive schema builder.</summary>
    let ``int``
        externalName
        (getter: 'model -> int)
        (builder: SchemaBuilder<'model, 'constructor, int -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, int, 'next, 'chain>> =
        field externalName getter Value.``int`` builder

    /// <summary>Appends a decimal field represented as <see cref="T:System.Decimal" /> to a progressive schema builder.</summary>
    let ``decimal``
        externalName
        (getter: 'model -> decimal)
        (builder: SchemaBuilder<'model, 'constructor, decimal -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, decimal, 'next, 'chain>> =
        field externalName getter Value.``decimal`` builder

    /// <summary>Appends a Boolean field represented as <see cref="T:System.Boolean" /> to a progressive schema builder.</summary>
    let ``bool``
        externalName
        (getter: 'model -> bool)
        (builder: SchemaBuilder<'model, 'constructor, bool -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, bool, 'next, 'chain>> =
        field externalName getter Value.``bool`` builder

#if NET6_0_OR_GREATER
    /// <summary>Appends a calendar date field represented as <see cref="T:System.DateOnly" /> to a progressive schema builder.</summary>
    let date
        externalName
        (getter: 'model -> DateOnly)
        (builder: SchemaBuilder<'model, 'constructor, DateOnly -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, DateOnly, 'next, 'chain>> =
        field externalName getter Value.date builder
#endif

    /// <summary>Appends an instant-like date and time field represented as <see cref="T:System.DateTimeOffset" /> to a progressive schema builder.</summary>
    let dateTime
        externalName
        (getter: 'model -> DateTimeOffset)
        (builder: SchemaBuilder<'model, 'constructor, DateTimeOffset -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, DateTimeOffset, 'next, 'chain>> =
        field externalName getter Value.dateTime builder

    /// <summary>Appends a globally unique identifier field represented as <see cref="T:System.Guid" /> to a progressive schema builder.</summary>
    let guid
        externalName
        (getter: 'model -> Guid)
        (builder: SchemaBuilder<'model, 'constructor, Guid -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, Guid, 'next, 'chain>> =
        field externalName getter Value.guid builder

    /// <summary>
    /// Builds a model schema from a progressive typed builder whose constructor has been fully applied by fields.
    /// </summary>
    /// <remarks>
    /// This is the arity-independent schema construction path. It preserves the existing type-erased model definition
    /// for metadata interpreters while deriving constructor application and field ordering from the typed field chain,
    /// without adding more fixed-arity <c>Schema.mapN</c> helpers.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="builder" /> is null.</exception>
    let build (builder: SchemaBuilder<'model, 'constructor, 'model, 'chain>) : Schema<'model> =
        if isNull (box builder) then
            nullArg (nameof builder)

        let chain = builder.Chain :> IFieldChain<'model, 'constructor, 'model>
        let fields, count =
            let fields, count = chain.GetFields 0
            fields |> List.map unbox<FieldDescriptor<'model>>, count

        let constructor =
            { ConstructorApplication.ArgumentCount = count
              ApplyTrusted =
                fun arguments ->
                    ConstructorApplication.ensureArgumentCount count arguments
                    chain.Apply(box builder.Constructor, arguments) |> unbox<'model> }

        let specialization =
            SchemaSpecialization<'model, 'constructor, 'chain>(builder.Constructor, builder.Chain) :> ISchemaSpecialization<'model>

        Schema(ModelDefinition(ModelSchemaDefinition.create constructor fields), Some specialization)

    /// <summary>
    /// Specializes a built model schema's retained typed field chain into an interpreter-specific result.
    /// </summary>
    /// <remarks>
    /// This is the constructor-specialized companion to the type-erased schema metadata exposed through ordinary
    /// schema inspection. It is intended for interpreters such as codecs that need to compile direct record plans from
    /// a <c>Schema&lt;'model&gt;</c> value without asking callers to re-supply the constructor or typed fields.
    /// Schemas produced by <see cref="M:Axial.Schema.Schema.build``4" /> carry this typed view.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="factory" /> or
    /// <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when the schema was not produced by the progressive typed
    /// builder and therefore has no retained typed field chain.</exception>
    let specialize (factory: IFieldChainFactory<'model, 'result>) (schema: Schema<'model>) : 'result =
        if isNull (box factory) then
            nullArg (nameof factory)

        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Specialization with
        | Some specialization -> specialization.Specialize factory
        | None -> invalidArg (nameof schema) "The schema does not carry a typed field chain."
