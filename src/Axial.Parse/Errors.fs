namespace Axial.Parse

/// <summary>Primitive parse failures returned by <c>Parse</c> helpers.</summary>
type ParseError =
    /// <summary>The input was missing for the target primitive type.</summary>
    | MissingValue of target: string
    /// <summary>The input did not match the expected format for the target primitive type.</summary>
    | InvalidFormat of target: string * input: string
    /// <summary>The input was outside the supported range for the target primitive type.</summary>
    | OutOfRange of target: string * input: string
