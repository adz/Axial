namespace Axial.Data

open System
open System.Text

/// <summary>A segment in a structured data path.</summary>
/// <remarks>
/// <para>
/// Structured data paths address boundary data by source field names and zero-based collection indexes. They are intentionally
/// separate from diagnostics graphs, but can be lowered to diagnostics paths when schema input errors are interpreted.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
type DataPathSegment =
    /// <summary>A named source field or object member.</summary>
    | Name of string
    /// <summary>A zero-based collection index.</summary>
    | Index of int

/// <summary>A path that addresses a location in structured data.</summary>
type DataPath = DataPathSegment list

/// <summary>Helpers for constructing, parsing, and rendering structured data paths.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module DataPath =
    let private validateName (name: string) =
        if isNull name then
            nullArg (nameof name)

        if name = "" then
            invalidArg (nameof name) "Input path names cannot be empty."

    let private validateIndex index =
        if index < 0 then
            invalidArg (nameof index) "Input path indexes must be zero or greater."

    let private isBareName (name: string) =
        not (String.IsNullOrEmpty name)
        && name.IndexOfAny([| '.'; '['; ']'; '"'; '\\' |]) < 0

    let private quoteName (name: string) =
        let builder = StringBuilder()
        builder.Append("[\"") |> ignore

        name
        |> Seq.iter (function
            | '\\' -> builder.Append("\\\\") |> ignore
            | '"' -> builder.Append("\\\"") |> ignore
            | value -> builder.Append(value) |> ignore)

        builder.Append("\"]").ToString()

    let private renderName name =
        validateName name

        if isBareName name then name else quoteName name

    let private parseError () = None

    let private parseBareName (text: string) start =
        let mutable index = start

        while index < text.Length && text[index] <> '.' && text[index] <> '[' do
            if text[index] = ']' then
                index <- text.Length + 1
            else
                index <- index + 1

        if index > text.Length || index = start then
            None
        else
            Some(text.Substring(start, index - start), index)

    let private parseBracket (text: string) start =
        let contentStart = start + 1

        if contentStart >= text.Length then
            parseError ()
        elif text[contentStart] = '"' then
            let builder = StringBuilder()
            let mutable index = contentStart + 1
            let mutable closed = false

            while index < text.Length && not closed do
                match text[index] with
                | '\\' when index + 1 < text.Length ->
                    builder.Append(text[index + 1]) |> ignore
                    index <- index + 2
                | '"' ->
                    closed <- true
                    index <- index + 1
                | value ->
                    builder.Append(value) |> ignore
                    index <- index + 1

            if not closed || index >= text.Length || text[index] <> ']' then
                parseError ()
            else
                let name = builder.ToString()

                if name = "" then
                    parseError ()
                else
                    Some(DataPathSegment.Name name, index + 1)
        else
            let mutable index = contentStart

            while index < text.Length && Char.IsDigit text[index] do
                index <- index + 1

            if index = contentStart || index >= text.Length || text[index] <> ']' then
                parseError ()
            else
                match Int32.TryParse(text.Substring(contentStart, index - contentStart)) with
                | true, value -> Some(DataPathSegment.Index value, index + 1)
                | false, _ -> parseError ()

    /// <summary>The root structured data path.</summary>
    let empty : DataPath = []

    /// <summary>Creates a one-segment path for a named source field.</summary>
    let name (name: string) : DataPath =
        validateName name
        [ DataPathSegment.Name name ]

    /// <summary>Creates a one-segment path for a zero-based collection index.</summary>
    let index (index: int) : DataPath =
        validateIndex index
        [ DataPathSegment.Index index ]

    /// <summary>Appends a named source field segment to an input path.</summary>
    let appendName (name: string) (path: DataPath) : DataPath =
        validateName name
        path @ [ DataPathSegment.Name name ]

    /// <summary>Appends a zero-based collection index segment to an input path.</summary>
    let appendIndex (index: int) (path: DataPath) : DataPath =
        validateIndex index
        path @ [ DataPathSegment.Index index ]

    /// <summary>Creates a path from validated segments.</summary>
    let ofSegments (segments: DataPathSegment seq) : DataPath =
        if isNull (box segments) then
            nullArg (nameof segments)

        segments
        |> Seq.map (function
            | DataPathSegment.Name name ->
                validateName name
                DataPathSegment.Name name
            | DataPathSegment.Index index ->
                validateIndex index
                DataPathSegment.Index index)
        |> Seq.toList

    /// <summary>Returns the segments in an input path.</summary>
    let segments (path: DataPath) : DataPathSegment list = path

    /// <summary>Renders a structured data path using names, dot separators, and bracketed indexes.</summary>
    let toString (path: DataPath) : string =
        let builder = StringBuilder()

        path
        |> List.iteri (fun position segment ->
            match segment with
            | DataPathSegment.Name name ->
                let rendered = renderName name

                if position = 0 || rendered.StartsWith("[", StringComparison.Ordinal) then
                    builder.Append(rendered) |> ignore
                else
                    builder.Append('.').Append(rendered) |> ignore
            | DataPathSegment.Index index ->
                validateIndex index
                builder.Append('[').Append(index).Append(']') |> ignore)

        builder.ToString()

    /// <summary>Attempts to parse a structured data path such as <c>contacts[1].value</c>.</summary>
    let tryParse (text: string) : DataPath option =
        if isNull text then
            nullArg (nameof text)

        if text = "" then
            Some empty
        else
            let rec loop index expectSegment segments =
                if index = text.Length then
                    if expectSegment then None else Some(List.rev segments)
                else
                    match text[index] with
                    | '.' when not expectSegment -> loop (index + 1) true segments
                    | '[' ->
                        match parseBracket text index with
                        | Some(segment, nextIndex) -> loop nextIndex false (segment :: segments)
                        | None -> None
                    | _ when expectSegment ->
                        match parseBareName text index with
                        | Some(name, nextIndex) -> loop nextIndex false (DataPathSegment.Name name :: segments)
                        | None -> None
                    | _ -> None

            loop 0 true []

    /// <summary>Parses a structured data path or raises <see cref="T:System.FormatException" /> when the text is invalid.</summary>
    let parse (text: string) : DataPath =
        match tryParse text with
        | Some path -> path
        | None -> raise (FormatException($"Invalid input path: {text}"))
