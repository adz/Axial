namespace Axial.Schema

/// <summary>
/// Describes the shape of a value schema as inspectable metadata for non-validation interpreters.
/// </summary>
/// <remarks>
/// <para>
/// Shape descriptions carry no getters, constructors, or executable checks. JSON Schema emitters, documentation
/// generators, and UI metadata producers can walk them without parsing raw input or running validation.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
type ValueShape =
    /// <summary>A primitive value of the supplied kind.</summary>
    | Primitive of kind: PrimitiveValueKind
    /// <summary>A refined/domain value whose boundary representation is the supplied underlying description.</summary>
    | Refined of underlying: ValueDescription
    /// <summary>A nested model value described by its own field descriptions.</summary>
    | Nested of model: ModelDescription
    /// <summary>A collection value whose items share the supplied item description.</summary>
    | Many of item: ValueDescription
    /// <summary>A tagged union value with explicit discriminator, payload field, and case descriptions.</summary>
    | Union of union: UnionDescription
    /// <summary>An internally-tagged union value whose case payload fields sit beside the discriminator field.</summary>
    | UnionInline of union: UnionInlineDescription
    /// <summary>A bare-string enum value with explicit case tags.</summary>
    | Enum of enum: EnumDescription
    /// <summary>An optional value whose present payload is described by the supplied payload description.</summary>
    | Optional of payload: ValueDescription
    /// <summary>A dictionary value, keyed by text, whose entries share the supplied item description.</summary>
    | MapOf of item: ValueDescription
    /// <summary>The first expansion of a deferred recursive value, identified within this inspection tree.</summary>
    | Deferred of reference: int * value: ValueDescription
    /// <summary>A reference back to an already-expanding deferred value.</summary>
    | Recursive of reference: int

/// <summary>Describes one value schema: its shape, declared format, and portable constraint metadata.</summary>
and ValueDescription =
    {
        /// <summary>The structural shape of the value.</summary>
        Shape: ValueShape
        /// <summary>The declared boundary format, when one was attached with <c>Value.withFormat</c>.</summary>
        Format: SchemaFormat option
        /// <summary>The portable constraint metadata attached to this value schema layer, in declaration order.</summary>
        Constraints: SchemaConstraint list
        /// <summary>The description metadata, when one was attached with <c>Value.describe</c>.</summary>
        Description: string option
        /// <summary>The default-value metadata, when one was attached with <c>Value.withDefault</c>.</summary>
        Default: obj option
    }

/// <summary>Describes one field of a model schema for inspection interpreters.</summary>
and FieldDescription =
    {
        /// <summary>The boundary-facing external field name.</summary>
        Name: string
        /// <summary>The zero-based field order used for trusted construction and ordered interpreter output.</summary>
        Order: int
        /// <summary>The description of the field's value schema.</summary>
        Value: ValueDescription
        /// <summary>The portable constraint metadata attached at the field level, in declaration order.</summary>
        Constraints: SchemaConstraint list
    }

/// <summary>Describes a built model schema as an ordered list of field descriptions.</summary>
and ModelDescription =
    {
        /// <summary>The field descriptions in declared order.</summary>
        Fields: FieldDescription list
        /// <summary>The description metadata, when one was attached with <c>Schema.describe</c>.</summary>
        Description: string option
    }

/// <summary>Describes one case in a tagged union value schema.</summary>
and UnionCaseDescription =
    {
        /// <summary>The raw discriminator tag for this union case.</summary>
        Tag: string
        /// <summary>The schema description of this case's payload.</summary>
        Payload: ValueDescription
    }

/// <summary>Describes a tagged union value schema.</summary>
and UnionDescription =
    {
        /// <summary>The raw input field name that carries the case tag.</summary>
        DiscriminatorField: string
        /// <summary>The raw input field name that carries the case payload.</summary>
        PayloadField: string
        /// <summary>The union cases in declaration order.</summary>
        Cases: UnionCaseDescription list
    }

/// <summary>Describes one case in an internally-tagged union value schema.</summary>
and UnionInlineCaseDescription =
    {
        /// <summary>The raw discriminator tag for this union case.</summary>
        Tag: string
        /// <summary>The field descriptions of this case's spliced-in payload.</summary>
        Payload: ModelDescription
    }

/// <summary>Describes an internally-tagged union value schema.</summary>
and UnionInlineDescription =
    {
        /// <summary>The raw input field name that carries the case tag.</summary>
        DiscriminatorField: string
        /// <summary>The union cases in declaration order.</summary>
        Cases: UnionInlineCaseDescription list
    }

/// <summary>Describes one case in a bare-string enum value schema.</summary>
and EnumCaseDescription =
    {
        /// <summary>The raw tag for this enum case.</summary>
        Tag: string
    }

/// <summary>Describes a bare-string enum value schema.</summary>
and EnumDescription =
    {
        /// <summary>The enum cases in declaration order.</summary>
        Cases: EnumCaseDescription list
    }

/// <summary>The inspection API over built schemas and value schemas.</summary>
/// <remarks>
/// <para>
/// <c>Inspect</c> is the entry point for non-validation interpreters: JSON Schema generation, documentation, UI
/// metadata, and codec planning all start from the same descriptions. The returned trees are plain immutable data —
/// inspecting them never parses input, runs checks, or constructs models.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module Inspect =
    let private describeValueDefinitionRoot (definition: ValueSchemaDefinition) : ValueDescription =
        let identities = System.Collections.Generic.Dictionary<DeferredValueDefinition, int>(HashIdentity.Reference)
        let expanding = System.Collections.Generic.HashSet<DeferredValueDefinition>(HashIdentity.Reference)

        let rec describeValueDefinition (definition: ValueSchemaDefinition) : ValueDescription =
            let metadata shape =
                { Shape = shape
                  Format = definition.Format
                  Constraints = definition.Constraints
                  Description = definition.Description
                  Default = definition.Default }

            match definition.Shape with
            | LazyValueDefinition deferred ->
                let reference =
                    match identities.TryGetValue deferred with
                    | true, value -> value
                    | false, _ ->
                        let value = identities.Count + 1
                        identities.Add(deferred, value)
                        value

                if expanding.Contains deferred then
                    metadata (ValueShape.Recursive reference)
                else
                    expanding.Add deferred |> ignore
                    let value = describeValueDefinition (deferred.Force())
                    expanding.Remove deferred |> ignore
                    metadata (ValueShape.Deferred(reference, value))
            | _ ->
                let shape =
                    match definition.Shape with
                    | PrimitiveValueDefinition kind -> ValueShape.Primitive kind
                    | RefinedValueDefinition(raw, _) -> ValueShape.Refined(describeValueDefinition raw)
                    | NestedValueDefinition(nested, _) ->
                        ValueShape.Nested
                            { Fields = nested.Fields |> List.map describeFieldDescriptor
                              Description = nested.Description }
                    | ManyValueDefinition collection -> ValueShape.Many(describeValueDefinition collection.Item)
                    | UnionValueDefinition union ->
                        ValueShape.Union
                            { DiscriminatorField = ExternalFieldName.value union.DiscriminatorField
                              PayloadField = ExternalFieldName.value union.PayloadField
                              Cases = union.Cases |> List.map (fun case -> { Tag = case.Tag; Payload = describeValueDefinition case.Payload }) }
                    | UnionInlineValueDefinition union ->
                        ValueShape.UnionInline
                            { DiscriminatorField = ExternalFieldName.value union.DiscriminatorField
                              Cases =
                                union.Cases
                                |> List.map (fun case ->
                                    match case.Payload.Shape with
                                    | NestedValueDefinition(nested, _) ->
                                        { Tag = case.Tag; Payload = { Fields = nested.Fields |> List.map describeFieldDescriptor; Description = nested.Description } }
                                    | _ -> invalidOp "Union-inline case payloads must be nested model schemas.") }
                    | EnumValueDefinition enum -> ValueShape.Enum { Cases = enum.Cases |> List.map (fun case -> { Tag = case.Tag }) }
                    | OptionValueDefinition optional -> ValueShape.Optional(describeValueDefinition optional.Payload)
                    | MapValueDefinition collection -> ValueShape.MapOf(describeValueDefinition collection.Item)
                    | LazyValueDefinition _ -> invalidOp "Deferred definitions are handled before ordinary shapes."
                metadata shape

        and describeFieldDescriptor (field: FieldDescriptor<obj>) : FieldDescription =
            { Name = ExternalFieldName.value field.ExternalName
              Order = FieldOrder.value field.Order
              Value = describeValueDefinition field.ValueSchema
              Constraints = field.Constraints }

        describeValueDefinition definition

    let internal describeValueDefinition definition = describeValueDefinitionRoot definition


    let internal describeModelDefinition (definition: ModelSchemaDefinition<'model>) : ModelDescription =
        { Fields =
            definition.Fields
            |> List.map (fun field ->
                { Name = ExternalFieldName.value field.ExternalName
                  Order = FieldOrder.value field.Order
                  Value = describeValueDefinitionRoot field.ValueSchema
                  Constraints = field.Constraints })
          Description = definition.Description }

    /// <summary>Describes a built model schema as inspectable field metadata.</summary>
    /// <param name="schema">The built model schema to describe.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> was not produced by <c>Schema.build</c>.</exception>
    /// <example>
    /// <code>
    /// let description = Inspect.model customerSchema
    /// let names = description.Fields |> List.map _.Name
    /// </code>
    /// </example>
    let model (schema: Schema<'model>) : ModelDescription =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition with
        | PendingDefinition -> invalidArg (nameof schema) "Expected a built model schema."
        | ModelDefinition definition -> describeModelDefinition definition

    /// <summary>Describes a value schema as inspectable shape, format, and constraint metadata.</summary>
    /// <param name="schema">The value schema to describe.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let value (schema: ValueSchema<'value>) : ValueDescription =
        if isNull (box schema) then
            nullArg (nameof schema)

        describeValueDefinition schema.Definition

    /// <summary>Describes a standalone schema field as inspectable field metadata.</summary>
    /// <param name="field">The schema field to describe.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="field" /> is null.</exception>
    let field (field: Field<'model, 'value>) : FieldDescription =
        if isNull (box field) then
            nullArg (nameof field)

        { Name = ExternalFieldName.value field.Definition.ExternalName
          Order = FieldOrder.value field.Definition.Order
          Value = describeValueDefinition field.Definition.ValueSchema
          Constraints = field.Definition.Constraints }
