namespace Axial.Validation.Schema

/// <summary>
/// Source-agnostic raw input captured at a data boundary before schema parsing and diagnostics interpretation.
/// </summary>
/// <remarks>
/// <para>
/// <c>RawInput</c> models the small set of shapes shared by form posts, command-line arguments, configuration, JSON-like
/// values, and other boundary sources. It deliberately does not carry source-specific metadata, parsed model values, or
/// diagnostics; those concerns belong to later input parsing and validation layers.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
type RawInput =
    /// <summary>The source did not provide a value for the requested input.</summary>
    | Missing
    /// <summary>A single scalar value represented in its boundary-facing text form.</summary>
    | Scalar of value: string
    /// <summary>An ordered collection of raw input items.</summary>
    | Many of items: RawInput list
    /// <summary>A named collection of raw input fields.</summary>
    | Object of fields: Map<string, RawInput>
