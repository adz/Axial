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

    /// <summary>Returns <c>true</c> when input parsing produced a trusted model.</summary>
    member this.IsValid =
        match this.Result with
        | Ok _ -> true
        | Error _ -> false

    /// <summary>Returns the trusted model or raises when input parsing failed.</summary>
    /// <exception cref="T:System.InvalidOperationException">Thrown when input parsing failed.</exception>
    member this.Model =
        match this.Result with
        | Ok model -> model
        | Error _ -> invalidOp "Parsed input does not contain a valid model."

    /// <summary>Returns the trusted model when input parsing succeeded.</summary>
    member this.TryModel =
        match this.Result with
        | Ok model -> Some model
        | Error _ -> None

    /// <summary>Returns flattened path-aware errors from a failed parse, or an empty list for a successful parse.</summary>
    member this.Errors =
        match this.Result with
        | Ok _ -> []
        | Error diagnostics -> Diagnostics.flatten diagnostics

    /// <summary>Returns errors attached exactly to the supplied diagnostics path.</summary>
    member this.ErrorsFor(path: Path) =
        this.Errors
        |> List.choose (fun diagnostic ->
            if diagnostic.Path = path then
                Some diagnostic.Error
            else
                None)

    /// <summary>Returns errors attached exactly to the supplied raw input path text.</summary>
    member this.ErrorsFor(path: string) =
        path
        |> InputPath.parse
        |> InputPath.toDiagnosticsPath
        |> this.ErrorsFor
