// A parse result that keeps the original boundary Data alongside the outcome, so forms can redisplay
// exactly what the user typed next to each field error and boundaries can audit raw input.
namespace Axial.Schema

open Axial

module private DataPathConversion =
    let schemaPath (path: DataPath) =
        path
        |> List.fold (fun current part ->
            let next =
                match part with
                | DataPathSegment.Name name -> Path.key name
                | DataPathSegment.Index index -> Path.index index

            Path.append current next) Path.root

/// <summary>A schema parse result that retains its original structured input.</summary>
type RetainedParseResult<'value> =
    {
        /// <summary>The structured boundary data that was parsed.</summary>
        Input: Data
        /// <summary>The parsed model or accumulated schema failures.</summary>
        Result: Result<'value, SchemaErrors>
    }

    /// <summary>Returns <c>true</c> when input parsing produced a trusted model.</summary>
    member this.IsValid = Result.isOk this.Result

    /// <summary>Returns the parsed value or raises when input parsing failed.</summary>
    member this.Value =
        match this.Result with
        | Ok model -> model
        | Error _ -> invalidOp "Parsed input does not contain a value."

    /// <summary>Returns the parsed value when input parsing succeeded.</summary>
    member this.TryValue = Result.toOption this.Result

    /// <summary>Returns path-aware issues from a failed parse, or an empty list after a successful parse.</summary>
    member this.Errors =
        match this.Result with
        | Ok _ -> []
        | Error errors -> SchemaErrors.toList errors

    /// <summary>Returns schema errors attached exactly to the supplied path.</summary>
    member this.ErrorsFor(path: Path) =
        if isNull path then nullArg (nameof path)

        this.Errors
        |> List.choose (fun issue ->
            if issue.Path = path then Some issue.Error else None)

    /// <summary>Returns schema errors attached exactly to the supplied structured data path.</summary>
    member this.ErrorsFor(path: string) =
        path
        |> DataPath.parse
        |> DataPathConversion.schemaPath
        |> this.ErrorsFor

/// <summary>Functions for creating and rendering retained schema parse results.</summary>
[<RequireQualifiedAccess>]
module RetainedParseResult =
    /// <summary>Retains structured data alongside an existing schema parse result.</summary>
    let create input result : RetainedParseResult<'value> =
        { Input = input; Result = result }

    /// <summary>Renders one line for every failed schema issue.</summary>
    let renderErrors (parsed: RetainedParseResult<'value>) : string list =
        parsed.Errors
        |> List.map (fun issue ->
            let message = SchemaError.render issue.Error
            let path = Path.format issue.Path
            if System.String.IsNullOrEmpty path then message else $"{path}: {message}")
