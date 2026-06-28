namespace Axial.Refined

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
    /// <summary>The value had an invalid format for the target refined type.</summary>
    | InvalidFormat of target: string * reason: string
    /// <summary>The value was outside the accepted range for the target refined type.</summary>
    | OutOfRange of target: string * reason: string
    /// <summary>The value required for the target refined type was missing.</summary>
    | MissingValue of target: string
    /// <summary>The value had an invalid structure for the target refined type.</summary>
    | InvalidStructure of target: string * reason: string
