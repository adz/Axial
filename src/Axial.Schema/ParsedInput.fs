namespace Axial.Schema

open Axial.Validation

/// <summary>
/// The result of parsing boundary input through a schema while retaining the original raw input.
/// </summary>
/// <remarks>
/// <para>
/// <c>ParsedInput</c> is the stable handoff value for schema input parsing. Successful parses carry the trusted model in
/// <see cref="P:Axial.Schema.ParsedInput`2.Result" />; failed parses carry path-aware diagnostics while the
/// original <see cref="T:Axial.Schema.RawInput" /> remains available for redisplay and error lookup.
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

/// <summary>Functions for adapting parsed input, most notably to translate interpreter errors into domain errors.</summary>
[<RequireQualifiedAccess>]
module ParsedInput =
    /// <summary>Maps a failed parse's errors to a domain or application error type, preserving the raw input and paths.</summary>
    /// <remarks>
    /// Use this at the boundary between schema input parsing and application code, where <c>SchemaError</c> (or any
    /// other interpreter error type) should become the caller's own domain/application error type before flowing
    /// further into the system. A successful parse is returned unchanged apart from its error type.
    /// </remarks>
    /// <param name="mapper">A function of type <c>'error -> 'nextError</c>.</param>
    /// <param name="parsed">The parsed input to map.</param>
    /// <returns>A <see cref="T:Axial.Schema.ParsedInput`2" /> with the same input and model, and mapped errors.</returns>
    /// <example>
    /// <code>
    /// let domainParsed = parsed |> ParsedInput.mapErrors SignupError.ofSchemaError
    /// </code>
    /// </example>
    let mapErrors (mapper: 'error -> 'nextError) (parsed: ParsedInput<'model, 'error>) : ParsedInput<'model, 'nextError> =
        {
            Input = parsed.Input
            Result = parsed.Result |> Result.mapError (Diagnostics.map mapper)
        }

    /// <summary>Renders a failed schema parse as default English display strings, preserving diagnostics paths.</summary>
    /// <remarks>
    /// This is the one-line display path for boundary parsing failures. Use <c>ParsedInput.mapErrors</c> when the same
    /// boundary failures should become an application-owned error type instead.
    /// </remarks>
    let renderErrors (parsed: ParsedInput<'model, SchemaError>) : string list =
        match parsed.Result with
        | Ok _ -> []
        | Error diagnostics -> SchemaError.renderDiagnostics diagnostics
