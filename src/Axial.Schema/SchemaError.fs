// SchemaError: the one portable error vocabulary every schema interpreter reports through, so
// parsing, checking, and refinement failures render and compose the same way regardless of which
// interpreter raised them. AXIAL_SCHEMA_CORE_ONLY trims the Refined-dependent cases for
// consumers that only need the core shape.
namespace Axial.Schema

open Axial.Parse

open Axial.Check

/// <summary>Schema input, checking, and contextual rule failures attached to diagnostics paths.</summary>
[<RequireQualifiedAccess>]
type SchemaError =
    | Omitted
    | Blank
    | ExpectedScalar
    | ExpectedObject
    | ExpectedMany
    | InvalidFormat of expected: string
    | ParseOutOfRange of target: string
    | InvalidLength of expectation: CheckLengthExpectation * actualLength: int option
    | OutOfRange of expectation: CheckRangeExpectation * actual: string option
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

    let internal constraintCodeFor failure =
        match failure with
        | CheckFailure.Blank -> Some "present"
        | CheckFailure.InvalidFormat "email" -> Some "email"
        | CheckFailure.InvalidFormat _ -> Some "pattern"
        | CheckFailure.InvalidLength(CheckLengthExpectation.MinimumLength _, _) -> Some "minLength"
        | CheckFailure.InvalidLength(CheckLengthExpectation.MaximumLength _, _) -> Some "maxLength"
        | CheckFailure.InvalidLength(CheckLengthExpectation.ExactLength _, _) -> Some "length"
        | CheckFailure.InvalidLength(CheckLengthExpectation.LengthBetween _, _) -> Some "lengthBetween"
        | CheckFailure.OutOfRange(CheckRangeExpectation.GreaterThan _, _) -> Some "greaterThan"
        | CheckFailure.OutOfRange(CheckRangeExpectation.LessThan _, _) -> Some "lessThan"
        | CheckFailure.OutOfRange(CheckRangeExpectation.AtLeast _, _) -> Some "atLeast"
        | CheckFailure.OutOfRange(CheckRangeExpectation.AtMost _, _) -> Some "atMost"
        | CheckFailure.OutOfRange(CheckRangeExpectation.Between _, _) -> Some "between"
        | CheckFailure.OutOfRange(CheckRangeExpectation.NotMultipleOf _, _) -> Some "multipleOf"
        | CheckFailure.NotOneOf _ -> Some "oneOf"
        | CheckFailure.Duplicate -> Some "distinct"
        | CheckFailure.Custom code -> Some code

    let ofParseError error =
        match error with
        | ParseError.MissingValue _ -> SchemaError.Blank
        | ParseError.InvalidFormat(target, _) -> SchemaError.InvalidFormat target
        | ParseError.OutOfRange(target, _) -> SchemaError.ParseOutOfRange target

    let ofCheckFailure failure =
        match failure with
        | CheckFailure.Blank -> SchemaError.Blank
        | CheckFailure.InvalidFormat expected -> SchemaError.InvalidFormat expected
        | CheckFailure.InvalidLength(expectation, actual) -> SchemaError.InvalidLength(expectation, actual)
        | CheckFailure.OutOfRange(expectation, actual) -> SchemaError.OutOfRange(expectation, actual)
        | CheckFailure.NotOneOf choices -> SchemaError.NotOneOf choices
        | CheckFailure.Duplicate -> SchemaError.Duplicate
        | CheckFailure.Custom code -> SchemaError.Custom(code, None)

    let render error =
        match error with
        | SchemaError.Omitted -> "This value was omitted."
        | SchemaError.Blank -> "This value must be present."
        | SchemaError.ExpectedScalar -> "Expected a scalar value."
        | SchemaError.ExpectedObject -> "Expected an object."
        | SchemaError.ExpectedMany -> "Expected a collection."
        | SchemaError.InvalidFormat expected -> $"Expected {expected} format."
        | SchemaError.ParseOutOfRange target -> $"{target} value is out of range."
        | SchemaError.InvalidLength(expectation, None) -> $"Length must be {lengthText expectation}."
        | SchemaError.InvalidLength(expectation, Some actual) -> $"Length must be {lengthText expectation}; got {actual}."
        | SchemaError.OutOfRange(expectation, None) -> $"Must be {rangeText expectation}."
        | SchemaError.OutOfRange(expectation, Some actual) -> $"Must be {rangeText expectation}; got {actual}."
        | SchemaError.NotOneOf choices -> $"Must be one of: {choices}."
        | SchemaError.Duplicate -> "Duplicate values are not allowed."
        | SchemaError.ConstructorFailed message -> message
        | SchemaError.Custom(_, Some message) -> message
        | SchemaError.Custom(code, None) -> code

#endif
