namespace Axial.Schema

type internal SchemaDefinition =
    | PendingDefinition

type internal ValueSchemaDefinition =
    | PendingValueDefinition

type internal FieldDefinition<'value> =
    { ValueSchema: ValueSchemaDefinition }

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
type Schema<'model> internal (definition: SchemaDefinition) =
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

/// <summary>
/// Describes one typed field of a trusted model for schema interpreters.
/// </summary>
/// <remarks>
/// <para>
/// A field definition records typed field metadata without tying that metadata to input parsing, diagnostics,
/// validation, codecs, UI generation, or workflow execution.
/// </para>
/// <para>
/// Field names, getters, constructor application, ordering, and public construction helpers are introduced by the
/// schema operations that follow this core type.
/// </para>
/// </remarks>
[<Sealed>]
type Field<'model, 'value> internal (definition: FieldDefinition<'value>) =
    member internal _.Definition = definition
