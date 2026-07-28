// Constraint execution: selects complete Check constraints by value shape and runs them at parse/check time.
// Parsing.fs drives this per field; it has no knowledge of input sources or whole-model construction.
namespace Axial.Schema

open System
open Axial.Check
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

/// <summary>Functions for selecting and combining the executable checks retained by Schema constraints.</summary>
/// <remarks>
/// <para>
/// Value constraints enter Schema as complete <c>Axial.Check.Constraint&lt;'value&gt;</c> values. This module selects
/// constraints that apply to text, ordered, numeric, or sequence values and combines their retained checks. Stable
/// codes identify constraints externally; they are not executable dispatch keys.
/// </para>
/// <para>
/// Boundary presence declarations such as <c>optional</c> have no value check. Sequence metadata is adapted to the
/// concrete collection shape selected by Schema.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module ConstraintCheck =
    let internal ensureConstraint (constraint': Constraint) =
        if isNull constraint' then nullArg (nameof constraint')

    let internal ensureConstraints constraints =
        if isNull (box constraints) then nullArg (nameof constraints)
        let constraints = constraints |> Seq.toList
        constraints |> List.iter ensureConstraint
        constraints

    let private retained<'value> constraint' = Constraint.tryCheck<'value> constraint'

    /// Combines every complete constraint retained for the supplied value type. Presence declarations have no check.
    let complete<'value> constraints : Check<'value> =
        ensureConstraints constraints |> List.choose retained<'value> |> Check.all

    let tryText (constraint': Constraint) : Check<string> option =
        ensureConstraint constraint'
        match Constraint.metadata constraint' with
        | ConstraintMetadata.Required -> Some Check.String.present
        | ConstraintMetadata.MinLength _
        | ConstraintMetadata.MaxLength _
        | ConstraintMetadata.LengthBetween _
        | ConstraintMetadata.Email
        | ConstraintMetadata.Trimmed
        | ConstraintMetadata.Pattern _
        | ConstraintMetadata.OneOf _
        | ConstraintMetadata.EqualTo _
        | ConstraintMetadata.NotEqualTo _
        | ConstraintMetadata.Custom _ -> retained constraint'
        | _ -> None

    let text constraints =
        ensureConstraints constraints |> List.choose tryText |> Check.all

    let tryOrdered<'value when 'value: comparison> (constraint': Constraint) : Check<'value> option =
        ensureConstraint constraint'
        match Constraint.metadata constraint' with
        | ConstraintMetadata.EqualTo _
        | ConstraintMetadata.NotEqualTo _
        | ConstraintMetadata.Between _
        | ConstraintMetadata.GreaterThan _
        | ConstraintMetadata.LessThan _
        | ConstraintMetadata.AtLeast _
        | ConstraintMetadata.AtMost _
        | ConstraintMetadata.Custom _ -> retained constraint'
        | _ -> None

    let ordered<'value when 'value: comparison> constraints =
        ensureConstraints constraints |> List.choose tryOrdered<'value> |> Check.all

    let inline internal tryMultipleOf constraint' =
        ensureConstraint constraint'
        match Constraint.metadata constraint' with
        | ConstraintMetadata.MultipleOf _ -> retained constraint'
        | _ -> None

    let inline internal multipleOf constraints =
        ensureConstraints constraints |> List.choose tryMultipleOf |> Check.all

module internal SchemaCheckFailure =
    let private tryCustomMessage constraints code =
        constraints
        |> List.tryFind (fun constraint' -> Constraint.code constraint' = code)
        |> Option.bind Constraint.message

    let private withCustomMessage constraints code error =
        match tryCustomMessage constraints code with
        | Some message -> SchemaError.Custom(code, Some message)
        | None -> error

    let withCustomMessageForCode constraints code error =
        withCustomMessage constraints code error

    let toSchemaError constraints failure =
        let error = SchemaError.ofCheckFailure failure

        match SchemaError.constraintCodeFor failure with
        | Some code -> withCustomMessage constraints code error
        | None -> error

    let toSchemaErrors constraints failures =
        failures |> List.map (toSchemaError constraints)

/// <summary>Functions for running executable value checks against refined and primitive value schemas.</summary>
/// <remarks>
/// <para>
/// Refined value schemas describe named domain values, such as an <c>Email</c> refined over raw text, while their
/// executable constraints are expressed against the underlying primitive representation. This interpreter runs
/// <see cref="T:Axial.Check.Check`1" /> programs against a schema's values by projecting each trusted value
/// through the schema's refinement layers with <see cref="M:Axial.Schema.Schema.inspectUnderlying``2" /> and running
/// the primitive-level check on the result. Primitive value schemas work the same way with an identity projection.
/// </para>
/// <para>
/// The metadata lowerers gather constraint metadata from every refinement layer with
/// <see cref="M:Axial.Schema.Schema.allConstraints``1" /> and lower it through
/// <see cref="T:Axial.Schema.ConstraintCheck" />, so raw-layer and refined-layer constraints run as
/// one check program.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module SchemaCheck =
    /// <summary>
    /// Adapts a check over a schema's underlying primitive representation into a check over the schema's values.
    /// </summary>
    /// <remarks>
    /// This is the general adapter for arbitrary <see cref="T:Axial.Check.Check`1" /> programs, including
    /// programs composed with <c>Check.all</c>, <c>Check.any</c>, and <c>Check.not</c>. The projection to the
    /// underlying primitive representation is created eagerly, so a projection type that does not match the schema's
    /// underlying primitive kind fails here rather than on each checked value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="check" /> or <paramref name="schema" /> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the check's value type does not match the schema's underlying primitive kind.
    /// </exception>
    let fromUnderlying (check: Check<'primitive>) (schema: Schema<'value>) : Check<'value> =
        if isNull (box check) then
            nullArg (nameof check)

        if isNull (box schema) then
            nullArg (nameof schema)

        let inspect = SchemaCore.inspectUnderlying<'value, 'primitive> schema
        fun value -> check (inspect value)

    /// <summary>
    /// Lowers the text-meaning constraint metadata carried by every layer of a value schema into one executable check
    /// over the schema's values.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the schema's underlying primitive kind is not text.
    /// </exception>
    let text (schema: Schema<'value>) : Check<'value> =
        if isNull (box schema) then
            nullArg (nameof schema)

        fromUnderlying (ConstraintCheck.text (SchemaCore.allConstraints schema)) schema

    /// <summary>
    /// Lowers the range-meaning constraint metadata carried by every layer of a value schema into one executable check
    /// over the schema's values.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the ordered primitive type does not match the schema's underlying primitive kind.
    /// </exception>
    let ordered<'primitive, 'value when 'primitive: comparison> (schema: Schema<'value>) : Check<'value> =
        if isNull (box schema) then
            nullArg (nameof schema)

        fromUnderlying (ConstraintCheck.ordered<'primitive> (SchemaCore.allConstraints schema)) schema

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

    let private allConstraints definition =
        let rec gather valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> valueDefinition.Constraints
            | RefinedValueDefinition(raw, _) -> gather raw @ valueDefinition.Constraints
            | NestedValueDefinition _ -> valueDefinition.Constraints
            | ManyValueDefinition _ -> valueDefinition.Constraints
            | UnionValueDefinition _ -> valueDefinition.Constraints
            | UnionInlineValueDefinition _ -> valueDefinition.Constraints
            | EnumValueDefinition _ -> valueDefinition.Constraints
            | OptionValueDefinition _ -> valueDefinition.Constraints
            | MapValueDefinition _ -> valueDefinition.Constraints
            | LazyValueDefinition deferred -> gather (deferred.Force()) @ valueDefinition.Constraints

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

    let private runCheck constraints check value =
        match check value with
        | Ok _ -> Ok ()
        | Error failures -> failures |> SchemaCheckFailure.toSchemaErrors constraints |> Error

    let private runComplete<'value> constraints value =
        value
        |> unbox<'value>
        |> runCheck constraints (ConstraintCheck.complete<'value> constraints)
        |> Result.map box

    let private checkPrimitive kind constraints value =
        match kind with
        | PrimitiveValueKind.Text ->
            let presence =
                if constraints |> List.exists (Constraint.metadata >> (=) ConstraintMetadata.Required) then
                    Check.String.present
                else
                    fun _ -> Ok ()

            let check = Check.all [ presence; ConstraintCheck.complete<string> constraints ]
            value |> unbox<string> |> runCheck constraints check |> Result.map box
        | PrimitiveValueKind.Int -> runComplete<int> constraints value
        | PrimitiveValueKind.Decimal -> runComplete<decimal> constraints value
        | PrimitiveValueKind.Bool -> runComplete<bool> constraints value
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date -> runComplete<DateOnly> constraints value
#else
        | PrimitiveValueKind.Date -> Ok ()
#endif
        | PrimitiveValueKind.DateTime -> runComplete<DateTimeOffset> constraints value
        | PrimitiveValueKind.Guid -> runComplete<Guid> constraints value

    let rec private validateValue valueSchema fieldConstraints path (value: obj) =
        let constraints = allConstraints valueSchema @ fieldConstraints

        match valueSchema.Shape with
        | LazyValueDefinition deferred ->
            validateValue (deferred.Force()) (valueSchema.Constraints @ fieldConstraints) path value
        | RefinedValueDefinition(raw, ops) ->
            let rawValue = ops.Inspect value
            let outerConstraints = valueSchema.Constraints @ fieldConstraints
            let rawValidation = validateValue raw [] path rawValue |> SchemaResult.map (fun _ -> value)

            let refinementValidation =
                match ops.Construct rawValue with
                | Ok _ -> SchemaResult.ok value
                | Error errors ->
                    errors |> List.map (diagnosticsAt path) |> mergeErrors |> SchemaResult.error

            let constraintValidation =
                match value |> runCheck outerConstraints (ConstraintCheck.complete<obj> outerConstraints) with
                | Ok _ -> SchemaResult.ok value
                | Error errors ->
                    errors |> List.map (diagnosticsAt path) |> mergeErrors |> SchemaResult.error

            SchemaResult.map2
                (fun _ _ -> value)
                (SchemaResult.map2 (fun _ _ -> value) rawValidation refinementValidation)
                constraintValidation
        | PrimitiveValueDefinition _ ->
            let kind = underlyingPrimitiveKind valueSchema
            let primitive = inspectUnderlying valueSchema value

            match checkPrimitive kind constraints primitive with
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
            validateMany path collection constraints value (value :?> System.Collections.IEnumerable)
            |> SchemaResult.map (fun _ -> value)
        | MapValueDefinition collection ->
            validateMap path collection constraints value
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
                validateValue optional.Payload (valueSchema.Constraints @ fieldConstraints) path payload
                |> SchemaResult.map (fun _ -> value)

    and private validateField basePath model (field: FieldDescriptor<obj>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = basePath @ [ KeyComponent name ]
        let value = field.Getter model
        validateValue field.ValueSchema field.Constraints path value

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

    and private checkMany constraints path value =
        match value |> runCheck constraints (ConstraintCheck.complete<obj> constraints) with
        | Ok checkedValue -> SchemaResult.ok checkedValue
        | Error errors ->
            errors
            |> List.map (diagnosticsAt path)
            |> mergeErrors
            |> SchemaResult.error

    and private validateMany path (collection: CollectionValueDefinition) constraints value (items: System.Collections.IEnumerable) =
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
        | [] -> checkMany constraints path value
        | diagnostics -> diagnostics |> mergeErrors |> SchemaResult.error

    and private validateMap path (collection: MapValueDefinition) constraints (value: obj) =
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
        | [] -> checkMany constraints path value
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
        validateValue field.ValueSchema field.Constraints path value

    /// <summary>Checks an existing trusted model value's field constraints through a built model schema.</summary>
    /// <remarks>
    /// Reads values with schema getters, runs schema constraints through the same executable
    /// <see cref="T:Axial.Check.Check`1" /> lowering used by input parsing, and recursively checks nested
    /// models and collection items. Does not re-invoke the model's constructor; <c>Schema.check</c> does that
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
