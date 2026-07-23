// The internal core module the public Schema facade (SchemaApi.fs) re-exports, plus field
// descriptor helpers and the public Field module.
namespace Axial.Schema

open System
open System.Collections.Generic

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
    /// directly. Ordinary record schemas declare fields inside <c>schema&lt;'model&gt; { }</c>.
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
    let text = ValueSchema.text
    /// <summary>Describes a 32-bit signed integer.</summary>
    let ``int`` = ValueSchema.``int``
    /// <summary>Describes a decimal number.</summary>
    let ``decimal`` = ValueSchema.``decimal``
    /// <summary>Describes a Boolean value.</summary>
    let ``bool`` = ValueSchema.``bool``
#if NET8_0_OR_GREATER
    /// <summary>Describes a calendar date.</summary>
    let date = ValueSchema.date
#endif
    /// <summary>Describes a date and time with an offset.</summary>
    let dateTime = ValueSchema.dateTime
    /// <summary>Describes a globally unique identifier.</summary>
    let guid = ValueSchema.guid
    /// <summary>Describes a list whose items use the supplied schema.</summary>
    let listWith item = ValueSchema.manyOf item
    /// <summary>Describes an optional value.</summary>
    let option item = ValueSchema.optionOf item
    /// <summary>Describes a string-keyed map whose values use the supplied schema.</summary>
    let mapWith item = ValueSchema.map item
    let constrainItems constraint' schema = ValueSchema.constrainItems constraint' schema
    let constrainValues constraint' schema = ValueSchema.constrainValues constraint' schema
    /// <summary>Defers a recursive schema.</summary>
    let defer schema = ValueSchema.lazyOf schema
    /// <summary>Converts a schema through total construction and inspection functions.</summary>
    let convert construct inspect schema = ValueSchema.refined construct inspect schema
    let refine refinement schema = ValueSchema.refine refinement schema
    let validate validation schema = ValueSchema.validate validation schema
    /// <summary>Describes a tagged union.</summary>
    let union discriminator payload cases = ValueSchema.union discriminator payload cases
    /// <summary>Describes an internally tagged union.</summary>
    let inlineUnion discriminator cases = ValueSchema.unionInline discriminator cases
    /// <summary>Describes a payload-less tagged enum.</summary>
    let enum cases = ValueSchema.enumOf cases
    /// <summary>Appends one portable constraint.</summary>
    let constrain constraint' schema = ValueSchema.withConstraint constraint' schema
    /// <summary>Appends portable constraints in declaration order.</summary>
    let constrainAll constraints schema = ValueSchema.withConstraints constraints schema
    /// <summary>Attaches boundary format metadata.</summary>
    let withFormat format schema = ValueSchema.withFormat format schema
    /// <summary>Attaches descriptive metadata.</summary>
    /// <summary>Attaches a default value.</summary>
    let withDefault value schema = ValueSchema.withDefault value schema
    let format schema = ValueSchema.format schema
    let description schema = ValueSchema.description schema
    let defaultValue schema = ValueSchema.defaultValue schema
    let constraints schema = ValueSchema.constraints schema
    let isRefined schema = ValueSchema.isRefined schema
    let primitiveKind schema = ValueSchema.primitiveKind schema
    let underlyingPrimitiveKind schema = ValueSchema.underlyingPrimitiveKind schema
    let rawConstraints schema = ValueSchema.rawConstraints schema
    let internal inspectUnderlying<'value, 'primitive> schema = ValueSchema.inspectUnderlying<'value, 'primitive> schema
    let internal allConstraints schema = ValueSchema.allConstraints schema
    /// <summary>
    /// Compiles a built model schema's retained typed shape into an interpreter-specific record plan.
    /// </summary>
    /// <remarks>
    /// This is the constructor-specialized companion to the type-erased schema metadata exposed through ordinary
    /// schema inspection. It is intended for interpreters such as codecs that need to compile direct record plans from
    /// a <c>Schema&lt;'model&gt;</c> value without asking callers to re-supply the constructor or typed fields.
    /// Schemas completed with <c>construct</c> or <c>constructResult</c> carry this typed view.
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
        | ValueDefinition _ -> ValueSchema.describe text schema
