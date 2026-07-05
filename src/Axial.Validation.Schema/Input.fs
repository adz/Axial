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
    /// <summary>A custom schema failure code was produced.</summary>
    | Custom of code: string

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

    let private checkFailureToSchemaError failure =
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
        | CheckFailure.Equality(CheckEqualityExpectation.NotEqualTo unexpected, _) -> SchemaError.Custom($"notEqualTo:{unexpected}")
        | CheckFailure.CustomCode code -> SchemaError.Custom code
        | CheckFailure.Positive actual -> SchemaError.RangeOutOfRange("greaterThan 0", actual)
        | CheckFailure.NonNegative actual -> SchemaError.RangeOutOfRange("atLeast 0", actual)
        | CheckFailure.Negative actual -> SchemaError.RangeOutOfRange("lessThan 0", actual)
        | CheckFailure.NonPositive actual -> SchemaError.RangeOutOfRange("atMost 0", actual)

    let private allConstraints definition =
        let rec gather valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> valueDefinition.Constraints
            | RefinedValueDefinition(raw, _) -> gather raw @ valueDefinition.Constraints

        gather definition

    let private hasRequiredConstraint constraints =
        constraints
        |> List.exists (fun constraint' -> SchemaConstraint.code constraint' = "required")

    let private underlyingPrimitiveKind definition =
        let rec kindOf valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition kind -> kind
            | RefinedValueDefinition(raw, _) -> kindOf raw

        kindOf definition

    let private constructValue definition primitive =
        let rec construct valueDefinition value =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> value
            | RefinedValueDefinition(raw, ops) -> construct raw value |> ops.Construct

        construct definition primitive

    let private runCheck check value =
        match check value with
        | Ok () -> Ok value
        | Error failures -> failures |> List.map checkFailureToSchemaError |> Error

    let private checkPrimitive kind constraints value =
        match kind with
        | PrimitiveValueKind.Text ->
            value
            |> unbox<string>
            |> runCheck (SchemaConstraintCheck.text constraints)
            |> Result.map box
        | PrimitiveValueKind.Int ->
            value
            |> unbox<int>
            |> runCheck (SchemaConstraintCheck.ordered<int> constraints)
            |> Result.map box
        | PrimitiveValueKind.Decimal ->
            value
            |> unbox<decimal>
            |> runCheck (SchemaConstraintCheck.ordered<decimal> constraints)
            |> Result.map box
        | PrimitiveValueKind.Bool -> Ok value
#if NET6_0_OR_GREATER
        | PrimitiveValueKind.Date ->
            value
            |> unbox<DateOnly>
            |> runCheck (SchemaConstraintCheck.ordered<DateOnly> constraints)
            |> Result.map box
#else
        | PrimitiveValueKind.Date -> Ok value
#endif
        | PrimitiveValueKind.DateTime ->
            value
            |> unbox<DateTimeOffset>
            |> runCheck (SchemaConstraintCheck.ordered<DateTimeOffset> constraints)
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

    let private parseValue valueSchema fieldConstraints path raw =
        let constraints = allConstraints valueSchema @ fieldConstraints

        match raw with
        | RawInput.Missing when hasRequiredConstraint constraints -> errorAt path SchemaError.Required
        | RawInput.Missing -> errorAt path SchemaError.Required
        | RawInput.Object _ -> errorAt path SchemaError.ExpectedScalar
        | RawInput.Many _ -> errorAt path SchemaError.ExpectedScalar
        | RawInput.Scalar text when hasRequiredConstraint constraints && String.IsNullOrWhiteSpace text ->
            errorAt path SchemaError.Required
        | RawInput.Scalar text ->
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

    let private parseField (fields: Map<string, RawInput>) (field: FieldDescriptor<'model>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = [ PathSegment.Name name ]
        let raw = fields |> Map.tryFind name |> Option.defaultValue RawInput.Missing
        parseValue field.ValueSchema field.Constraints path raw

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
                let parsedFields = model.Fields |> List.map (parseField fields)
                let errors = parsedFields |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)

                match errors with
                | [] ->
                    parsedFields
                    |> List.map (function Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")
                    |> List.toArray
                    |> ConstructorApplication.apply model.Constructor
                    |> Ok
                | diagnostics -> Error(mergeErrors diagnostics)

        { Input = input; Result = result }
