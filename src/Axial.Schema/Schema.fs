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

type internal SchemaDefinition =
    | PendingDefinition

type internal ValueSchemaDefinition =
    | PendingValueDefinition

type internal FieldDefinition<'value> =
    { ExternalName: ExternalFieldName
      ValueSchema: ValueSchemaDefinition }

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
/// validation, codecs, UI generation, or workflow execution. The field's external name is the portable boundary-facing
/// name interpreters use for raw input lookup, diagnostic paths, codecs, generated documentation, and UI metadata.
/// </para>
/// <para>
/// Getters, constructor application, ordering, and public construction helpers are introduced by the schema operations
/// that follow this core type.
/// </para>
/// </remarks>
[<Sealed>]
type Field<'model, 'value> internal (definition: FieldDefinition<'value>) =
    member internal _.Definition = definition

/// <summary>Functions for inspecting schema field metadata.</summary>
[<RequireQualifiedAccess>]
module Field =
    /// <summary>Returns the boundary-facing name for a schema field.</summary>
    let externalName (field: Field<'model, 'value>) =
        if isNull (box field) then
            nullArg (nameof field)

        field.Definition.ExternalName
