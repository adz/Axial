namespace Axial.Validation.Schema

open System
open Axial.ErrorHandling
open Axial.Refined
open Axial.Schema
open Axial.Validation

/// <summary>Schema input, model validation, and contextual rule failures attached to diagnostics paths.</summary>
[<RequireQualifiedAccess>]
type SchemaError =
    /// <summary>A required boundary value was missing.</summary>
    | Required
    /// <summary>The raw input value was expected to be a scalar.</summary>
    | ExpectedScalar
    /// <summary>The raw input value was expected to be an object.</summary>
    | ExpectedObject
    /// <summary>The raw input value was expected to be a collection.</summary>
    | ExpectedMany
    /// <summary>The scalar text did not match the expected format.</summary>
    | InvalidFormat of expected: string
    /// <summary>The scalar text was outside the supported range for the target type.</summary>
    | OutOfRange of target: string
    /// <summary>The value was shorter than required.</summary>
    | TooShort of minimum: int * actual: int option
    /// <summary>The value was longer than allowed.</summary>
    | TooLong of maximum: int * actual: int option
    /// <summary>The value length was outside the required inclusive bounds.</summary>
    | LengthOutOfRange of minimum: int * maximum: int * actual: int option
    /// <summary>The value was outside the required ordered range.</summary>
    | RangeOutOfRange of expectation: string * actual: string option
    /// <summary>The collection count was outside the required count range.</summary>
    | CountOutOfRange of expectation: string * actual: int option
    /// <summary>The value was not one of the expected choices.</summary>
    | NotOneOf of choices: string
    /// <summary>A duplicate value was found.</summary>
    | Duplicate
    /// <summary>A trusted model constructor rejected otherwise-valid field values.</summary>
    | ConstructorFailed of message: string
    /// <summary>A custom schema failure code, with an optional custom message.</summary>
    | Custom of code: string * message: string option

/// <summary>Functions for lowering and rendering boundary schema failures.</summary>
[<RequireQualifiedAccess>]
module SchemaError =
    let private rangeText expectation =
        match expectation with
        | CheckRangeExpectation.GreaterThan minimum -> $"greaterThan {minimum}"
        | CheckRangeExpectation.LessThan maximum -> $"lessThan {maximum}"
        | CheckRangeExpectation.AtLeast minimum -> $"atLeast {minimum}"
        | CheckRangeExpectation.AtMost maximum -> $"atMost {maximum}"
        | CheckRangeExpectation.Between(minimum, maximum) -> $"between {minimum} {maximum}"

    let private countText expectation =
        match expectation with
        | CheckCountExpectation.MinimumCount minimum -> $"minCount {minimum}"
        | CheckCountExpectation.MaximumCount maximum -> $"maxCount {maximum}"
        | CheckCountExpectation.ExactCount expected -> $"count {expected}"
        | CheckCountExpectation.CountBetween(minimum, maximum) -> $"countBetween {minimum} {maximum}"

    let internal constraintCodeFor failure =
        match failure with
        | CheckFailure.Missing
        | CheckFailure.Blank -> Some "required"
        | CheckFailure.InvalidFormat "email" -> Some "email"
        | CheckFailure.InvalidFormat _ -> Some "pattern"
        | CheckFailure.Length(CheckLengthExpectation.MinimumLength _, _) -> Some "minLength"
        | CheckFailure.Length(CheckLengthExpectation.MaximumLength _, _) -> Some "maxLength"
        | CheckFailure.Length(CheckLengthExpectation.ExactLength _, _)
        | CheckFailure.Length(CheckLengthExpectation.LengthBetween _, _) -> Some "lengthBetween"
        | CheckFailure.Range(CheckRangeExpectation.GreaterThan _, _) -> Some "greaterThan"
        | CheckFailure.Range(CheckRangeExpectation.LessThan _, _) -> Some "lessThan"
        | CheckFailure.Range(CheckRangeExpectation.AtLeast _, _) -> Some "atLeast"
        | CheckFailure.Range(CheckRangeExpectation.AtMost _, _) -> Some "atMost"
        | CheckFailure.Range(CheckRangeExpectation.Between _, _) -> Some "between"
        | CheckFailure.Count(CheckCountExpectation.MinimumCount _, _) -> Some "minCount"
        | CheckFailure.Count(CheckCountExpectation.MaximumCount _, _) -> Some "maxCount"
        | CheckFailure.Count(CheckCountExpectation.ExactCount _, _) -> Some "count"
        | CheckFailure.Count(CheckCountExpectation.CountBetween _, _) -> Some "countBetween"
        | CheckFailure.NonEmpty _ -> Some "minCount"
        | CheckFailure.Equality(CheckEqualityExpectation.EqualTo _, _) -> Some "oneOf"
        | CheckFailure.Equality(CheckEqualityExpectation.NotEqualTo _, _) -> Some "notEqualTo"
        | CheckFailure.CustomCode code -> Some code
        | CheckFailure.Positive _ -> Some "greaterThan"
        | CheckFailure.NonNegative _ -> Some "atLeast"
        | CheckFailure.Negative _ -> Some "lessThan"
        | CheckFailure.NonPositive _ -> Some "atMost"

    /// <summary>Lowers a primitive parse failure into the schema boundary error shape.</summary>
    let ofParseError error =
        match error with
        | ParseError.MissingValue _ -> SchemaError.Required
        | ParseError.InvalidFormat(target, _) -> SchemaError.InvalidFormat target
        | ParseError.OutOfRange(target, _) -> SchemaError.OutOfRange target

    /// <summary>Lowers one path-free check failure into the schema boundary error shape.</summary>
    let ofCheckFailure failure =
        match failure with
        | CheckFailure.Missing
        | CheckFailure.Blank -> SchemaError.Required
        | CheckFailure.InvalidFormat expected -> SchemaError.InvalidFormat expected
        | CheckFailure.Length(CheckLengthExpectation.MinimumLength minimum, actual) -> SchemaError.TooShort(minimum, actual)
        | CheckFailure.Length(CheckLengthExpectation.MaximumLength maximum, actual) -> SchemaError.TooLong(maximum, actual)
        | CheckFailure.Length(CheckLengthExpectation.ExactLength expected, actual) -> SchemaError.LengthOutOfRange(expected, expected, actual)
        | CheckFailure.Length(CheckLengthExpectation.LengthBetween(minimum, maximum), actual) ->
            SchemaError.LengthOutOfRange(minimum, maximum, actual)
        | CheckFailure.Range(expectation, actual) -> SchemaError.RangeOutOfRange(rangeText expectation, actual)
        | CheckFailure.Count(expectation, actual) -> SchemaError.CountOutOfRange(countText expectation, actual)
        | CheckFailure.NonEmpty actual -> SchemaError.CountOutOfRange("minCount 1", actual)
        | CheckFailure.Equality(CheckEqualityExpectation.EqualTo choices, _) -> SchemaError.NotOneOf choices
        | CheckFailure.Equality(CheckEqualityExpectation.NotEqualTo unexpected, _) ->
            SchemaError.Custom($"notEqualTo:{unexpected}", None)
        | CheckFailure.CustomCode code -> SchemaError.Custom(code, None)
        | CheckFailure.Positive actual -> SchemaError.RangeOutOfRange("greaterThan 0", actual)
        | CheckFailure.NonNegative actual -> SchemaError.RangeOutOfRange("atLeast 0", actual)
        | CheckFailure.Negative actual -> SchemaError.RangeOutOfRange("lessThan 0", actual)
        | CheckFailure.NonPositive actual -> SchemaError.RangeOutOfRange("atMost 0", actual)

    /// <summary>Lowers a refinement failure into one or more schema boundary errors.</summary>
    let ofRefinementError error =
        match error with
        | RefinementError.ParseFailed parseError -> [ ofParseError parseError ]
        | RefinementError.CheckFailed(_, failures) -> failures |> List.map ofCheckFailure
        | RefinementError.InvalidStructure(target, reason) -> [ SchemaError.Custom(target, Some reason) ]

    /// <summary>Renders one schema boundary error as a default English display string.</summary>
    let render error =
        match error with
        | SchemaError.Required -> "This value is required."
        | SchemaError.ExpectedScalar -> "Expected a scalar value."
        | SchemaError.ExpectedObject -> "Expected an object."
        | SchemaError.ExpectedMany -> "Expected a collection."
        | SchemaError.InvalidFormat expected -> $"Expected {expected} format."
        | SchemaError.OutOfRange target -> $"{target} value is out of range."
        | SchemaError.TooShort(minimum, None) -> $"Must be at least {minimum} characters."
        | SchemaError.TooShort(minimum, Some actual) -> $"Must be at least {minimum} characters; got {actual}."
        | SchemaError.TooLong(maximum, None) -> $"Must be at most {maximum} characters."
        | SchemaError.TooLong(maximum, Some actual) -> $"Must be at most {maximum} characters; got {actual}."
        | SchemaError.LengthOutOfRange(minimum, maximum, None) -> $"Length must be between {minimum} and {maximum}."
        | SchemaError.LengthOutOfRange(minimum, maximum, Some actual) ->
            $"Length must be between {minimum} and {maximum}; got {actual}."
        | SchemaError.RangeOutOfRange(expectation, None) -> $"Must satisfy {expectation}."
        | SchemaError.RangeOutOfRange(expectation, Some actual) -> $"Must satisfy {expectation}; got {actual}."
        | SchemaError.CountOutOfRange(expectation, None) -> $"Count must satisfy {expectation}."
        | SchemaError.CountOutOfRange(expectation, Some actual) -> $"Count must satisfy {expectation}; got {actual}."
        | SchemaError.NotOneOf choices -> $"Must be one of: {choices}."
        | SchemaError.Duplicate -> "Duplicate values are not allowed."
        | SchemaError.ConstructorFailed message -> message
        | SchemaError.Custom(_, Some message) -> message
        | SchemaError.Custom(code, None) -> code

    /// <summary>Renders flattened diagnostics with path prefixes for display at a boundary.</summary>
    let renderDiagnostic (diagnostic: Diagnostic<SchemaError>) =
        let message = render diagnostic.Error
        let segmentText = function
            | PathSegment.Key key -> key
            | PathSegment.Index index -> $"[{index}]"
            | PathSegment.Name name -> name

        match diagnostic.Path with
        | [] -> message
        | path ->
            let pathText = path |> List.map segmentText |> String.concat "."
            $"{pathText}: {message}"

    /// <summary>Renders schema boundary diagnostics as default English display strings.</summary>
    let renderDiagnostics diagnostics =
        diagnostics
        |> Diagnostics.flatten
        |> List.map renderDiagnostic

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
    let packageName = "Axial.Validation.Schema"

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
module SchemaConstraintCheck =
    let private ensureConstraint (constraint': SchemaConstraint) =
        if isNull constraint' then
            nullArg (nameof constraint')

    let private ensureConstraints constraints =
        if isNull (box constraints) then
            nullArg (nameof constraints)

        let constraints = constraints |> Seq.toList

        constraints
        |> List.iter (fun constraint' ->
            if isNull constraint' then
                nullArg (nameof constraints))

        constraints

    let private tryArgument<'value> name constraint' =
        match SchemaConstraint.tryFindArgument name constraint' with
        | Some (:? 'value as value) -> Some value
        | _ -> None

    let private tryBounds<'value> constraint' =
        match tryArgument<'value> "minimum" constraint', tryArgument<'value> "maximum" constraint' with
        | Some minimum, Some maximum -> Some(minimum, maximum)
        | _ -> None

    /// <summary>Lowers one schema constraint to a string check when the constraint has text-level meaning.</summary>
    let tryText (constraint': SchemaConstraint) : Check<string> option =
        ensureConstraint constraint'

        match SchemaConstraint.code constraint' with
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
                    Error [ CheckFailure.Missing ]
                elif value.Trim() = value then
                    Ok()
                else
                    Error [ CheckFailure.InvalidFormat "trimmed" ])
        | "pattern" -> tryArgument<string> "pattern" constraint' |> Option.map Check.String.matches
        | "oneOf" ->
            tryArgument<string array> "choices" constraint'
            |> Option.map (fun choices -> Check.String.oneOf choices)
        | "notEqualTo" -> tryArgument<string> "unexpected" constraint' |> Option.map Check.notEqualTo
        | _ -> None

    /// <summary>Lowers schema constraints with text-level meaning into one string check.</summary>
    let text (constraints: SchemaConstraint seq) : Check<string> =
        ensureConstraints constraints
        |> Seq.choose tryText
        |> Seq.toList
        |> Check.all

    let private betweenCheck minimum maximum : Check<'value> =
        fun value ->
            if value >= minimum && value <= maximum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.Between(string minimum, string maximum), Some(string value)) ]

    let private greaterThanCheck minimum : Check<'value> =
        fun value ->
            if value > minimum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.GreaterThan(string minimum), Some(string value)) ]

    let private lessThanCheck maximum : Check<'value> =
        fun value ->
            if value < maximum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.LessThan(string maximum), Some(string value)) ]

    let private atLeastCheck minimum : Check<'value> =
        fun value ->
            if value >= minimum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.AtLeast(string minimum), Some(string value)) ]

    let private atMostCheck maximum : Check<'value> =
        fun value ->
            if value <= maximum then
                Ok ()
            else
                Error [ Range(CheckRangeExpectation.AtMost(string maximum), Some(string value)) ]

    /// <summary>Lowers one schema constraint to an ordered-value check when the constraint has range-level meaning.</summary>
    let tryOrdered<'value when 'value: comparison> (constraint': SchemaConstraint) : Check<'value> option =
        ensureConstraint constraint'

        match SchemaConstraint.code constraint' with
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
    let ordered<'value when 'value: comparison> (constraints: SchemaConstraint seq) : Check<'value> =
        ensureConstraints constraints
        |> Seq.choose tryOrdered<'value>
        |> Seq.toList
        |> Check.all

    /// <summary>Lowers one schema constraint to a sequence check when the constraint has sequence-level meaning.</summary>
    let trySequence<'value when 'value: equality> (constraint': SchemaConstraint) : Check<seq<'value>> option =
        ensureConstraint constraint'

        match SchemaConstraint.code constraint' with
        | "count" -> tryArgument<int> "expected" constraint' |> Option.map Check.Seq.count
        | "minCount" -> tryArgument<int> "minimum" constraint' |> Option.map Check.Seq.minCount
        | "maxCount" -> tryArgument<int> "maximum" constraint' |> Option.map Check.Seq.maxCount
        | "countBetween" ->
            tryBounds<int> constraint'
            |> Option.map (fun (minimum, maximum) -> Check.Seq.countBetween minimum maximum)
        | "distinct" -> Some Check.Seq.noDuplicates
        | _ -> None

    /// <summary>Lowers schema constraints with sequence-level meaning into one sequence check.</summary>
    let sequence<'value when 'value: equality> (constraints: SchemaConstraint seq) : Check<seq<'value>> =
        ensureConstraints constraints
        |> Seq.choose trySequence<'value>
        |> Seq.toList
        |> Check.all

module internal SchemaCheckFailure =
    let private tryCustomMessage constraints code =
        constraints
        |> List.tryFind (fun constraint' -> SchemaConstraint.code constraint' = code)
        |> Option.bind SchemaConstraint.message

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
/// through the schema's refinement layers with <see cref="M:Axial.Schema.Value.inspectUnderlying``2" /> and running
/// the primitive-level check on the result. Primitive value schemas work the same way with an identity projection.
/// </para>
/// <para>
/// The metadata lowerers gather constraint metadata from every refinement layer with
/// <see cref="M:Axial.Schema.Value.allConstraints``1" /> and lower it through
/// <see cref="T:Axial.Validation.Schema.SchemaConstraintCheck" />, so raw-layer and refined-layer constraints run as
/// one check program.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module ValueSchemaCheck =
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
    let fromUnderlying (check: Check<'primitive>) (schema: ValueSchema<'value>) : Check<'value> =
        if isNull (box check) then
            nullArg (nameof check)

        if isNull (box schema) then
            nullArg (nameof schema)

        let inspect = Value.inspectUnderlying<'value, 'primitive> schema
        fun value -> check (inspect value)

    /// <summary>
    /// Lowers the text-meaning constraint metadata carried by every layer of a value schema into one executable check
    /// over the schema's values.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the schema's underlying primitive kind is not text.
    /// </exception>
    let text (schema: ValueSchema<'value>) : Check<'value> =
        if isNull (box schema) then
            nullArg (nameof schema)

        fromUnderlying (SchemaConstraintCheck.text (Value.allConstraints schema)) schema

    /// <summary>
    /// Lowers the range-meaning constraint metadata carried by every layer of a value schema into one executable check
    /// over the schema's values.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// Thrown when the ordered primitive type does not match the schema's underlying primitive kind.
    /// </exception>
    let ordered<'primitive, 'value when 'primitive: comparison> (schema: ValueSchema<'value>) : Check<'value> =
        if isNull (box schema) then
            nullArg (nameof schema)

        fromUnderlying (SchemaConstraintCheck.ordered<'primitive> (Value.allConstraints schema)) schema

/// <summary>Functions for validating existing trusted model values through a schema.</summary>
[<RequireQualifiedAccess>]
module Validation =
    let private diagnosticsAt path error =
        Axial.Validation.Validation.fail (Diagnostics.singleton error)
        |> Axial.Validation.Validation.at path
        |> Axial.Validation.Validation.toResult
        |> function
            | Error diagnostics -> diagnostics
            | Ok _ -> Diagnostics.empty

    let private mergeErrors diagnostics =
        diagnostics
        |> List.reduce Diagnostics.merge

    let private allConstraints definition =
        let rec gather valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> valueDefinition.Constraints
            | RefinedValueDefinition(raw, _) -> gather raw @ valueDefinition.Constraints
            | NestedValueDefinition _ -> valueDefinition.Constraints
            | ManyValueDefinition _ -> valueDefinition.Constraints

        gather definition

    let private underlyingPrimitiveKind definition =
        let rec kindOf valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition kind -> kind
            | RefinedValueDefinition(raw, _) -> kindOf raw
            | NestedValueDefinition _ -> invalidOp "Nested model value schemas have no underlying primitive kind."
            | ManyValueDefinition _ -> invalidOp "Collection value schemas have no underlying primitive kind."

        kindOf definition

    let private inspectUnderlying definition value =
        let rec project valueDefinition current =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> current
            | RefinedValueDefinition(raw, ops) -> project raw (ops.Inspect current)
            | NestedValueDefinition _ -> invalidOp "Nested model values have no underlying primitive representation."
            | ManyValueDefinition _ -> invalidOp "Collection values have no underlying primitive representation."

        project definition value

    let private runCheck constraints check value =
        match check value with
        | Ok () -> Ok value
        | Error failures -> failures |> SchemaCheckFailure.toSchemaErrors constraints |> Error

    let private checkPrimitive kind constraints value =
        match kind with
        | PrimitiveValueKind.Text ->
            value
            |> unbox<string>
            |> runCheck constraints (SchemaConstraintCheck.text constraints)
            |> Result.map box
        | PrimitiveValueKind.Int ->
            value
            |> unbox<int>
            |> runCheck constraints (SchemaConstraintCheck.ordered<int> constraints)
            |> Result.map box
        | PrimitiveValueKind.Decimal ->
            value
            |> unbox<decimal>
            |> runCheck constraints (SchemaConstraintCheck.ordered<decimal> constraints)
            |> Result.map box
        | PrimitiveValueKind.Bool -> Ok value
#if NET6_0_OR_GREATER
        | PrimitiveValueKind.Date ->
            value
            |> unbox<DateOnly>
            |> runCheck constraints (SchemaConstraintCheck.ordered<DateOnly> constraints)
            |> Result.map box
#else
        | PrimitiveValueKind.Date -> Ok value
#endif
        | PrimitiveValueKind.DateTime ->
            value
            |> unbox<DateTimeOffset>
            |> runCheck constraints (SchemaConstraintCheck.ordered<DateTimeOffset> constraints)
            |> Result.map box
        | PrimitiveValueKind.Guid -> Ok value

    let rec private validateValue valueSchema fieldConstraints path (value: obj) =
        let constraints = allConstraints valueSchema @ fieldConstraints

        match valueSchema.Shape with
        | RefinedValueDefinition(raw, ops) ->
            match raw.Shape with
            | NestedValueDefinition _
            | ManyValueDefinition _ ->
                validateValue raw (valueSchema.Constraints @ fieldConstraints) path (ops.Inspect value)
                |> Axial.Validation.Validation.map (fun _ -> value)
            | PrimitiveValueDefinition _
            | RefinedValueDefinition _ ->
                let kind = underlyingPrimitiveKind valueSchema
                let primitive = inspectUnderlying valueSchema value

                match checkPrimitive kind constraints primitive with
                | Ok _ -> Axial.Validation.Validation.ok value
                | Error errors ->
                    errors
                    |> List.map (diagnosticsAt path)
                    |> mergeErrors
                    |> Axial.Validation.Validation.error
        | PrimitiveValueDefinition _ ->
            let kind = underlyingPrimitiveKind valueSchema
            let primitive = inspectUnderlying valueSchema value

            match checkPrimitive kind constraints primitive with
            | Ok _ -> Axial.Validation.Validation.ok value
            | Error errors ->
                errors
                |> List.map (diagnosticsAt path)
                |> mergeErrors
                |> Axial.Validation.Validation.error
        | NestedValueDefinition nestedModel ->
            validateObject path nestedModel value
            |> Axial.Validation.Validation.map (fun _ -> value)
        | ManyValueDefinition collection ->
            validateMany path collection constraints (value :?> System.Collections.IEnumerable)
            |> Axial.Validation.Validation.map (fun _ -> value)

    and private validateField basePath model (field: FieldDescriptor<obj>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = basePath @ [ PathSegment.Name name ]
        let value = field.Getter model
        validateValue field.ValueSchema field.Constraints path value

    and private validateObject path (modelSchema: ModelSchemaDefinition<obj>) model =
        let validatedFields = modelSchema.Fields |> List.map (validateField path model)
        let errors =
            validatedFields
            |> List.choose (fun validation ->
                match Axial.Validation.Validation.toResult validation with
                | Ok _ -> None
                | Error diagnostics -> Some diagnostics)

        match errors with
        | [] -> Axial.Validation.Validation.ok model
        | diagnostics -> diagnostics |> mergeErrors |> Axial.Validation.Validation.error

    and private checkMany constraints path items =
        match items |> runCheck constraints (SchemaConstraintCheck.sequence<obj> constraints) with
        | Ok checkedItems -> Axial.Validation.Validation.ok checkedItems
        | Error errors ->
            errors
            |> List.map (diagnosticsAt path)
            |> mergeErrors
            |> Axial.Validation.Validation.error

    and private validateMany path (collection: CollectionValueDefinition) constraints (items: System.Collections.IEnumerable) =
        let items = items |> Seq.cast<obj> |> Seq.toList

        let validatedItems =
            items
            |> List.mapi (fun index item -> validateValue collection.Item [] (path @ [ PathSegment.Index index ]) item)

        let errors =
            validatedItems
            |> List.choose (fun validation ->
                match Axial.Validation.Validation.toResult validation with
                | Ok _ -> None
                | Error diagnostics -> Some diagnostics)

        match errors with
        | [] -> checkMany constraints path items
        | diagnostics -> diagnostics |> mergeErrors |> Axial.Validation.Validation.error

    let private validateRootField model (field: FieldDescriptor<'model>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = [ PathSegment.Name name ]
        let value = field.Getter model
        validateValue field.ValueSchema field.Constraints path value

    /// <summary>Validates an existing trusted model value through a built model schema.</summary>
    /// <remarks>
    /// The validator reads values with schema getters, runs schema constraints through the same executable
    /// <see cref="T:Axial.ErrorHandling.Check`1" /> lowering used by input parsing, and recursively validates nested
    /// models and collection items. Successful validation returns the original model value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is not a built model schema.</exception>
    let validate (schema: Schema<'model>) (model: 'model) : Axial.Validation.Validation<'model, SchemaError> =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition with
        | PendingDefinition -> invalidArg (nameof schema) "Expected a built model schema."
        | ModelDefinition modelSchema ->
            let validatedFields = modelSchema.Fields |> List.map (validateRootField model)
            let errors =
                validatedFields
                |> List.choose (fun validation ->
                    match Axial.Validation.Validation.toResult validation with
                    | Ok _ -> None
                    | Error diagnostics -> Some diagnostics)

            match errors with
            | [] -> Axial.Validation.Validation.ok model
            | diagnostics -> diagnostics |> mergeErrors |> Axial.Validation.Validation.error
