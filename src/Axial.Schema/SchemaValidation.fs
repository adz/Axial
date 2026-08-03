// Constraint execution: runs the constraints stored on a schema at parse and check time.
// Parsing.fs drives this per field; it has no knowledge of input sources or whole-model construction.
namespace Axial.Schema

open System
open Axial.Constraint
open Axial.Refined
open Axial.Schema

/// <summary>
/// Marks the package that owns schema input, diagnostics, validation, and rules interpreters.
/// </summary>
/// <remarks>
/// <para>
/// The interpreter surface is intentionally introduced in focused slices after the core schema metadata model is proven.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module SchemaValidation =
    /// <summary>Identifies the schema validation integration package.</summary>
    let packageName = "Axial.Schema"

/// <summary>Runs the constraints Schema stores against boxed values.</summary>
/// <remarks>
/// There is no shape-directed selection here. Every stored constraint already carries the typed closure it was
/// built from, so a constraint applies exactly where it was attached and Schema never has to decide from metadata
/// whether a rule is about text, ordering, or collections.
/// </remarks>
module internal ErasedCheck =
    /// Runs every value constraint in declaration order and accumulates failures into one normalized tree,
    /// matching what a single `Constraint.all` over the same rules would produce.
    let run (rules: SchemaRule list) (value: obj) : Result<unit, Violation> =
        let failures =
            rules
            |> SchemaRule.constraints
            |> List.choose (fun erased ->
                match erased.Check value with
                | Ok() -> None
                | Error violation -> Some violation)

        match Violation.conjoin failures with
        | None -> Ok()
        | Some violation -> Error violation

/// <summary>Functions for running executable value checks against refined and primitive value schemas.</summary>
/// <remarks>
/// <para>
/// Refined value schemas describe named domain values, such as an <c>Email</c> refined over raw text, while their
/// constraints are expressed against the underlying primitive representation. This interpreter runs those
/// constraints against a schema's values by projecting each trusted value through the schema's refinement layers
/// with <see cref="M:Axial.Schema.Schema.inspectUnderlying``2" /> and checking the result. Primitive value schemas
/// work the same way with an identity projection.
/// </para>
/// <para>
/// Each refinement layer executes the complete checks attached at that layer. Raw constraints run against the raw
/// value; constraints attached after refinement run against the refined value.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module SchemaCheck =
    /// <summary>
    /// Adapts a check over a schema's underlying primitive representation into a check over the schema's values.
    /// </summary>
    /// <remarks>
    /// The projection to the underlying primitive representation is created eagerly, so a projection type that
    /// does not match the schema's underlying primitive kind fails here rather than on each checked value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="check" /> or <paramref name="schema" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the check's value type does not match the schema's underlying primitive kind.
    /// </exception>
    /// <example>
    /// <code>let checkEmail = SchemaCheck.fromUnderlying Constraint.email emailSchema</code>
    /// </example>
    let fromUnderlying (constraint': Constraint<'primitive>) (schema: Schema<'value>) : 'value -> Result<unit, Violation> =
        if isNull constraint' then
            nullArg (nameof constraint')

        if isNull (box schema) then
            nullArg (nameof schema)

        let inspect = SchemaCore.inspectUnderlying<'value, 'primitive> schema
        fun value -> Constraint.check constraint' (inspect value)

    let private combine first second =
        match first, second with
        | Ok(), Ok() -> Ok()
        | Error violation, Ok()
        | Ok(), Error violation -> Error violation
        | Error first, Error second -> Error(Violation.All(first, [ second ]))

    let rec private runDefinition (definition: ValueSchemaDefinition) value =
        let own = ErasedCheck.run definition.Rules value

        let underlying =
            match definition.Shape with
            | RefinedValueDefinition(raw, ops) ->
                // A refined value already exists here, so Refinement.create does not run and cannot re-establish the
                // refinement's own invariant. Its retained constraint is typed over the underlying representation,
                // so run it against the projection alongside the raw layer's own constraints. Parsing takes a
                // different path, so this does not double-execute.
                let projected = ops.Inspect value
                combine (runDefinition raw projected) (ErasedCheck.run ops.Rules projected)
            | LazyValueDefinition deferred -> runDefinition (deferred.Force()) value
            | _ -> Ok ()

        combine underlying own

    /// <summary>Runs each complete constraint against the value at the refinement layer where it was attached.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is not a value schema.</exception>
    /// <example>
    /// <code>let checkName = SchemaCheck.complete constrainedNameSchema</code>
    /// </example>
    let complete (schema: Schema<'value>) : 'value -> Result<unit, Violation> =
        if isNull (box schema) then nullArg (nameof schema)

        match schema.Definition with
        | ValueDefinition definition -> fun value -> runDefinition definition (box value)
        | PendingDefinition
        | ModelDefinition _ -> invalidArg (nameof schema) "Expected a value schema."

    /// <summary>Runs complete constraints for a schema whose underlying primitive value is text.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when the schema's underlying primitive kind is not text.</exception>
    /// <example>
    /// <code>let checkEmail = SchemaCheck.text emailSchema</code>
    /// </example>
    let text (schema: Schema<'value>) : 'value -> Result<unit, Violation> =
        SchemaCore.inspectUnderlying<'value, string> schema |> ignore
        complete schema

    /// <summary>Runs complete constraints for a schema whose underlying primitive has the supplied ordered type.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when the ordered type does not match the underlying primitive.</exception>
    /// <example>
    /// <code>let checkAge = SchemaCheck.ordered&lt;int, int&gt; ageSchema</code>
    /// </example>
    let ordered<'primitive, 'value when 'primitive: comparison> (schema: Schema<'value>) : 'value -> Result<unit, Violation> =
        SchemaCore.inspectUnderlying<'value, 'primitive> schema |> ignore
        complete schema

/// <summary>
/// Field-constraint checking for an existing trusted model value, shared by <c>Schema.check</c>. Checks every
/// field's schema constraints but does not re-invoke the model's constructor; <c>Schema.check</c> adds that.
/// </summary>
[<RequireQualifiedAccess>]
module internal ModelFieldCheck =
    let private diagnosticsAt path error =
        SchemaErrors.singleton (Path path) error

    let private mergeErrors errors =
        SchemaErrors.collect errors

    let private allRules definition =
        let rec gather valueDefinition =
            match valueDefinition.Shape with
            | RefinedValueDefinition(raw, _) -> gather raw @ valueDefinition.Rules
            | LazyValueDefinition deferred -> gather (deferred.Force()) @ valueDefinition.Rules
            | PrimitiveValueDefinition _
            | NestedValueDefinition _
            | ManyValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _
            | EnumValueDefinition _
            | OptionValueDefinition _
            | MapValueDefinition _ -> valueDefinition.Rules

        gather definition

    let private underlyingPrimitiveKind definition =
        let rec kindOf valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition kind -> kind
            | RefinedValueDefinition(raw, _) -> kindOf raw
            | NestedValueDefinition _ -> invalidOp "Nested model value schemas have no underlying primitive kind."
            | ManyValueDefinition _ -> invalidOp "Collection value schemas have no underlying primitive kind."
            | UnionValueDefinition _ -> invalidOp "Union value schemas have no underlying primitive kind."
            | UnionInlineValueDefinition _ -> invalidOp "Union-inline value schemas have no underlying primitive kind."
            | EnumValueDefinition _ -> invalidOp "Enum value schemas have no underlying primitive kind."
            | OptionValueDefinition _ -> invalidOp "Optional value schemas have no underlying primitive kind."
            | MapValueDefinition _ -> invalidOp "Map value schemas have no underlying primitive kind."
            | LazyValueDefinition _ -> invalidOp "Deferred model value schemas have no underlying primitive kind."

        kindOf definition

    let private inspectUnderlying definition value =
        let rec project valueDefinition current =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> current
            | RefinedValueDefinition(raw, ops) -> project raw (ops.Inspect current)
            | NestedValueDefinition _ -> invalidOp "Nested model values have no underlying primitive representation."
            | ManyValueDefinition _ -> invalidOp "Collection values have no underlying primitive representation."
            | UnionValueDefinition _ -> invalidOp "Union values have no underlying primitive representation."
            | UnionInlineValueDefinition _ -> invalidOp "Union-inline values have no underlying primitive representation."
            | EnumValueDefinition _ -> invalidOp "Enum values have no underlying primitive representation."
            | OptionValueDefinition _ -> invalidOp "Optional values have no underlying primitive representation."
            | MapValueDefinition _ -> invalidOp "Map values have no underlying primitive representation."
            | LazyValueDefinition _ -> invalidOp "Deferred model values have no underlying primitive representation."

        project definition value

    let private runRules rules value =
        match ErasedCheck.run rules value with
        | Ok() -> Ok()
        | Error violation -> Error [ SchemaError.Violation violation ]

    let rec private validateValue valueSchema fieldRules path (value: obj) =
        let rules = allRules valueSchema @ fieldRules

        match valueSchema.Shape with
        | LazyValueDefinition deferred ->
            validateValue (deferred.Force()) (valueSchema.Rules @ fieldRules) path value
        | RefinedValueDefinition(raw, ops) ->
            let rawValue = ops.Inspect value
            let outerRules = valueSchema.Rules @ fieldRules
            let rawValidation = validateValue raw [] path rawValue |> SchemaResult.map (fun _ -> value)

            let refinementValidation =
                match ops.Construct rawValue with
                | Ok _ -> SchemaResult.ok value
                | Error errors ->
                    errors |> List.map (diagnosticsAt path) |> mergeErrors |> SchemaResult.error

            let constraintValidation =
                match runRules outerRules value with
                | Ok _ -> SchemaResult.ok value
                | Error errors ->
                    errors |> List.map (diagnosticsAt path) |> mergeErrors |> SchemaResult.error

            SchemaResult.map2
                (fun _ _ -> value)
                (SchemaResult.map2 (fun _ _ -> value) rawValidation refinementValidation)
                constraintValidation
        | PrimitiveValueDefinition _ ->
            let primitive = inspectUnderlying valueSchema value

            match runRules rules primitive with
            | Ok _ -> SchemaResult.ok value
            | Error errors ->
                errors
                |> List.map (diagnosticsAt path)
                |> mergeErrors
                |> SchemaResult.error
        | NestedValueDefinition(nestedModel, _) ->
            validateObject path nestedModel value
            |> SchemaResult.map (fun _ -> value)
        | ManyValueDefinition collection ->
            validateMany path collection rules value (value :?> System.Collections.IEnumerable)
            |> SchemaResult.map (fun _ -> value)
        | MapValueDefinition collection ->
            validateMap path collection rules value
            |> SchemaResult.map (fun _ -> value)
        | UnionValueDefinition union ->
            validateUnion path union value
            |> SchemaResult.map (fun _ -> value)
        | UnionInlineValueDefinition union ->
            validateUnionInline path union value
            |> SchemaResult.map (fun _ -> value)
        | EnumValueDefinition enum ->
            validateEnum path enum value
            |> SchemaResult.map (fun _ -> value)
        | OptionValueDefinition optional ->
            match optional.TryUnwrap value with
            | None -> SchemaResult.ok value
            | Some payload ->
                validateValue optional.Payload (valueSchema.Rules @ fieldRules) path payload
                |> SchemaResult.map (fun _ -> value)

    and private validateField basePath model (field: FieldDescriptor<obj>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = basePath @ [ KeyComponent name ]
        let value = field.Getter model
        validateValue field.ValueSchema field.Rules path value

    and private validateObject path (modelSchema: ModelSchemaDefinition<obj>) model =
        let validatedFields = modelSchema.Fields |> List.map (validateField path model)
        let errors =
            validatedFields
            |> List.choose (fun validation ->
                match SchemaResult.toResult validation with
                | Ok _ -> None
                | Error diagnostics -> Some diagnostics)

        match errors with
        | [] -> SchemaResult.ok model
        | diagnostics -> diagnostics |> mergeErrors |> SchemaResult.error

    and private checkMany rules path value =
        match runRules rules value with
        | Ok checkedValue -> SchemaResult.ok checkedValue
        | Error errors ->
            errors
            |> List.map (diagnosticsAt path)
            |> mergeErrors
            |> SchemaResult.error

    and private validateMany path (collection: CollectionValueDefinition) rules value (items: System.Collections.IEnumerable) =
        let items = items |> Seq.cast<obj> |> Seq.toList

        let validatedItems =
            items
            |> List.mapi (fun index item -> validateValue collection.Item [] (path @ [ IndexComponent index ]) item)

        let errors =
            validatedItems
            |> List.choose (fun validation ->
                match SchemaResult.toResult validation with
                | Ok _ -> None
                | Error diagnostics -> Some diagnostics)

        match errors with
        | [] -> checkMany rules path value
        | diagnostics -> diagnostics |> mergeErrors |> SchemaResult.error

    and private validateMap path (collection: MapValueDefinition) rules (value: obj) =
        let entries = collection.Entries value

        let validatedEntries =
            entries
            |> List.map (fun (key, item) -> validateValue collection.Item [] (path @ [ KeyComponent key ]) item)

        let errors =
            validatedEntries
            |> List.choose (fun validation ->
                match SchemaResult.toResult validation with
                | Ok _ -> None
                | Error diagnostics -> Some diagnostics)

        match errors with
        | [] -> checkMany rules path value
        | diagnostics -> diagnostics |> mergeErrors |> SchemaResult.error

    and private validateUnion path (union: TaggedUnionValueDefinition) value =
        let payloadName = ExternalFieldName.value union.PayloadField
        let payloadPath = path @ [ KeyComponent payloadName ]

        match union.Cases |> List.tryPick (fun case -> case.TryInspect value |> Option.map (fun payload -> case, payload)) with
        | Some(case, payload) ->
            validateValue case.Payload [] payloadPath payload
        | None ->
            SchemaError.Custom("union.case", Some "The value did not match any configured union case.")
            |> diagnosticsAt path
            |> SchemaResult.error

    and private validateUnionInline path (union: InlineTaggedUnionValueDefinition) value =
        match union.Cases |> List.tryPick (fun case -> case.TryInspect value |> Option.map (fun payload -> case, payload)) with
        | Some(case, payload) -> validateValue case.Payload [] path payload
        | None ->
            SchemaError.Custom("union.case", Some "The value did not match any configured union case.")
            |> diagnosticsAt path
            |> SchemaResult.error

    and private validateEnum path (enum: TaggedEnumValueDefinition) (value: obj) =
        if enum.Cases |> List.exists (fun case -> case.Value.Equals value) then
            SchemaResult.ok value
        else
            SchemaError.Custom("enum.case", Some "The value did not match any configured enum case.")
            |> diagnosticsAt path
            |> SchemaResult.error

    let private validateRootField model (field: FieldDescriptor<'model>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = [ KeyComponent name ]
        let value = field.Getter model
        validateValue field.ValueSchema field.Rules path value

    /// <summary>Checks an existing trusted model value's field constraints through a built model schema.</summary>
    /// <remarks>
    /// Reads values with schema getters, runs schema constraints through the same execution path used by input
    /// parsing, and recursively checks nested models and collection items. Does not re-invoke the model's constructor; <c>Schema.check</c> does that
    /// separately once every field's constraints have passed.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is not a built model schema.</exception>
    let check (schema: Schema<'model>) (model: 'model) : Result<'model, SchemaErrors> =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition with
        | PendingDefinition -> invalidArg (nameof schema) "Expected a built model schema."
        | ValueDefinition valueSchema ->
            validateValue valueSchema [] [] (box model)
            |> SchemaResult.map unbox<'model>
        | ModelDefinition modelSchema ->
            let validatedFields = modelSchema.Fields |> List.map (validateRootField model)
            let errors =
                validatedFields
                |> List.choose (fun validation ->
                    match SchemaResult.toResult validation with
                    | Ok _ -> None
                    | Error diagnostics -> Some diagnostics)

            match errors with
            | [] -> SchemaResult.ok model
            | diagnostics -> diagnostics |> mergeErrors |> SchemaResult.error
