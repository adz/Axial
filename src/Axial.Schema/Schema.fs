namespace Axial.Schema

type internal SchemaDefinition =
    | PendingDefinition

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
