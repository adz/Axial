namespace Axial.Validation.Schema

open System
open System.Globalization
open Axial.ErrorHandling
open Axial.Refined
open Axial.Schema
open Axial.Validation

/// <summary>Schema input parsing failures attached to diagnostics paths.</summary>
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

/// <summary>Functions for parsing raw input through a schema.</summary>
[<RequireQualifiedAccess>]
module Input =
    let private diagnosticsAt path error =
        Validation.fail (Diagnostics.singleton error)
        |> Validation.at path
        |> Validation.toResult
        |> function
            | Error diagnostics -> diagnostics
            | Ok _ -> Diagnostics.empty

    let private errorAt path error =
        Error(diagnosticsAt path error)

    let private constructorErrorAt path message =
        errorAt path (SchemaError.ConstructorFailed message)

    let private mergeErrors diagnostics =
        diagnostics
        |> List.reduce Diagnostics.merge

    let private parseErrorToSchemaError error =
        match error with
        | ParseError.MissingValue _ -> SchemaError.Required
        | ParseError.InvalidFormat(target, _) -> SchemaError.InvalidFormat target
        | ParseError.OutOfRange(target, _) -> SchemaError.OutOfRange target

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

    /// <summary>Identifies the schema constraint code responsible for a check failure, when one is known.</summary>
    /// <remarks>
    /// Used only to look up an author-supplied custom message on the originating <see cref="T:Axial.Schema.SchemaConstraint" />;
    /// it is not part of the default <see cref="T:Axial.Validation.Schema.SchemaError" /> shape.
    /// </remarks>
    let private constraintCodeFor failure =
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
        | CheckFailure.Equality(CheckEqualityExpectation.NotEqualTo _, _) -> None
        | CheckFailure.CustomCode code -> Some code
        | CheckFailure.Positive _ -> Some "greaterThan"
        | CheckFailure.NonNegative _ -> Some "atLeast"
        | CheckFailure.Negative _ -> Some "lessThan"
        | CheckFailure.NonPositive _ -> Some "atMost"

    let private tryCustomMessage constraints code =
        constraints
        |> List.tryFind (fun constraint' -> SchemaConstraint.code constraint' = code)
        |> Option.bind SchemaConstraint.message

    let private withCustomMessage constraints code error =
        match tryCustomMessage constraints code with
        | Some message -> SchemaError.Custom(code, Some message)
        | None -> error

    let private checkFailureToSchemaError constraints failure =
        let error =
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

        match constraintCodeFor failure with
        | Some code -> withCustomMessage constraints code error
        | None -> error

    let private allConstraints definition =
        let rec gather valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> valueDefinition.Constraints
            | RefinedValueDefinition(raw, _) -> gather raw @ valueDefinition.Constraints
            | NestedValueDefinition _ -> valueDefinition.Constraints
            | ManyValueDefinition _ -> valueDefinition.Constraints

        gather definition

    let private hasRequiredConstraint constraints =
        constraints
        |> List.exists (fun constraint' -> SchemaConstraint.code constraint' = "required")

    let private underlyingPrimitiveKind definition =
        let rec kindOf valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition kind -> kind
            | RefinedValueDefinition(raw, _) -> kindOf raw
            | NestedValueDefinition _ -> invalidOp "Nested model value schemas have no underlying primitive kind."
            | ManyValueDefinition _ -> invalidOp "Collection value schemas have no underlying primitive kind."

        kindOf definition

    let private constructValue definition primitive =
        let rec construct valueDefinition value =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> value
            | RefinedValueDefinition(raw, ops) -> construct raw value |> ops.Construct
            | NestedValueDefinition _ -> invalidOp "Nested model values are constructed by parsing, not primitive construction."
            | ManyValueDefinition _ -> invalidOp "Collection values are constructed by parsing, not primitive construction."

        construct definition primitive

    let private runCheck constraints check value =
        match check value with
        | Ok () -> Ok value
        | Error failures -> failures |> List.map (checkFailureToSchemaError constraints) |> Error

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

    let private parsePrimitive kind text =
        match kind with
        | PrimitiveValueKind.Text -> Ok(box text)
        | PrimitiveValueKind.Int -> Parse.int text |> Result.map box |> Result.mapError parseErrorToSchemaError
        | PrimitiveValueKind.Decimal -> Parse.decimal text |> Result.map box |> Result.mapError parseErrorToSchemaError
        | PrimitiveValueKind.Bool -> Parse.bool text |> Result.map box |> Result.mapError parseErrorToSchemaError
#if NET6_0_OR_GREATER
        | PrimitiveValueKind.Date ->
            match DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, value -> Ok(box value)
            | false, _ ->
                if String.IsNullOrWhiteSpace text then Error SchemaError.Required else Error(SchemaError.InvalidFormat "date")
#else
        | PrimitiveValueKind.Date -> Error(SchemaError.InvalidFormat "date")
#endif
        | PrimitiveValueKind.DateTime -> Parse.dateTimeOffset text |> Result.map box |> Result.mapError parseErrorToSchemaError
        | PrimitiveValueKind.Guid -> Parse.guid text |> Result.map box |> Result.mapError parseErrorToSchemaError

    let rec private parseValue valueSchema fieldConstraints path raw =
        let constraints = allConstraints valueSchema @ fieldConstraints

        match raw with
        | RawInput.Missing -> errorAt path (withCustomMessage constraints "required" SchemaError.Required)
        | RawInput.Object fields ->
            match valueSchema.Shape with
            | NestedValueDefinition nestedModel -> parseObject path nestedModel fields
            | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
            | PrimitiveValueDefinition _
            | RefinedValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
        | RawInput.Many rawItems ->
            match valueSchema.Shape with
            | NestedValueDefinition _ -> errorAt path SchemaError.ExpectedObject
            | ManyValueDefinition collection -> parseMany path collection constraints rawItems
            | PrimitiveValueDefinition _
            | RefinedValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
        | RawInput.Scalar text when hasRequiredConstraint constraints && String.IsNullOrWhiteSpace text ->
            errorAt path (withCustomMessage constraints "required" SchemaError.Required)
        | RawInput.Scalar text ->
            match valueSchema.Shape with
            | NestedValueDefinition _ -> errorAt path SchemaError.ExpectedObject
            | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
            | PrimitiveValueDefinition _
            | RefinedValueDefinition _ ->
                let kind = underlyingPrimitiveKind valueSchema

                match parsePrimitive kind text with
                | Error error -> errorAt path error
                | Ok primitive ->
                    match checkPrimitive kind constraints primitive with
                    | Error errors ->
                        errors
                        |> List.map (diagnosticsAt path)
                        |> mergeErrors
                        |> Error
                    | Ok checkedPrimitive -> Ok(constructValue valueSchema checkedPrimitive)

    and private parseField basePath (fields: Map<string, RawInput>) (field: FieldDescriptor<'model>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = basePath @ [ PathSegment.Name name ]
        let raw = fields |> Map.tryFind name |> Option.defaultValue RawInput.Missing
        parseValue field.ValueSchema field.Constraints path raw

    and private parseObject path (model: ModelSchemaDefinition<obj>) (fields: Map<string, RawInput>) =
        let parsedFields = model.Fields |> List.map (parseField path fields)
        let errors = parsedFields |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)

        match errors with
        | [] ->
            parsedFields
            |> List.map (function Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")
            |> List.toArray
            |> ConstructorApplication.tryApply model.Constructor
            |> function
                | Ok model -> Ok model
                | Error message -> constructorErrorAt path message
        | diagnostics -> Error(mergeErrors diagnostics)

    and private parseManyItem path itemModel rawItem =
        match rawItem with
        | RawInput.Object fields -> parseObject path itemModel fields
        | RawInput.Missing
        | RawInput.Scalar _
        | RawInput.Many _ -> errorAt path SchemaError.ExpectedObject

    and private checkMany constraints path items =
        match items |> runCheck constraints (SchemaConstraintCheck.sequence<obj> constraints) with
        | Ok checkedItems -> Ok checkedItems
        | Error errors ->
            errors
            |> List.map (diagnosticsAt path)
            |> mergeErrors
            |> Error

    and private parseMany path (collection: CollectionValueDefinition) constraints rawItems =
        match collection.Item.Shape with
        | NestedValueDefinition itemModel ->
            let parsedItems =
                rawItems
                |> List.mapi (fun index rawItem -> parseManyItem (path @ [ PathSegment.Index index ]) itemModel rawItem)
            let errors = parsedItems |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)

            match errors with
            | [] ->
                let items =
                    parsedItems
                    |> List.map (function Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")

                match checkMany constraints path items with
                | Ok checkedItems -> checkedItems |> collection.BoxItems |> Ok
                | Error diagnostics -> Error diagnostics
            | diagnostics -> Error(mergeErrors diagnostics)
        | PrimitiveValueDefinition _
        | RefinedValueDefinition _
        | ManyValueDefinition _ -> invalidOp "Collection item value schemas must be nested model value schemas."

    /// <summary>Parses raw boundary input through a trusted model schema.</summary>
    let parse (schema: Schema<'model>) (input: RawInput) : ParsedInput<'model, SchemaError> =
        if isNull (box schema) then
            nullArg (nameof schema)

        let result =
            match schema.Definition, input with
            | PendingDefinition, _ -> invalidArg (nameof schema) "Expected a built model schema."
            | ModelDefinition _, RawInput.Missing -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition _, RawInput.Scalar _ -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition _, RawInput.Many _ -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition model, RawInput.Object fields ->
                let parsedFields = model.Fields |> List.map (parseField [] fields)
                let errors = parsedFields |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)

                match errors with
                | [] ->
                    parsedFields
                    |> List.map (function Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")
                    |> List.toArray
                    |> ConstructorApplication.tryApply model.Constructor
                    |> function
                        | Ok model -> Ok model
                        | Error message -> constructorErrorAt [] message
                | diagnostics -> Error(mergeErrors diagnostics)

        { Input = input; Result = result }
