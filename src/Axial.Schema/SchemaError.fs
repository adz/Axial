// SchemaError: the one portable error vocabulary every schema interpreter reports through, so
// parsing, checking, and refinement failures render and compose the same way regardless of which
// interpreter raised them. AXIAL_SCHEMA_CORE_ONLY trims the Refined-dependent cases for
// consumers that only need the core shape.
namespace Axial.Schema

open Axial.Parse

open Axial.Constraint

/// <summary>Schema input, checking, and contextual rule failures attached to diagnostics paths.</summary>
/// <remarks>
/// The parse/check axis is the organising split. Parsing cases mean the input could not be read as the declared
/// type at all; <c>Violation</c> means it was read and then failed its constraint. Constraint failures are never
/// lowered into a parse-shaped case, because a lowering that discards the atom forces consumers back to
/// reconstructing constraint identity from strings.
/// </remarks>
[<RequireQualifiedAccess>]
type SchemaError =
    /// <summary>Required boundary input was not supplied.</summary>
    | Omitted
    /// <summary>Boundary input was present but carried no value. The parse-side lowering of a missing value.</summary>
    | Blank
    /// <summary>A scalar was expected at this path.</summary>
    | ExpectedScalar
    /// <summary>An object was expected at this path.</summary>
    | ExpectedObject
    /// <summary>A collection was expected at this path.</summary>
    | ExpectedMany
    /// <summary>The input could not be read as the named target type.</summary>
    | InvalidFormat of expected: string
    /// <summary>The input was well-formed but outside the target type's representable range.</summary>
    | ParseOutOfRange of target: string
    /// <summary>A union or enum discriminator did not name one of the declared cases.</summary>
    /// <remarks>
    /// A parsing failure, not a constraint violation: the input could not be read as any declared case, so no
    /// typed value exists for a constraint to reject.
    /// </remarks>
    | UnknownTag of choices: string
    /// <summary>The value was read successfully and then failed its constraint.</summary>
    | Violation of violation: Violation
    /// <summary>The model constructor rejected an otherwise admissible set of field values.</summary>
    | ConstructorFailed of message: string
    /// <summary>A Schema-owned intrinsic check failed.</summary>
    | Custom of code: string * message: string option

#if !AXIAL_SCHEMA_CORE_ONLY
/// <summary>Functions for lowering and rendering boundary schema failures.</summary>
[<RequireQualifiedAccess>]
module SchemaError =
    /// <summary>Lowers a parse failure. Parsing failures never become constraint violations.</summary>
    /// <example><code>ParseError.MissingValue "int" |> SchemaError.ofParseError</code></example>
    let ofParseError error =
        match error with
        | ParseError.MissingValue _ -> SchemaError.Blank
        | ParseError.InvalidFormat(target, _) -> SchemaError.InvalidFormat target
        | ParseError.OutOfRange(target, _) -> SchemaError.ParseOutOfRange target

    /// <summary>Renders a schema error as an English sentence.</summary>
    /// <remarks>Constraint violations delegate to <c>Violation.render</c>, so there is one rendering catalogue.</remarks>
    /// <example><code>SchemaError.render (SchemaError.InvalidFormat "int") // "Expected int format."</code></example>
    let render error =
        match error with
        | SchemaError.Omitted -> "This value was omitted."
        | SchemaError.Blank -> "This value must be present."
        | SchemaError.ExpectedScalar -> "Expected a scalar value."
        | SchemaError.ExpectedObject -> "Expected an object."
        | SchemaError.ExpectedMany -> "Expected a collection."
        | SchemaError.InvalidFormat expected -> $"Expected {expected} format."
        | SchemaError.ParseOutOfRange target -> $"{target} value is out of range."
        | SchemaError.UnknownTag choices -> $"Must be one of: {choices}."
        | SchemaError.Violation violation ->
            let rendered = Violation.render violation
            $"{System.Char.ToUpperInvariant rendered.[0]}{rendered.Substring 1}."
        | SchemaError.ConstructorFailed message -> message
        | SchemaError.Custom(_, Some message) -> message
        | SchemaError.Custom(code, None) -> code

#endif
