namespace Axial.Schema

open Axial

open Axial.Validation

module private DataPathConversion =
    let diagnosticsPath (path: DataPath) : Path =
        path
        |> List.map (function
            | DataPathSegment.Name name -> PathSegment.Name name
            | DataPathSegment.Index index -> PathSegment.Index index)

/// <summary>
/// A parse result that retains the original structured data for redisplay and error lookup.
/// </summary>
/// <remarks>
/// <para>
/// <c>RetainedParseResult</c> is an opt-in handoff value for boundaries that need the source representation after
/// parsing. Successful parses carry the trusted value in <see cref="P:Axial.Schema.RetainedParseResult`2.Result" />;
/// failed parses carry path-aware diagnostics while the
/// original <see cref="T:Axial.Schema.Data" /> remains available for redisplay and error lookup.
/// </para>
/// </remarks>
type RetainedParseResult<'value, 'error> =
    {
        /// <summary>The structured boundary data that was parsed.</summary>
        Input: Data
        /// <summary>The parsed model or path-aware parse diagnostics.</summary>
        Result: Result<'value, Diagnostics<'error>>
    }

    /// <summary>Returns <c>true</c> when input parsing produced a trusted model.</summary>
    member this.IsValid =
        match this.Result with
        | Ok _ -> true
        | Error _ -> false

    /// <summary>Returns the parsed value or raises when input parsing failed.</summary>
    /// <exception cref="T:System.InvalidOperationException">Thrown when input parsing failed.</exception>
    member this.Value =
        match this.Result with
        | Ok model -> model
        | Error _ -> invalidOp "Parsed input does not contain a value."

    /// <summary>Returns the parsed value when input parsing succeeded.</summary>
    member this.TryValue =
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

    /// <summary>Returns errors attached exactly to the supplied structured data path text.</summary>
    member this.ErrorsFor(path: string) =
        path
        |> DataPath.parse
        |> DataPathConversion.diagnosticsPath
        |> this.ErrorsFor

/// <summary>Functions for adapting parsed input, most notably to translate interpreter errors into domain errors.</summary>
[<RequireQualifiedAccess>]
module RetainedParseResult =
    /// <summary>Retains structured data alongside an existing parse result.</summary>
    let create input result : RetainedParseResult<'value, 'error> =
        { Input = input; Result = result }

    /// <summary>Maps a failed parse's errors to a domain or application error type, preserving the structured data and paths.</summary>
    /// <remarks>
    /// Use this at the boundary between schema input parsing and application code, where <c>SchemaError</c> (or any
    /// other interpreter error type) should become the caller's own domain/application error type before flowing
    /// further into the system. A successful parse is returned unchanged apart from its error type.
    /// </remarks>
    /// <param name="mapper">A function of type <c>'error -> 'nextError</c>.</param>
    /// <param name="parsed">The parsed input to map.</param>
    /// <returns>A <see cref="T:Axial.Schema.RetainedParseResult`2" /> with the same input and value, and mapped errors.</returns>
    /// <example>
    /// <code>
    /// let domainParsed = parsed |> RetainedParseResult.mapErrors SignupError.ofSchemaError
    /// </code>
    /// </example>
    let mapErrors (mapper: 'error -> 'nextError) (parsed: RetainedParseResult<'value, 'error>) : RetainedParseResult<'value, 'nextError> =
        {
            Input = parsed.Input
            Result = parsed.Result |> Result.mapError (Diagnostics.map mapper)
        }

    /// <summary>Renders a failed schema parse as default English display strings, preserving diagnostics paths.</summary>
    /// <remarks>
    /// This is the one-line display path for boundary parsing failures. Use <c>RetainedParseResult.mapErrors</c> when the same
    /// boundary failures should become an application-owned error type instead.
    /// </remarks>
    let renderErrors (parsed: RetainedParseResult<'value, SchemaError>) : string list =
        match parsed.Result with
        | Ok _ -> []
        | Error diagnostics -> SchemaError.renderDiagnostics diagnostics
