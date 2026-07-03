namespace Axial.Schema

open System

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
            raise (ArgumentOutOfRangeException(nameof value, value, "Field order must be zero or greater."))

        FieldOrder value

    /// <summary>Returns the zero-based field position.</summary>
    let value (order: FieldOrder) = order.Value

type internal ConstructorApplication<'model> =
    { ArgumentCount: int
      ApplyTrusted: obj array -> 'model }

module internal ConstructorApplication =
    let private ensureArgumentCount expected (arguments: obj array) =
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

type internal ValueSchemaDefinition =
    | PrimitiveValueDefinition of PrimitiveValueKind

type internal FieldDescriptor<'model> =
    { ExternalName: ExternalFieldName
      Order: FieldOrder
      Getter: 'model -> obj
      ValueSchema: ValueSchemaDefinition }

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
      ValueSchema: ValueSchemaDefinition }

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
/// The public construction API is intentionally introduced by the field and value-schema operations that follow this
/// core type.
/// </para>
/// </remarks>
[<Sealed>]
type Schema<'model> internal (definition: SchemaDefinition<'model>) =
    member internal _.Definition = definition

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
    let private primitive kind = ValueSchema(PrimitiveValueDefinition kind)

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
    let primitiveKind (schema: ValueSchema<'value>) =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition with
        | PrimitiveValueDefinition kind -> kind

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

module internal FieldDescriptorOps =
    let fromField (field: Field<'model, 'value>) : FieldDescriptor<'model> =
        if isNull (box field) then
            nullArg (nameof field)

        { FieldDescriptor.ExternalName = field.Definition.ExternalName
          Order = field.Definition.Order
          Getter = fun model -> field.Definition.Getter model |> box
          ValueSchema = field.Definition.ValueSchema }

/// <summary>Functions for inspecting schema field metadata.</summary>
[<RequireQualifiedAccess>]
module Field =
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
