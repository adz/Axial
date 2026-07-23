// Constraint interpretation: gives each portable Constraint (Constraints.fs) its runtime meaning at
// parse/check time, plus the failure vocabulary those checks report. Parsing.fs drives this per
// field; it has no knowledge of input sources or of whole-model construction.
namespace Axial.Schema

open System
open Axial.ErrorHandling
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

/// <summary>Functions for lowering portable schema constraint metadata to executable value checks.</summary>
/// <remarks>
/// <para>
/// Schema constraints stay inspectable in <c>Axial.Schema</c>. This integration module turns the subset that has
/// value-level meaning into path-free <see cref="T:Axial.ErrorHandling.Check`1" /> programs.
/// </para>
/// <para>
/// Constraints such as <c>optional</c> remain metadata-only. Constraints that belong to another value shape return
/// <c>None</c> from the per-constraint lowerers and are ignored by the list lowerers.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module ConstraintCheck =
    let internal ensureConstraint (constraint': Constraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

    let internal ensureConstraints constraints =
        if isNull (box constraints) then
            nullArg (nameof constraints)

        let constraints = constraints |> Seq.toList

        constraints
        |> List.iter (fun constraint' ->
            if isNull constraint' then
                nullArg (nameof constraints))

        constraints

    let internal tryArgument<'value> name constraint' =
        match Constraint.tryFindArgument name constraint' with
        | Some (:? 'value as value) -> Some value
        | _ -> None

    let private tryBounds<'value> constraint' =
        match tryArgument<'value> "minimum" constraint', tryArgument<'value> "maximum" constraint' with
        | Some minimum, Some maximum -> Some(minimum, maximum)
        | _ -> None

    /// <summary>Lowers one schema constraint to a string check when the constraint has text-level meaning.</summary>
    let tryText (constraint': Constraint) : Check<string> option =
        ensureConstraint constraint'

        match Constraint.code constraint' with
        | "required" -> Some Check.String.present
        | "minLength" -> tryArgument<int> "minimum" constraint' |> Option.map Check.String.minLength
        | "maxLength" -> tryArgument<int> "maximum" constraint' |> Option.map Check.String.maxLength
        | "lengthBetween" ->
            tryBounds<int> constraint'
            |> Option.map (fun (minimum, maximum) -> Check.String.lengthBetween minimum maximum)
        | "email" -> Some Check.String.email
        | "trimmed" ->
            Some(fun value ->
                if isNull value then
                    Error [ CheckFailure.Required ]
                elif value.Trim() = value then
                    Ok value
                else
                    Error [ CheckFailure.InvalidFormat "trimmed" ])
        | "pattern" -> tryArgument<string> "pattern" constraint' |> Option.map Check.String.matches
        | "oneOf" ->
            tryArgument<string array> "choices" constraint'
            |> Option.map (fun choices -> Check.String.oneOf choices)
        | "notEqualTo" -> tryArgument<string> "unexpected" constraint' |> Option.map Check.notEqualTo
        | _ -> None

    /// <summary>Lowers schema constraints with text-level meaning into one string check.</summary>
    let text (constraints: Constraint seq) : Check<string> =
        ensureConstraints constraints
        |> Seq.choose tryText
        |> Seq.toList
        |> Check.all

    let private betweenCheck minimum maximum : Check<'value> =
        fun value ->
            if value >= minimum && value <= maximum then
                Ok value
            else
                Error [ CheckFailure.OutOfRange(CheckRangeExpectation.Between(string minimum, string maximum), Some(string value)) ]

    let private greaterThanCheck minimum : Check<'value> =
        fun value ->
            if value > minimum then
                Ok value
            else
                Error [ CheckFailure.OutOfRange(CheckRangeExpectation.GreaterThan(string minimum), Some(string value)) ]

    let private lessThanCheck maximum : Check<'value> =
        fun value ->
            if value < maximum then
                Ok value
            else
                Error [ CheckFailure.OutOfRange(CheckRangeExpectation.LessThan(string maximum), Some(string value)) ]

    let private atLeastCheck minimum : Check<'value> =
        fun value ->
            if value >= minimum then
                Ok value
            else
                Error [ CheckFailure.OutOfRange(CheckRangeExpectation.AtLeast(string minimum), Some(string value)) ]

    let private atMostCheck maximum : Check<'value> =
        fun value ->
            if value <= maximum then
                Ok value
            else
                Error [ CheckFailure.OutOfRange(CheckRangeExpectation.AtMost(string maximum), Some(string value)) ]

    /// <summary>Lowers one schema constraint to an ordered-value check when the constraint has range-level meaning.</summary>
    let tryOrdered<'value when 'value: comparison> (constraint': Constraint) : Check<'value> option =
        ensureConstraint constraint'

        match Constraint.code constraint' with
        | "between" ->
            tryBounds<'value> constraint'
            |> Option.map (fun (minimum, maximum) -> betweenCheck minimum maximum)
        | "greaterThan" -> tryArgument<'value> "minimum" constraint' |> Option.map greaterThanCheck
        | "lessThan" -> tryArgument<'value> "maximum" constraint' |> Option.map lessThanCheck
        | "atLeast" -> tryArgument<'value> "minimum" constraint' |> Option.map atLeastCheck
        | "atMost" -> tryArgument<'value> "maximum" constraint' |> Option.map atMostCheck
        | "notEqualTo" -> tryArgument<'value> "unexpected" constraint' |> Option.map Check.notEqualTo
        | _ -> None

    /// <summary>Lowers schema constraints with range-level meaning into one ordered-value check.</summary>
    let ordered<'value when 'value: comparison> (constraints: Constraint seq) : Check<'value> =
        ensureConstraints constraints
        |> Seq.choose tryOrdered<'value>
        |> Seq.toList
        |> Check.all

    let inline internal multipleOfCheck<'value when 'value: (static member Zero: 'value) and 'value: equality and 'value: (static member (%) : 'value * 'value -> 'value)>
        (divisor: 'value)
        : Check<'value> =
        fun value ->
            if value % divisor = LanguagePrimitives.GenericZero<'value> then
                Ok value
            else
                Error [ CheckFailure.OutOfRange(CheckRangeExpectation.NotMultipleOf(string divisor), Some(string value)) ]

    /// <summary>Lowers one schema constraint to a multiple-of check when the constraint has that meaning.</summary>
    let inline internal tryMultipleOf<'value when 'value: (static member Zero: 'value) and 'value: equality and 'value: (static member (%) : 'value * 'value -> 'value)>
        (constraint': Constraint)
        : Check<'value> option =
        ensureConstraint constraint'

        match Constraint.code constraint' with
        | "multipleOf" -> tryArgument<'value> "divisor" constraint' |> Option.map multipleOfCheck<'value>
        | _ -> None

    /// <summary>Lowers schema constraints with multiple-of meaning into one numeric check.</summary>
    let inline internal multipleOf<'value when 'value: (static member Zero: 'value) and 'value: equality and 'value: (static member (%) : 'value * 'value -> 'value)>
        (constraints: Constraint seq)
        : Check<'value> =
        ensureConstraints constraints
        |> Seq.choose tryMultipleOf<'value>
        |> Seq.toList
        |> Check.all

    /// <summary>Lowers one schema constraint to a sequence check when the constraint has sequence-level meaning.</summary>
    let trySequence<'value when 'value: equality> (constraint': Constraint) : Check<seq<'value>> option =
        ensureConstraint constraint'

        match Constraint.code constraint' with
        | "count" -> tryArgument<int> "expected" constraint' |> Option.map Check.Seq.count
        | "minCount" -> tryArgument<int> "minimum" constraint' |> Option.map Check.Seq.minCount
        | "maxCount" -> tryArgument<int> "maximum" constraint' |> Option.map Check.Seq.maxCount
        | "countBetween" ->
            tryBounds<int> constraint'
            |> Option.map (fun (minimum, maximum) -> Check.Seq.countBetween minimum maximum)
        | "distinct" -> Some Check.Seq.noDuplicates
        | "contains" -> tryArgument<'value> "item" constraint' |> Option.map Check.Seq.contains
        | _ -> None

    /// <summary>Lowers schema constraints with sequence-level meaning into one sequence check.</summary>
    let sequence<'value when 'value: equality> (constraints: Constraint seq) : Check<seq<'value>> =
        ensureConstraints constraints
        |> Seq.choose trySequence<'value>
        |> Seq.toList
        |> Check.all

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
/// <see cref="T:Axial.ErrorHandling.Check`1" /> programs against a schema's values by projecting each trusted value
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
    /// This is the general adapter for arbitrary <see cref="T:Axial.ErrorHandling.Check`1" /> programs, including
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
        fun value -> check (inspect value) |> Result.map (fun _ -> value)

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
        | Ok _ -> Ok value
        | Error failures -> failures |> SchemaCheckFailure.toSchemaErrors constraints |> Error

    let private checkPrimitive kind constraints value =
        match kind with
        | PrimitiveValueKind.Text ->
            value
            |> unbox<string>
            |> runCheck constraints (ConstraintCheck.text constraints)
            |> Result.map box
        | PrimitiveValueKind.Int ->
            value
            |> unbox<int>
            |> runCheck constraints (fun v -> Check.all [ ConstraintCheck.ordered<int> constraints; ConstraintCheck.multipleOf<int> constraints ] v)
            |> Result.map box
        | PrimitiveValueKind.Decimal ->
            value
            |> unbox<decimal>
            |> runCheck constraints (fun v -> Check.all [ ConstraintCheck.ordered<decimal> constraints; ConstraintCheck.multipleOf<decimal> constraints ] v)
            |> Result.map box
        | PrimitiveValueKind.Bool -> Ok value
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date ->
            value
            |> unbox<DateOnly>
            |> runCheck constraints (ConstraintCheck.ordered<DateOnly> constraints)
            |> Result.map box
#else
        | PrimitiveValueKind.Date -> Ok value
#endif
        | PrimitiveValueKind.DateTime ->
            value
            |> unbox<DateTimeOffset>
            |> runCheck constraints (ConstraintCheck.ordered<DateTimeOffset> constraints)
            |> Result.map box
        | PrimitiveValueKind.Guid -> Ok value

    let rec private validateValue valueSchema fieldConstraints path (value: obj) =
        let constraints = allConstraints valueSchema @ fieldConstraints

        match valueSchema.Shape with
        | LazyValueDefinition deferred ->
            validateValue (deferred.Force()) (valueSchema.Constraints @ fieldConstraints) path value
        | RefinedValueDefinition(raw, ops) ->
            let rawValidation =
                match raw.Shape with
                | NestedValueDefinition _
                | ManyValueDefinition _
                | UnionValueDefinition _
                | UnionInlineValueDefinition _
                | EnumValueDefinition _
                | OptionValueDefinition _
                | MapValueDefinition _
                | LazyValueDefinition _ ->
                    validateValue raw (valueSchema.Constraints @ fieldConstraints) path (ops.Inspect value)
                    |> SchemaResult.map (fun _ -> value)
                | PrimitiveValueDefinition _
                | RefinedValueDefinition _ ->
                    let kind = underlyingPrimitiveKind valueSchema
                    let primitive = inspectUnderlying valueSchema value

                    match checkPrimitive kind constraints primitive with
                    | Ok _ -> SchemaResult.ok value
                    | Error errors ->
                        errors
                        |> List.map (diagnosticsAt path)
                        |> mergeErrors
                        |> SchemaResult.error

            let refinementValidation =
                match ops.Construct(ops.Inspect value) with
                | Ok _ -> SchemaResult.ok value
                | Error errors ->
                    errors
                    |> List.map (diagnosticsAt path)
                    |> mergeErrors
                    |> SchemaResult.error

            SchemaResult.map2
                (fun _ _ -> value)
                rawValidation
                refinementValidation
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
            validateMany path collection constraints (value :?> System.Collections.IEnumerable)
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

    and private checkMany constraints path items =
        match items |> runCheck constraints (ConstraintCheck.sequence<obj> constraints) with
        | Ok checkedItems -> SchemaResult.ok checkedItems
        | Error errors ->
            errors
            |> List.map (diagnosticsAt path)
            |> mergeErrors
            |> SchemaResult.error

    and private validateMany path (collection: CollectionValueDefinition) constraints (items: System.Collections.IEnumerable) =
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
        | [] -> checkMany constraints path items
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
        | [] -> checkMany constraints path (entries |> List.map snd)
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
    /// <see cref="T:Axial.ErrorHandling.Check`1" /> lowering used by input parsing, and recursively checks nested
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
