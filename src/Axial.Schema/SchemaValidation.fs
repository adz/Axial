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
/// Supply declarations such as <c>omittable</c> have no value check. Length metadata is adapted to the
/// concrete collection shape selected by Schema.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module ConstraintCheck =
    let internal ensureConstraint (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')

    let internal ensureConstraints constraints =
        if isNull (box constraints) then nullArg (nameof constraints)
        let constraints = constraints |> Seq.toList
        constraints |> List.iter ensureConstraint
        constraints

    let private retained<'value> constraint' = Constraint.tryCheck<'value> constraint'

    /// <summary>Combines every complete constraint retained for the supplied value type.</summary>
    /// <example>
    /// <code>let check = ConstraintCheck.complete&lt;int&gt; descriptors</code>
    /// </example>
    let complete<'value> constraints : Check<'value> =
        ensureConstraints constraints |> List.choose retained<'value> |> Check.all

    /// <summary>Returns the retained text check when the descriptor applies to text.</summary>
    /// <example>
    /// <code>let check = ConstraintCheck.tryText descriptor</code>
    /// </example>
    let tryText (constraint': ConstraintDescriptor) : Check<string> option =
        ensureConstraint constraint'
        match Constraint.metadata constraint' with
        | ConstraintMetadata.Supply _ -> None
        | ConstraintMetadata.ValueConstraint metadata ->
            match metadata with
            | Axial.Check.ConstraintMetadata.Present
            | Axial.Check.ConstraintMetadata.Length _
            | Axial.Check.ConstraintMetadata.MinLength _
            | Axial.Check.ConstraintMetadata.MaxLength _
            | Axial.Check.ConstraintMetadata.LengthBetween _
            | Axial.Check.ConstraintMetadata.Email
            | Axial.Check.ConstraintMetadata.Trimmed
            | Axial.Check.ConstraintMetadata.Pattern _
            | Axial.Check.ConstraintMetadata.OneOf _
            | Axial.Check.ConstraintMetadata.EqualTo _
            | Axial.Check.ConstraintMetadata.NotEqualTo _
            | Axial.Check.ConstraintMetadata.Custom _ -> retained constraint'
            | _ -> None

    /// <summary>Combines the retained value checks that apply to text.</summary>
    /// <example>
    /// <code>let check = ConstraintCheck.text descriptors</code>
    /// </example>
    let text constraints =
        ensureConstraints constraints |> List.choose tryText |> Check.all

    /// <summary>Returns the retained ordered-value check when the descriptor applies to the supplied value type.</summary>
    /// <example>
    /// <code>let check = ConstraintCheck.tryOrdered&lt;int&gt; descriptor</code>
    /// </example>
    let tryOrdered<'value when 'value: comparison> (constraint': ConstraintDescriptor) : Check<'value> option =
        ensureConstraint constraint'
        match Constraint.metadata constraint' with
        | ConstraintMetadata.ValueConstraint metadata ->
            match metadata with
            | Axial.Check.ConstraintMetadata.EqualTo _
            | Axial.Check.ConstraintMetadata.NotEqualTo _
            | Axial.Check.ConstraintMetadata.Between _
            | Axial.Check.ConstraintMetadata.GreaterThan _
            | Axial.Check.ConstraintMetadata.LessThan _
            | Axial.Check.ConstraintMetadata.AtLeast _
            | Axial.Check.ConstraintMetadata.AtMost _
            | Axial.Check.ConstraintMetadata.Custom _ -> retained constraint'
            | _ -> None
        | ConstraintMetadata.Supply _ -> None

    /// <summary>Combines retained ordered-value checks for the supplied value type.</summary>
    /// <example>
    /// <code>let check = ConstraintCheck.ordered&lt;int&gt; descriptors</code>
    /// </example>
    let ordered<'value when 'value: comparison> constraints =
        ensureConstraints constraints |> List.choose tryOrdered<'value> |> Check.all

    let inline internal tryMultipleOf constraint' =
        ensureConstraint constraint'
        match Constraint.metadata constraint' with
        | ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MultipleOf _) -> retained constraint'
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
    /// <example>
    /// <code>let checkEmail = SchemaCheck.fromUnderlying Check.String.email emailSchema</code>
    /// </example>
    let fromUnderlying (check: Check<'primitive>) (schema: Schema<'value>) : Check<'value> =
        if isNull (box check) then
            nullArg (nameof check)

        if isNull (box schema) then
            nullArg (nameof schema)

        let inspect = SchemaCore.inspectUnderlying<'value, 'primitive> schema
        fun value -> check (inspect value)

    let private combine first second =
        match first, second with
        | Ok (), Ok () -> Ok ()
        | Error failures, Ok ()
        | Ok (), Error failures -> Error failures
        | Error firstFailures, Error secondFailures -> Error(firstFailures @ secondFailures)

    let rec private runDefinition (definition: ValueSchemaDefinition) value =
        let own = ConstraintCheck.complete<obj> definition.Constraints value

        let underlying =
            match definition.Shape with
            | RefinedValueDefinition(raw, ops) ->
                // A refined value already exists here, so Refinement.create does not run and cannot re-establish the
                // refinement's own invariant. Its retained constraints are typed over the underlying representation,
                // so run them against the projection alongside the raw layer's own constraints. Parsing takes a
                // different path (ConstraintCheck over definition.Constraints), so this does not double-execute.
                let projected = ops.Inspect value
                combine (runDefinition raw projected) (ConstraintCheck.complete<obj> ops.Constraints projected)
            | LazyValueDefinition deferred -> runDefinition (deferred.Force()) value
            | _ -> Ok ()

        combine underlying own

    /// <summary>Runs each complete constraint against the value at the refinement layer where it was attached.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is not a value schema.</exception>
    /// <example>
    /// <code>let checkName = SchemaCheck.complete constrainedNameSchema</code>
    /// </example>
    let complete (schema: Schema<'value>) : Check<'value> =
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
    let text (schema: Schema<'value>) : Check<'value> =
        SchemaCore.inspectUnderlying<'value, string> schema |> ignore
        complete schema

    /// <summary>Runs complete constraints for a schema whose underlying primitive has the supplied ordered type.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when the ordered type does not match the underlying primitive.</exception>
    /// <example>
    /// <code>let checkAge = SchemaCheck.ordered&lt;int, int&gt; ageSchema</code>
    /// </example>
    let ordered<'primitive, 'value when 'primitive: comparison> (schema: Schema<'value>) : Check<'value> =
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
            value |> unbox<string> |> runCheck constraints (ConstraintCheck.complete<string> constraints) |> Result.map box
        | PrimitiveValueKind.Int -> runComplete<int> constraints value
        | PrimitiveValueKind.Int64 -> runComplete<int64> constraints value
        | PrimitiveValueKind.Decimal -> runComplete<decimal> constraints value
        | PrimitiveValueKind.Float -> runComplete<float> constraints value
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
