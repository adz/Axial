// The value-schema catalog implementation behind Schema.text, Schema.int, Schema.list,
// Schema.refine, Schema.union and friends: constructors and combinators over the erased value
// definitions in Definitions.fs. Internal; SchemaApi.fs exposes the public surface.
namespace Axial.Schema

open System
open System.Collections.Generic
open Axial.Refined

/// <summary>Functions for creating and inspecting value schemas.</summary>
[<RequireQualifiedAccess>]
module internal ValueSchema =
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
        (refinement: Refinement<'raw, 'value>)
        (raw: Schema<'raw>)
        : Schema<'value> =
        if isNull (box refinement) then nullArg (nameof refinement)
        if isNull (box raw) then nullArg (nameof raw)

        let ops =
            RefinedValueOps(
                (fun value ->
                    match Refinement.create refinement (unbox<'raw> value) with
                    | Ok refined -> Ok(box refined)
                    | Error error -> Error(SchemaError.ofRefinementError error)),
                (fun value -> value |> unbox<'value> |> Refinement.inspect refinement |> box)
            )

        Schema(ValueDefinition
            { Shape = RefinedValueDefinition(raw.ValueDefinition, ops)
              Format = None
              Constraints = []
              Description = None
              Default = None })

    let validate
        (validation: 'value -> Result<unit, SchemaError>)
        (schema: Schema<'value>)
        : Schema<'value> =
        if isNull (box validation) then nullArg (nameof validation)
        if isNull (box schema) then nullArg (nameof schema)

        let ops =
            RefinedValueOps(
                (fun value ->
                    let typed = unbox<'value> value

                    match validation typed with
                    | Ok () -> Ok value
                    | Error error -> Error [ error ]),
                id
            )

        Schema(ValueDefinition
            { Shape = RefinedValueDefinition(schema.ValueDefinition, ops)
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
