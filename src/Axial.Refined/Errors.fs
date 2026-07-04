namespace Axial.Refined

open Axial.ErrorHandling

/// <summary>Primitive parse failures returned by <c>Parse</c> helpers.</summary>
type ParseError =
    /// <summary>The input was missing for the target primitive type.</summary>
    | MissingValue of target: string
    /// <summary>The input did not match the expected format for the target primitive type.</summary>
    | InvalidFormat of target: string * input: string
    /// <summary>The input was outside the supported range for the target primitive type.</summary>
    | OutOfRange of target: string * input: string

/// <summary>Structural failures returned by built-in refinement constructors and the <c>refine { }</c> builder.</summary>
type RefinementError =
    /// <summary>A primitive parse operation failed before refinement.</summary>
    | ParseFailed of ParseError
    /// <summary>An executable <see cref="T:Axial.ErrorHandling.Check`1" /> program run against the target refined type
    /// failed. Carries the same structured <see cref="T:Axial.ErrorHandling.CheckFailure" /> values the check program
    /// produced, so callers never need to reinterpret or re-describe them.</summary>
    | CheckFailed of target: string * failures: CheckFailure list
    /// <summary>The value had an invalid structure for the target refined type that a single-value
    /// <see cref="T:Axial.ErrorHandling.Check`1" /> program cannot express, such as a cross-field ordering
    /// invariant.</summary>
    | InvalidStructure of target: string * reason: string

/// <summary>Renders <see cref="T:Axial.Refined.RefinementError" /> values as human-readable messages.</summary>
module RefinementError =
    /// <summary>Renders a single refinement error using the supplied <see cref="T:Axial.ErrorHandling.CheckFailureResources" />
    /// for its <c>CheckFailed</c> failures.</summary>
    let describeWith (resources: CheckFailureResources) (error: RefinementError) : string =
        match error with
        | ParseFailed(ParseError.MissingValue target) -> $"{target} is required"
        | ParseFailed(ParseError.InvalidFormat(target, input)) -> $"{target} value '{input}' has an invalid format"
        | ParseFailed(ParseError.OutOfRange(target, input)) -> $"{target} value '{input}' is out of range"
        | CheckFailed(target, failures) -> $"{target}: {CheckFailure.describeAllWith resources failures}"
        | InvalidStructure(target, reason) -> $"{target}: {reason}"

    /// <summary>Renders a single refinement error as a human-readable message, using
    /// <see cref="P:Axial.ErrorHandling.CheckFailure.english" /> for its <c>CheckFailed</c> failures.</summary>
    let describe (error: RefinementError) : string =
        describeWith CheckFailure.english error
