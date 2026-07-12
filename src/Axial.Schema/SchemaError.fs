namespace Axial.Schema

open Axial.ErrorHandling
#if !AXIAL_SCHEMA_CORE_ONLY
open Axial.Refined
open Axial.Validation
#endif

/// <summary>Schema input, checking, and contextual rule failures attached to diagnostics paths.</summary>
[<RequireQualifiedAccess>]
type SchemaError =
    | Required
    | ExpectedScalar
    | ExpectedObject
    | ExpectedMany
    | InvalidFormat of expected: string
    | ParseOutOfRange of target: string
    | InvalidLength of expectation: CheckLengthExpectation * actualLength: int option
    | OutOfRange of expectation: CheckRangeExpectation * actual: string option
    | InvalidCount of expectation: CheckCountExpectation * actualCount: int option
    | NotOneOf of choices: string
    | Duplicate
    | ConstructorFailed of message: string
    | Custom of code: string * message: string option

#if !AXIAL_SCHEMA_CORE_ONLY
/// <summary>Functions for lowering and rendering boundary schema failures.</summary>
[<RequireQualifiedAccess>]
module SchemaError =
    let private lengthText expectation =
        match expectation with
        | CheckLengthExpectation.MinimumLength minimum -> $"at least {minimum}"
        | CheckLengthExpectation.MaximumLength maximum -> $"at most {maximum}"
        | CheckLengthExpectation.ExactLength expected -> $"exactly {expected}"
        | CheckLengthExpectation.LengthBetween(minimum, maximum) -> $"between {minimum} and {maximum}"

    let private rangeText expectation =
        match expectation with
        | CheckRangeExpectation.GreaterThan minimum -> $"greater than {minimum}"
        | CheckRangeExpectation.LessThan maximum -> $"less than {maximum}"
        | CheckRangeExpectation.AtLeast minimum -> $"at least {minimum}"
        | CheckRangeExpectation.AtMost maximum -> $"at most {maximum}"
        | CheckRangeExpectation.Between(minimum, maximum) -> $"between {minimum} and {maximum}"
        | CheckRangeExpectation.NotMultipleOf divisor -> $"a multiple of {divisor}"

    let private countText expectation =
        match expectation with
        | CheckCountExpectation.MinimumCount minimum -> $"at least {minimum}"
        | CheckCountExpectation.MaximumCount maximum -> $"at most {maximum}"
        | CheckCountExpectation.ExactCount expected -> $"exactly {expected}"
        | CheckCountExpectation.CountBetween(minimum, maximum) -> $"between {minimum} and {maximum}"

    let internal constraintCodeFor failure =
        match failure with
        | CheckFailure.Required -> Some "required"
        | CheckFailure.InvalidFormat "email" -> Some "email"
        | CheckFailure.InvalidFormat _ -> Some "pattern"
        | CheckFailure.InvalidLength(CheckLengthExpectation.MinimumLength _, _) -> Some "minLength"
        | CheckFailure.InvalidLength(CheckLengthExpectation.MaximumLength _, _) -> Some "maxLength"
        | CheckFailure.InvalidLength(CheckLengthExpectation.ExactLength _, _)
        | CheckFailure.InvalidLength(CheckLengthExpectation.LengthBetween _, _) -> Some "lengthBetween"
        | CheckFailure.OutOfRange(CheckRangeExpectation.GreaterThan _, _) -> Some "greaterThan"
        | CheckFailure.OutOfRange(CheckRangeExpectation.LessThan _, _) -> Some "lessThan"
        | CheckFailure.OutOfRange(CheckRangeExpectation.AtLeast _, _) -> Some "atLeast"
        | CheckFailure.OutOfRange(CheckRangeExpectation.AtMost _, _) -> Some "atMost"
        | CheckFailure.OutOfRange(CheckRangeExpectation.Between _, _) -> Some "between"
        | CheckFailure.OutOfRange(CheckRangeExpectation.NotMultipleOf _, _) -> Some "multipleOf"
        | CheckFailure.InvalidCount(CheckCountExpectation.MinimumCount _, _) -> Some "minCount"
        | CheckFailure.InvalidCount(CheckCountExpectation.MaximumCount _, _) -> Some "maxCount"
        | CheckFailure.InvalidCount(CheckCountExpectation.ExactCount _, _) -> Some "count"
        | CheckFailure.InvalidCount(CheckCountExpectation.CountBetween _, _) -> Some "countBetween"
        | CheckFailure.NotOneOf _ -> Some "oneOf"
        | CheckFailure.Duplicate -> Some "distinct"
        | CheckFailure.Custom code -> Some code

    let ofParseError error =
        match error with
        | ParseError.MissingValue _ -> SchemaError.Required
        | ParseError.InvalidFormat(target, _) -> SchemaError.InvalidFormat target
        | ParseError.OutOfRange(target, _) -> SchemaError.ParseOutOfRange target

    let ofCheckFailure failure =
        match failure with
        | CheckFailure.Required -> SchemaError.Required
        | CheckFailure.InvalidFormat expected -> SchemaError.InvalidFormat expected
        | CheckFailure.InvalidLength(expectation, actual) -> SchemaError.InvalidLength(expectation, actual)
        | CheckFailure.OutOfRange(expectation, actual) -> SchemaError.OutOfRange(expectation, actual)
        | CheckFailure.InvalidCount(expectation, actual) -> SchemaError.InvalidCount(expectation, actual)
        | CheckFailure.NotOneOf choices -> SchemaError.NotOneOf choices
        | CheckFailure.Duplicate -> SchemaError.Duplicate
        | CheckFailure.Custom code -> SchemaError.Custom(code, None)

    let ofRefinementError error =
        match error with
        | RefinementError.ParseFailed parseError -> [ ofParseError parseError ]
        | RefinementError.CheckFailed(_, failures) -> failures |> List.map ofCheckFailure
        | RefinementError.InvalidStructure(target, reason) -> [ SchemaError.Custom(target, Some reason) ]

    let render error =
        match error with
        | SchemaError.Required -> "This value is required."
        | SchemaError.ExpectedScalar -> "Expected a scalar value."
        | SchemaError.ExpectedObject -> "Expected an object."
        | SchemaError.ExpectedMany -> "Expected a collection."
        | SchemaError.InvalidFormat expected -> $"Expected {expected} format."
        | SchemaError.ParseOutOfRange target -> $"{target} value is out of range."
        | SchemaError.InvalidLength(expectation, None) -> $"Length must be {lengthText expectation}."
        | SchemaError.InvalidLength(expectation, Some actual) -> $"Length must be {lengthText expectation}; got {actual}."
        | SchemaError.OutOfRange(expectation, None) -> $"Must be {rangeText expectation}."
        | SchemaError.OutOfRange(expectation, Some actual) -> $"Must be {rangeText expectation}; got {actual}."
        | SchemaError.InvalidCount(expectation, None) -> $"Count must be {countText expectation}."
        | SchemaError.InvalidCount(expectation, Some actual) -> $"Count must be {countText expectation}; got {actual}."
        | SchemaError.NotOneOf choices -> $"Must be one of: {choices}."
        | SchemaError.Duplicate -> "Duplicate values are not allowed."
        | SchemaError.ConstructorFailed message -> message
        | SchemaError.Custom(_, Some message) -> message
        | SchemaError.Custom(code, None) -> code

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

    let renderDiagnostics diagnostics =
        diagnostics |> Diagnostics.flatten |> List.map renderDiagnostic
#endif
