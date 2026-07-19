// The type-erased description layer: ConstructorApplication (how a model is built from trusted
// arguments — typed closures, never runtime reflection), value-shape definitions (primitives,
// options, collections, maps, unions, enums, refined and deferred values), and the erased record
// views FieldDescriptor/ModelSchemaDefinition plus the typed FieldDefinition/Field pair. Metadata
// interpreters work from these; nothing here executes.
namespace Axial.Schema

open System
open System.Collections.Generic

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
