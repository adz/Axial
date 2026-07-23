namespace Axial.Schema

open System

type internal PathComponent =
    | KeyComponent of string
    | IndexComponent of int

/// <summary>An immutable location within structured schema input.</summary>
[<Sealed; AllowNullLiteral>]
type Path internal (components: PathComponent list) =
    member internal _.Components = components

    override _.Equals(other) =
        match other with
        | :? Path as path -> components = path.Components
        | _ -> false

    override _.GetHashCode() = hash components

    interface IComparable with
        member _.CompareTo(other) =
            match other with
            | :? Path as path -> compare components path.Components
            | _ -> invalidArg (nameof other) "Cannot compare values of different types."

/// <summary>Functions for building and formatting schema error paths.</summary>
[<RequireQualifiedAccess>]
module Path =
    /// <summary>The root of a schema value.</summary>
    let root = Path []

    /// <summary>A string field or map-key location.</summary>
    let key value =
        if isNull value then nullArg (nameof value)
        Path [ KeyComponent value ]

    /// <summary>A zero-based collection-item location.</summary>
    let index value =
        if value < 0 then invalidArg (nameof value) "A path index cannot be negative."
        Path [ IndexComponent value ]

    /// <summary>Appends a relative path to a parent path.</summary>
    let append (parent: Path) (child: Path) =
        if isNull parent then nullArg (nameof parent)
        if isNull child then nullArg (nameof child)
        Path(parent.Components @ child.Components)

    /// <summary>Formats a path with dot-separated keys and bracketed indexes.</summary>
    let format (path: Path) =
        if isNull path then nullArg (nameof path)

        path.Components
        |> List.fold (fun text part ->
            match part with
            | KeyComponent key when String.IsNullOrEmpty text -> key
            | KeyComponent key -> $"{text}.{key}"
            | IndexComponent index -> $"{text}[{index}]") ""

    /// <summary>Folds over string keys and integer indexes without exposing a path-segment type.</summary>
    let fold keyFolder indexFolder state (path: Path) =
        if isNull (box keyFolder) then nullArg (nameof keyFolder)
        if isNull (box indexFolder) then nullArg (nameof indexFolder)
        if isNull path then nullArg (nameof path)

        path.Components
        |> List.fold (fun current part ->
            match part with
            | KeyComponent key -> keyFolder current key
            | IndexComponent index -> indexFolder current index) state

/// <summary>One schema failure and its complete structural location.</summary>
type SchemaIssue =
    {
        /// <summary>The location of the failure.</summary>
        Path: Path
        /// <summary>The schema failure.</summary>
        Error: SchemaError
    }

/// <summary>One or more accumulated schema failures.</summary>
[<Sealed; AllowNullLiteral>]
type SchemaErrors internal (issues: SchemaIssue list) =
    member internal _.Issues = issues

/// <summary>Functions for inspecting and rendering accumulated schema failures.</summary>
[<RequireQualifiedAccess>]
module SchemaErrors =
    let internal empty = SchemaErrors []

    let internal singleton path error =
        SchemaErrors [ { Path = path; Error = error } ]

    let internal merge (left: SchemaErrors) (right: SchemaErrors) =
        left.Issues @ right.Issues
        |> List.sortBy (fun issue -> issue.Path)
        |> SchemaErrors

    let internal collect errors =
        errors |> List.fold merge empty

    /// <summary>Returns failures in deterministic path order.</summary>
    let toList (errors: SchemaErrors) =
        if isNull errors then nullArg (nameof errors)
        errors.Issues

    /// <summary>Returns the number of accumulated failures.</summary>
    let count errors = toList errors |> List.length

    /// <summary>Reports whether the collection contains no failures.</summary>
    let isEmpty errors = toList errors |> List.isEmpty

    /// <summary>Renders one line per failure.</summary>
    let toString errors =
        toList errors
        |> List.map (fun issue ->
            let message = SchemaError.render issue.Error
            let path = Path.format issue.Path
            if String.IsNullOrEmpty path then message else $"{path}: {message}")
        |> String.concat Environment.NewLine

[<RequireQualifiedAccess>]
module internal SchemaResult =
    let ok value : Result<'value, SchemaErrors> = Ok value
    let error errors : Result<'value, SchemaErrors> = Error errors
    let map mapper result = Result.map mapper result
    let toResult result = result

    let map2 mapper left right =
        match left, right with
        | Ok leftValue, Ok rightValue -> Ok(mapper leftValue rightValue)
        | Error leftErrors, Error rightErrors -> Error(SchemaErrors.merge leftErrors rightErrors)
        | Error errors, _
        | _, Error errors -> Error errors
