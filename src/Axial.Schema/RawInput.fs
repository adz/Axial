namespace Axial.Schema

open System
open System.Collections.Specialized
open System.Text
open Axial.Validation

/// <summary>A segment in a raw input path.</summary>
/// <remarks>
/// <para>
/// Raw input paths address boundary data by source field names and zero-based collection indexes. They are intentionally
/// separate from diagnostics graphs, but can be lowered to diagnostics paths when schema input errors are interpreted.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
type InputPathSegment =
    /// <summary>A named source field or object member.</summary>
    | Name of string
    /// <summary>A zero-based collection index.</summary>
    | Index of int

/// <summary>A path that addresses a location in raw input.</summary>
type InputPath = InputPathSegment list

/// <summary>Helpers for constructing, parsing, and rendering raw input paths.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module InputPath =
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
                    Some(InputPathSegment.Name name, index + 1)
        else
            let mutable index = contentStart

            while index < text.Length && Char.IsDigit text[index] do
                index <- index + 1

            if index = contentStart || index >= text.Length || text[index] <> ']' then
                parseError ()
            else
                match Int32.TryParse(text.Substring(contentStart, index - contentStart)) with
                | true, value -> Some(InputPathSegment.Index value, index + 1)
                | false, _ -> parseError ()

    /// <summary>The root raw input path.</summary>
    let empty : InputPath = []

    /// <summary>Creates a one-segment path for a named source field.</summary>
    let name (name: string) : InputPath =
        validateName name
        [ InputPathSegment.Name name ]

    /// <summary>Creates a one-segment path for a zero-based collection index.</summary>
    let index (index: int) : InputPath =
        validateIndex index
        [ InputPathSegment.Index index ]

    /// <summary>Appends a named source field segment to an input path.</summary>
    let appendName (name: string) (path: InputPath) : InputPath =
        validateName name
        path @ [ InputPathSegment.Name name ]

    /// <summary>Appends a zero-based collection index segment to an input path.</summary>
    let appendIndex (index: int) (path: InputPath) : InputPath =
        validateIndex index
        path @ [ InputPathSegment.Index index ]

    /// <summary>Creates a path from validated segments.</summary>
    let ofSegments (segments: InputPathSegment seq) : InputPath =
        if isNull (box segments) then
            nullArg (nameof segments)

        segments
        |> Seq.map (function
            | InputPathSegment.Name name ->
                validateName name
                InputPathSegment.Name name
            | InputPathSegment.Index index ->
                validateIndex index
                InputPathSegment.Index index)
        |> Seq.toList

    /// <summary>Returns the segments in an input path.</summary>
    let segments (path: InputPath) : InputPathSegment list = path

    /// <summary>Renders a raw input path using names, dot separators, and bracketed indexes.</summary>
    let toString (path: InputPath) : string =
        let builder = StringBuilder()

        path
        |> List.iteri (fun position segment ->
            match segment with
            | InputPathSegment.Name name ->
                let rendered = renderName name

                if position = 0 || rendered.StartsWith("[", StringComparison.Ordinal) then
                    builder.Append(rendered) |> ignore
                else
                    builder.Append('.').Append(rendered) |> ignore
            | InputPathSegment.Index index ->
                validateIndex index
                builder.Append('[').Append(index).Append(']') |> ignore)

        builder.ToString()

    /// <summary>Attempts to parse a raw input path such as <c>contacts[1].value</c>.</summary>
    let tryParse (text: string) : InputPath option =
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
                        | Some(name, nextIndex) -> loop nextIndex false (InputPathSegment.Name name :: segments)
                        | None -> None
                    | _ -> None

            loop 0 true []

    /// <summary>Parses a raw input path or raises <see cref="T:System.FormatException" /> when the text is invalid.</summary>
    let parse (text: string) : InputPath =
        match tryParse text with
        | Some path -> path
        | None -> raise (FormatException($"Invalid input path: {text}"))

    /// <summary>Converts a raw input path into a diagnostics path with name and index segments.</summary>
    let toDiagnosticsPath (path: InputPath) : Axial.Validation.Path =
        path
        |> List.map (function
            | InputPathSegment.Name name ->
                validateName name
                PathSegment.Name name
            | InputPathSegment.Index index ->
                validateIndex index
                PathSegment.Index index)

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

/// <summary>A small dependency-free value model for adapting JSON-shaped data into <see cref="T:Axial.Schema.RawInput" />.</summary>
[<RequireQualifiedAccess>]
type JsonLikeValue =
    /// <summary>A JSON null value.</summary>
    | Null
    /// <summary>A JSON string value.</summary>
    | String of string
    /// <summary>A JSON number, preserved in its boundary-facing text form.</summary>
    | Number of string
    /// <summary>A JSON boolean value.</summary>
    | Bool of bool
    /// <summary>A JSON array value.</summary>
    | Array of JsonLikeValue list
    /// <summary>A JSON object value.</summary>
    | Object of Map<string, JsonLikeValue>

/// <summary>Helpers for inspecting source-agnostic raw input.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module RawInput =
    type private ConfigurationNode =
        | Value of RawInput
        | Branch of Map<string, ConfigurationNode>

    let private tryRedisplayValue (input: RawInput) =
        match input with
        | RawInput.Missing -> Some ""
        | RawInput.Scalar value -> Some value
        | RawInput.Many _
        | RawInput.Object _ -> None

    let private ensureName (name: string) =
        if isNull name then
            nullArg (nameof name)

        if name = "" then
            invalidArg (nameof name) "Raw input field names cannot be empty."

        name

    let private ensureValues name values =
        if isNull (box values) then
            nullArg name

        values

    let private scalarOrMissing (value: string) =
        if isNull value then RawInput.Missing else RawInput.Scalar value

    let private fieldValue (values: string list) =
        match values with
        | [] -> RawInput.Missing
        | [ value ] -> scalarOrMissing value
        | values -> values |> List.map scalarOrMissing |> RawInput.Many

    let private objectFromGroupedValues (values: seq<string * string list>) =
        values
        |> Seq.map (fun (name, values) -> ensureName name, fieldValue values)
        |> Map.ofSeq
        |> RawInput.Object

    let private addField name value fields =
        let name = ensureName name

        let append existing =
            match existing with
            | None -> Some value
            | Some(RawInput.Many values) -> Some(RawInput.Many(values @ [ value ]))
            | Some existing -> Some(RawInput.Many [ existing; value ])

        fields |> Map.change name append

    let private tryNonNegativeInt (text: string) =
        match Int32.TryParse text with
        | true, value when value >= 0 -> Some value
        | _ -> None

    let private insertConfigurationValue (segments: string list) value node =
        let rec insert remaining current =
            match remaining, current with
            // Last write wins, matching .NET configuration layering — except that a null value never
            // overrides an existing section, because IConfiguration.AsEnumerable() emits every section
            // key with a null value alongside that section's children.
            | [], Branch children when not children.IsEmpty && value = RawInput.Missing -> current
            | [], _ -> Value value
            // A later section path replaces an earlier scalar at the same key: last write wins there too.
            | _ :: _, Value _ -> insert remaining (Branch Map.empty)
            | segment :: rest, Branch children ->
                let segment = ensureName segment
                let child = children |> Map.tryFind segment |> Option.defaultValue (Branch Map.empty)
                Branch(children |> Map.add segment (insert rest child))

        insert segments node

    let private configurationNodeToRawInput node =
        let rec convert node =
            match node with
            | Value value -> value
            | Branch children when children.IsEmpty -> RawInput.Object Map.empty
            | Branch children ->
                let indexed =
                    children
                    |> Map.toList
                    |> List.map (fun (key, child) -> tryNonNegativeInt key |> Option.map (fun index -> index, child))

                if indexed |> List.forall Option.isSome then
                    let byIndex = indexed |> List.choose id |> Map.ofList
                    let maximum = byIndex |> Map.toSeq |> Seq.map fst |> Seq.max

                    [ for index in 0..maximum ->
                          byIndex
                          |> Map.tryFind index
                          |> Option.map convert
                          |> Option.defaultValue RawInput.Missing ]
                    |> RawInput.Many
                else
                    children
                    |> Map.map (fun _ child -> convert child)
                    |> RawInput.Object

        convert node

    /// <summary>Builds object-shaped raw input from a list of named raw input fields.</summary>
    /// <remarks>When a field name occurs more than once, the last value wins.</remarks>
    /// <example>
    /// <code>
    /// [ "email", RawInput.Scalar "ada@example.com"
    ///   "age", RawInput.Scalar "42" ]
    /// |> RawInput.objectOfList
    /// </code>
    /// </example>
    let objectOfList (fields: (string * RawInput) list) : RawInput =
        ensureValues (nameof fields) fields
        |> List.map (fun (name, value) -> ensureName name, value)
        |> Map.ofList
        |> RawInput.Object

    /// <summary>Builds object-shaped raw input from a map of scalar field values.</summary>
    let ofMap (values: Map<string, string>) : RawInput =
        if isNull (box values) then
            nullArg (nameof values)

        values
        |> Map.toSeq
        |> Seq.map (fun (name, value) -> ensureName name, scalarOrMissing value)
        |> Map.ofSeq
        |> RawInput.Object

    /// <summary>Builds object-shaped raw input from a .NET dictionary of scalar field values.</summary>
    /// <remarks>
    /// A C#-friendly equivalent of <c>ofMap</c>: takes <see cref="T:System.Collections.Generic.IDictionary`2" />
    /// instead of an F# <c>Map</c>, so callers do not need to construct an F# map value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="values" /> is null.</exception>
    let ofDictionary (values: System.Collections.Generic.IDictionary<string, string>) : RawInput =
        if isNull values then
            nullArg (nameof values)

        values
        |> Seq.map (fun pair -> ensureName pair.Key, scalarOrMissing pair.Value)
        |> Map.ofSeq
        |> RawInput.Object

    /// <summary>Builds object-shaped raw input from name/value pairs, grouping repeated names into <c>Many</c>.</summary>
    let ofNameValues (values: seq<string * string>) : RawInput =
        ensureValues (nameof values) values
        |> Seq.groupBy fst
        |> Seq.map (fun (name, grouped) -> name, grouped |> Seq.map snd |> Seq.toList)
        |> objectFromGroupedValues

    /// <summary>Builds object-shaped raw input from a .NET name-value collection.</summary>
    let ofNameValueCollection (values: NameValueCollection) : RawInput =
        if isNull values then
            nullArg (nameof values)

        values.AllKeys
        |> Seq.map (fun name ->
            let name = ensureName name

            let fieldValues =
                match values.GetValues name with
                | null -> []
                | fieldValues -> fieldValues |> Array.toList

            name, fieldValues)
        |> objectFromGroupedValues

    /// <summary>
    /// Builds raw input from command-line arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supports <c>--name value</c>, <c>--name=value</c>, <c>-n value</c>, boolean flags, <c>--no-name</c>, and repeated
    /// options. Positional arguments are stored under the <c>_</c> field as a collection.
    /// </para>
    /// </remarks>
    let ofCliArgs (args: seq<string>) : RawInput =
        let args = ensureValues (nameof args) args |> Seq.toList

        let rec loop remaining fields positionals =
            match remaining with
            | [] ->
                let fields =
                    match List.rev positionals with
                    | [] -> fields
                    | positionals -> fields |> addField "_" (positionals |> List.map RawInput.Scalar |> RawInput.Many)

                RawInput.Object fields
            | "--" :: rest ->
                loop [] fields (List.rev rest @ positionals)
            | arg :: rest when isNull arg ->
                nullArg (nameof args)
            | arg :: rest when arg.StartsWith("--no-", StringComparison.Ordinal) && arg.Length > 5 ->
                let name = arg.Substring 5
                loop rest (fields |> addField name (RawInput.Scalar "false")) positionals
            | arg :: rest when arg.StartsWith("--", StringComparison.Ordinal) && arg.Length > 2 ->
                let optionText = arg.Substring 2
                let equalsIndex = optionText.IndexOf('=')

                if equalsIndex >= 0 then
                    let name = optionText.Substring(0, equalsIndex)
                    let value = optionText.Substring(equalsIndex + 1)
                    loop rest (fields |> addField name (RawInput.Scalar value)) positionals
                else
                    match rest with
                    | value :: tail when not (isNull value) && not (value.StartsWith("-", StringComparison.Ordinal)) ->
                        loop tail (fields |> addField optionText (RawInput.Scalar value)) positionals
                    | _ -> loop rest (fields |> addField optionText (RawInput.Scalar "true")) positionals
            | arg :: rest when arg.StartsWith("-", StringComparison.Ordinal) && arg.Length > 1 ->
                let optionText = arg.Substring 1
                let equalsIndex = optionText.IndexOf('=')

                if equalsIndex >= 0 then
                    let name = optionText.Substring(0, equalsIndex)
                    let value = optionText.Substring(equalsIndex + 1)
                    loop rest (fields |> addField name (RawInput.Scalar value)) positionals
                else
                    match rest with
                    | value :: tail when not (isNull value) && not (value.StartsWith("-", StringComparison.Ordinal)) ->
                        loop tail (fields |> addField optionText (RawInput.Scalar value)) positionals
                    | _ -> loop rest (fields |> addField optionText (RawInput.Scalar "true")) positionals
            | arg :: rest -> loop rest fields (arg :: positionals)

        loop args Map.empty []

    /// <summary>Builds raw input from dependency-free JSON-shaped values.</summary>
    let rec ofJsonLikeValue (value: JsonLikeValue) : RawInput =
        match value with
        | JsonLikeValue.Null -> RawInput.Missing
        | JsonLikeValue.String value -> scalarOrMissing value
        | JsonLikeValue.Number value -> scalarOrMissing value
        | JsonLikeValue.Bool value -> RawInput.Scalar(if value then "true" else "false")
        | JsonLikeValue.Array values -> values |> List.map ofJsonLikeValue |> RawInput.Many
        | JsonLikeValue.Object fields ->
            fields
            |> Map.toSeq
            |> Seq.map (fun (name, value) -> ensureName name, ofJsonLikeValue value)
            |> Map.ofSeq
            |> RawInput.Object

#if NET8_0_OR_GREATER && !FABLE_COMPILER
    /// <summary>Builds raw input from a <see cref="T:System.Text.Json.JsonElement" />.</summary>
    /// <remarks>
    /// <para>
    /// This is the boundary adapter for JSON bodies parsed with <c>System.Text.Json</c>, such as ASP.NET Core request
    /// payloads: convert the element once, then parse it with <c>Schema.parse</c> to get path-aware diagnostics or a
    /// trusted model. JSON null and undefined become <c>Missing</c>, numbers keep their exact boundary text, and
    /// booleans become <c>"true"</c>/<c>"false"</c> scalars.
    /// </para>
    /// <para>
    /// The adapter is available on .NET 8+ targets where <c>System.Text.Json</c> ships in-box, keeping the package
    /// dependency-free and Fable-safe on other targets. Fable and .NET Standard callers can adapt JSON-shaped data
    /// through <see cref="M:Axial.Schema.RawInputModule.ofJsonLikeValue" /> instead.
    /// </para>
    /// <para>netstandard2.1: not available.</para>
    /// </remarks>
    let rec ofJsonElement (element: System.Text.Json.JsonElement) : RawInput =
        match element.ValueKind with
        | System.Text.Json.JsonValueKind.Null
        | System.Text.Json.JsonValueKind.Undefined -> RawInput.Missing
        | System.Text.Json.JsonValueKind.String -> scalarOrMissing (element.GetString())
        | System.Text.Json.JsonValueKind.Number -> RawInput.Scalar(element.GetRawText())
        | System.Text.Json.JsonValueKind.True -> RawInput.Scalar "true"
        | System.Text.Json.JsonValueKind.False -> RawInput.Scalar "false"
        | System.Text.Json.JsonValueKind.Array ->
            element.EnumerateArray() |> Seq.map ofJsonElement |> Seq.toList |> RawInput.Many
        | _ ->
            element.EnumerateObject()
            |> Seq.map (fun property -> ensureName property.Name, ofJsonElement property.Value)
            |> Map.ofSeq
            |> RawInput.Object

    /// <summary>Builds raw input from the root element of a <see cref="T:System.Text.Json.JsonDocument" />.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="document" /> is null.</exception>
    let ofJsonDocument (document: System.Text.Json.JsonDocument) : RawInput =
        if isNull document then
            nullArg (nameof document)

        ofJsonElement document.RootElement
#endif

    /// <summary>
    /// Builds raw input from flattened configuration keys using <c>:</c> as the path separator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Numeric path segments are interpreted as collection indexes, matching the common .NET configuration convention
    /// for arrays such as <c>contacts:0:value</c>.
    /// </para>
    /// <para>
    /// Later pairs override earlier ones at the same path, matching .NET configuration layering: a repeated key
    /// keeps its last value, and a later scalar or section replaces an earlier section or scalar at that key.
    /// Collections come from numeric segments, never from repetition — repeated names as multi-value input is a
    /// wire convention that belongs to <c>ofNameValues</c>. A null value never overrides an existing section,
    /// because <c>IConfiguration.AsEnumerable()</c> emits every section key with a null value alongside that
    /// section's children.
    /// </para>
    /// </remarks>
    let ofConfiguration (values: seq<string * string>) : RawInput =
        let values = ensureValues (nameof values) values

        values
        |> Seq.fold
            (fun node (key, value) ->
                let key = ensureName key
                let segments = key.Split([| ':' |], StringSplitOptions.None) |> Array.toList

                if segments |> List.exists ((=) "") then
                    invalidArg (nameof values) $"Configuration key cannot contain an empty segment: {key}"

                insertConfigurationValue segments (scalarOrMissing value) node)
            (Branch Map.empty)
        |> configurationNodeToRawInput

    /// <summary>Builds raw input from configuration key/value pairs, such as .NET <c>IConfiguration.AsEnumerable()</c>.</summary>
    /// <remarks>
    /// A C#-friendly equivalent of <c>ofConfiguration</c>: takes
    /// <see cref="T:System.Collections.Generic.IEnumerable`1" /> of
    /// <see cref="T:System.Collections.Generic.KeyValuePair`2" /> instead of a sequence of F# tuples, matching what
    /// <c>Microsoft.Extensions.Configuration</c>'s <c>IConfiguration.AsEnumerable()</c> returns directly.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="values" /> is null.</exception>
    let ofConfigurationPairs
        (values: System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>)
        : RawInput =
        if isNull values then
            nullArg (nameof values)

        values |> Seq.map (fun pair -> pair.Key, pair.Value) |> ofConfiguration

    /// <summary>Attempts to find a raw input value at a parsed input path.</summary>
    let tryFind (path: InputPath) (input: RawInput) : RawInput option =
        let path = InputPath.ofSegments path

        let rec loop current remaining =
            match remaining, current with
            | [], _ -> Some current
            | InputPathSegment.Name name :: rest, RawInput.Object fields ->
                fields |> Map.tryFind name |> Option.bind (fun field -> loop field rest)
            | InputPathSegment.Index index :: rest, RawInput.Many items ->
                items |> List.tryItem index |> Option.bind (fun item -> loop item rest)
            | _ -> None

        loop input path

    /// <summary>Looks up a raw input value at a parsed input path, returning <c>Missing</c> when the path is absent.</summary>
    let lookup (path: InputPath) (input: RawInput) : RawInput =
        tryFind path input |> Option.defaultValue RawInput.Missing

    /// <summary>Attempts to parse an input path and find the addressed raw input value.</summary>
    let tryFindPath (path: string) (input: RawInput) : RawInput option =
        InputPath.tryParse path |> Option.bind (fun parsedPath -> tryFind parsedPath input)

    /// <summary>Parses an input path and looks up the addressed raw input value.</summary>
    let lookupPath (path: string) (input: RawInput) : RawInput =
        InputPath.parse path |> fun parsedPath -> lookup parsedPath input

    /// <summary>
    /// Attempts to redisplay a scalar raw input value, returning blank text for explicitly missing input.
    /// </summary>
    let tryRedisplay (input: RawInput) : string option =
        tryRedisplayValue input

    /// <summary>
    /// Redisplays a scalar raw input value, returning blank text for missing, object-shaped, or collection-shaped input.
    /// </summary>
    let redisplay (input: RawInput) : string =
        tryRedisplay input |> Option.defaultValue ""

    /// <summary>Attempts to redisplay the scalar raw input value at a parsed input path.</summary>
    let tryRedisplayAt (path: InputPath) (input: RawInput) : string option =
        lookup path input |> tryRedisplayValue

    /// <summary>
    /// Redisplays the scalar raw input value at a parsed input path, returning blank text when the value cannot be
    /// redisplayed as a scalar.
    /// </summary>
    let redisplayAt (path: InputPath) (input: RawInput) : string =
        tryRedisplayAt path input |> Option.defaultValue ""

    /// <summary>Attempts to parse an input path and redisplay the addressed scalar raw input value.</summary>
    let tryRedisplayPath (path: string) (input: RawInput) : string option =
        InputPath.tryParse path |> Option.bind (fun parsedPath -> tryRedisplayAt parsedPath input)

    /// <summary>Parses an input path and redisplays the addressed scalar raw input value.</summary>
    let redisplayPath (path: string) (input: RawInput) : string =
        InputPath.parse path |> fun parsedPath -> redisplayAt parsedPath input
