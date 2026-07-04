namespace Axial.Validation.Schema

open Axial.Validation

/// <summary>
/// The result of parsing boundary input through a schema while retaining the original raw input.
/// </summary>
/// <remarks>
/// <para>
/// <c>ParsedInput</c> is the stable handoff value for schema input parsing. Successful parses carry the trusted model in
/// <see cref="P:Axial.Validation.Schema.ParsedInput`2.Result" />; failed parses carry path-aware diagnostics while the
/// original <see cref="T:Axial.Validation.Schema.RawInput" /> remains available for redisplay and error lookup.
/// </para>
/// </remarks>
type ParsedInput<'model, 'error> =
    {
        /// <summary>The raw boundary input that was parsed.</summary>
        Input: RawInput
        /// <summary>The parsed model or path-aware parse diagnostics.</summary>
        Result: Result<'model, Diagnostics<'error>>
    }
