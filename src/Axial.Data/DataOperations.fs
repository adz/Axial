namespace Axial

open System
open System.Collections.Specialized
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

/// <summary>Helpers for inspecting source-agnostic structured data.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Data =
    /// <summary>Builds an object from an F# map, ordered by key.</summary>
    let objectOfMap (fields: Map<string, Data>) : Data =
        if isNull (box fields) then nullArg (nameof fields)
        fields |> Map.toList |> Data.Object

    /// <summary>Concise, opt-in syntax for constructing structured objects.</summary>
    module Syntax =
        /// <summary>Associates a field name with a supported primitive, structured value, or recursive list.</summary>
        let inline (=>) (name: string) (value: ^value) : string * Data =
            if isNull name then nullArg (nameof name)

            let inline convert (witness: ^w) (value: ^v) =
                ((^w or ^v): (static member From: ^v -> Data) value)

            name, convert Unchecked.defaultof<Data> value

        /// <summary>Builds an object from ordered name/value pairs produced with <c>=&gt;</c>.</summary>
        let data (fields: (string * Data) list) : Data =
            if isNull (box fields) then nullArg (nameof fields)
            Data.Object fields

    type private ConfigurationNode =
        | Value of Data
        | Branch of Map<string, ConfigurationNode>

    let private tryRedisplayValue (input: Data) =
        match input with
        | Data.Null -> Some ""
        | Data.Text value -> Some value
        | Data.Number token -> Some token
        | Data.Bool value -> Some(if value then "true" else "false")
        | Data.List _
        | Data.Object _ -> None

    let private ensureName (name: string) =
        if isNull name then
            nullArg (nameof name)

        if name = "" then
            invalidArg (nameof name) "Structured data field names cannot be empty."

        name

    let private ensureValues name values =
        if isNull (box values) then
            nullArg name

        values

    let private textOrNull (value: string) =
        if isNull value then Data.Null else Data.Text value

    let private fieldValue (values: string list) =
        match values with
        | [] -> Data.Null
        | [ value ] -> textOrNull value
        | values -> values |> List.map textOrNull |> Data.List

    let private objectFromGroupedValues (values: seq<string * string list>) =
        values
        |> Seq.map (fun (name, values) -> ensureName name, fieldValue values)
        |> Seq.toList
        |> Data.Object

    let private addField name value fields =
        let name = ensureName name

        let append existing =
            match existing with
            | None -> Some value
            | Some(Data.List values) -> Some(Data.List(values @ [ value ]))
            | Some existing -> Some(Data.List [ existing; value ])

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
            | [], Branch children when not children.IsEmpty && value = Data.Null -> current
            | [], _ -> Value value
            // A later section path replaces an earlier scalar at the same key: last write wins there too.
            | _ :: _, Value _ -> insert remaining (Branch Map.empty)
            | segment :: rest, Branch children ->
                let segment = ensureName segment
                let child = children |> Map.tryFind segment |> Option.defaultValue (Branch Map.empty)
                Branch(children |> Map.add segment (insert rest child))

        insert segments node

    let private configurationNodeToData node =
        let rec convert node =
            match node with
            | Value value -> value
            | Branch children when children.IsEmpty -> Data.Object []
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
                          |> Option.defaultValue Data.Null ]
                    |> Data.List
                else
                    children
                    |> Map.toList
                    |> List.map (fun (name, child) -> name, convert child)
                    |> Data.Object

        convert node

    /// <summary>Builds object-shaped structured data from a list of named structured data fields.</summary>
    /// <remarks>Field order and repeated names are preserved.</remarks>
    /// <example>
    /// <code>
    /// [ "email", Data.Text "ada@example.com"
    ///   "age", Data.Text "42" ]
    /// |> Data.objectOfList
    /// </code>
    /// </example>
    let objectOfList (fields: (string * Data) list) : Data =
        ensureValues (nameof fields) fields
        |> List.map (fun (name, value) -> ensureName name, value)
        |> Data.Object

    /// <summary>Builds object-shaped structured data from a map of scalar field values.</summary>
    let ofMap (values: Map<string, string>) : Data =
        if isNull (box values) then
            nullArg (nameof values)

        values
        |> Map.toSeq
        |> Seq.map (fun (name, value) -> ensureName name, textOrNull value)
        |> Seq.toList
        |> Data.Object

    /// <summary>Builds object-shaped structured data from a .NET dictionary of scalar field values.</summary>
    /// <remarks>
    /// A C#-friendly equivalent of <c>ofMap</c>: takes <see cref="T:System.Collections.Generic.IDictionary`2" />
    /// instead of an F# <c>Map</c>, so callers do not need to construct an F# map value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="values" /> is null.</exception>
    let ofDictionary (values: System.Collections.Generic.IDictionary<string, string>) : Data =
        if isNull values then
            nullArg (nameof values)

        values
        |> Seq.map (fun pair -> ensureName pair.Key, textOrNull pair.Value)
        |> Seq.toList
        |> Data.Object

    /// <summary>Builds object-shaped structured data from name/value pairs, grouping repeated names into <c>Many</c>.</summary>
    let ofNameValues (values: seq<string * string>) : Data =
        ensureValues (nameof values) values
        |> Seq.groupBy fst
        |> Seq.map (fun (name, grouped) -> name, grouped |> Seq.map snd |> Seq.toList)
        |> objectFromGroupedValues

    /// <summary>Builds object-shaped structured data from a .NET name-value collection.</summary>
    /// <remarks>Fable: not available because <c>NameValueCollection</c> is a .NET input type.</remarks>
    let ofNameValueCollection (values: NameValueCollection) : Data =
        if isNull values then
            nullArg (nameof values)

#if FABLE_COMPILER
        invalidOp "NameValueCollection is a .NET input type and is not available under Fable."
#else
        values.AllKeys
        |> Seq.map (fun name ->
            let name = ensureName name

            let fieldValues =
                match values.GetValues name with
                | null -> []
                | fieldValues -> fieldValues |> Array.toList

            name, fieldValues)
        |> objectFromGroupedValues
#endif

    /// <summary>
    /// Builds structured data from command-line arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supports <c>--name value</c>, <c>--name=value</c>, <c>-n value</c>, boolean flags, <c>--no-name</c>, and repeated
    /// options. Positional arguments are stored under the <c>_</c> field as a collection.
    /// </para>
    /// </remarks>
    let ofCliArgs (args: seq<string>) : Data =
        let args = ensureValues (nameof args) args |> Seq.toList

        let rec loop remaining fields positionals =
            match remaining with
            | [] ->
                let fields =
                    match List.rev positionals with
                    | [] -> fields
                    | positionals -> fields |> addField "_" (positionals |> List.map Data.Text |> Data.List)

                fields |> Map.toList |> Data.Object
            | "--" :: rest ->
                loop [] fields (List.rev rest @ positionals)
            | arg :: rest when isNull arg ->
                nullArg (nameof args)
            | arg :: rest when arg.StartsWith("--no-", StringComparison.Ordinal) && arg.Length > 5 ->
                let name = arg.Substring 5
                loop rest (fields |> addField name (Data.Text "false")) positionals
            | arg :: rest when arg.StartsWith("--", StringComparison.Ordinal) && arg.Length > 2 ->
                let optionText = arg.Substring 2
                let equalsIndex = optionText.IndexOf('=')

                if equalsIndex >= 0 then
                    let name = optionText.Substring(0, equalsIndex)
                    let value = optionText.Substring(equalsIndex + 1)
                    loop rest (fields |> addField name (Data.Text value)) positionals
                else
                    match rest with
                    | value :: tail when not (isNull value) && not (value.StartsWith("-", StringComparison.Ordinal)) ->
                        loop tail (fields |> addField optionText (Data.Text value)) positionals
                    | _ -> loop rest (fields |> addField optionText (Data.Text "true")) positionals
            | arg :: rest when arg.StartsWith("-", StringComparison.Ordinal) && arg.Length > 1 ->
                let optionText = arg.Substring 1
                let equalsIndex = optionText.IndexOf('=')

                if equalsIndex >= 0 then
                    let name = optionText.Substring(0, equalsIndex)
                    let value = optionText.Substring(equalsIndex + 1)
                    loop rest (fields |> addField name (Data.Text value)) positionals
                else
                    match rest with
                    | value :: tail when not (isNull value) && not (value.StartsWith("-", StringComparison.Ordinal)) ->
                        loop tail (fields |> addField optionText (Data.Text value)) positionals
                    | _ -> loop rest (fields |> addField optionText (Data.Text "true")) positionals
            | arg :: rest -> loop rest fields (arg :: positionals)

        loop args Map.empty []

#if NET8_0_OR_GREATER && !FABLE_COMPILER
    /// <summary>Builds structured data from a <see cref="T:System.Text.Json.JsonElement" />.</summary>
    /// <remarks>
    /// <para>
    /// This is the boundary adapter for JSON bodies parsed with <c>System.Text.Json</c>, such as ASP.NET Core request
    /// payloads: convert the element once, then parse it with <c>Schema.parse</c> to get path-aware diagnostics or a
    /// trusted model. JSON value kinds remain distinct, and number tokens are carried without narrowing them to one
    /// CLR numeric type. Other JSON syntax, such as whitespace and source locations, is not represented.
    /// </para>
    /// <para>
    /// The adapter is available on .NET 8+ targets where <c>System.Text.Json</c> ships in-box, keeping the package
    /// dependency-free and Fable-safe on other targets.
    /// </para>
    /// <para>netstandard2.1: not available.</para>
    /// </remarks>
    let rec ofJsonElement (element: System.Text.Json.JsonElement) : Data =
        match element.ValueKind with
        | System.Text.Json.JsonValueKind.Null
        | System.Text.Json.JsonValueKind.Undefined -> Data.Null
        | System.Text.Json.JsonValueKind.String -> textOrNull (element.GetString())
        | System.Text.Json.JsonValueKind.Number -> Data.Number(element.GetRawText())
        | System.Text.Json.JsonValueKind.True -> Data.Bool true
        | System.Text.Json.JsonValueKind.False -> Data.Bool false
        | System.Text.Json.JsonValueKind.Array ->
            element.EnumerateArray() |> Seq.map ofJsonElement |> Seq.toList |> Data.List
        | _ ->
            element.EnumerateObject()
            |> Seq.map (fun property -> ensureName property.Name, ofJsonElement property.Value)
            |> Seq.toList
            |> Data.Object

    /// <summary>Builds structured data from the root element of a <see cref="T:System.Text.Json.JsonDocument" />.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="document" /> is null.</exception>
    let ofJsonDocument (document: System.Text.Json.JsonDocument) : Data =
        if isNull document then
            nullArg (nameof document)

        ofJsonElement document.RootElement
#endif

    /// <summary>
    /// Builds structured data from flattened configuration keys using <c>:</c> as the path separator.
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
    let ofConfiguration (values: seq<string * string>) : Data =
        let values = ensureValues (nameof values) values

        values
        |> Seq.fold
            (fun node (key, value) ->
                let key = ensureName key
                let segments = key.Split([| ':' |], StringSplitOptions.None) |> Array.toList

                if segments |> List.exists ((=) "") then
                    invalidArg (nameof values) $"Configuration key cannot contain an empty segment: {key}"

                insertConfigurationValue segments (textOrNull value) node)
            (Branch Map.empty)
        |> configurationNodeToData

    /// <summary>Builds structured data from configuration key/value pairs, such as .NET <c>IConfiguration.AsEnumerable()</c>.</summary>
    /// <remarks>
    /// A C#-friendly equivalent of <c>ofConfiguration</c>: takes
    /// <see cref="T:System.Collections.Generic.IEnumerable`1" /> of
    /// <see cref="T:System.Collections.Generic.KeyValuePair`2" /> instead of a sequence of F# tuples, matching what
    /// <c>Microsoft.Extensions.Configuration</c>'s <c>IConfiguration.AsEnumerable()</c> returns directly.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="values" /> is null.</exception>
    let ofConfigurationPairs
        (values: System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>)
        : Data =
        if isNull values then
            nullArg (nameof values)

        values |> Seq.map (fun pair -> pair.Key, pair.Value) |> ofConfiguration

    /// <summary>Attempts to find a structured data value at a parsed input path.</summary>
    let tryFind (path: DataPath) (input: Data) : Data option =
        let path = DataPath.ofSegments path

        let rec loop current remaining =
            match remaining, current with
            | [], _ -> Some current
            | DataPathSegment.Name name :: rest, Data.Object fields ->
                fields
                |> List.tryFindBack (fun (fieldName, _) -> fieldName = name)
                |> Option.map snd
                |> Option.bind (fun field -> loop field rest)
            | DataPathSegment.Index index :: rest, Data.List items ->
                items |> List.tryItem index |> Option.bind (fun item -> loop item rest)
            | _ -> None

        loop input path

    /// <summary>Looks up a structured data value at a parsed input path, returning <c>Null</c> when the path is absent.</summary>
    let lookup (path: DataPath) (input: Data) : Data =
        tryFind path input |> Option.defaultValue Data.Null

    /// <summary>Attempts to parse an input path and find the addressed structured data value.</summary>
    let tryFindPath (path: string) (input: Data) : Data option =
        DataPath.tryParse path |> Option.bind (fun parsedPath -> tryFind parsedPath input)

    /// <summary>Parses an input path and looks up the addressed structured data value.</summary>
    let lookupPath (path: string) (input: Data) : Data =
        DataPath.parse path |> fun parsedPath -> lookup parsedPath input

    /// <summary>
    /// Attempts to redisplay a scalar structured data value, returning blank text for explicitly missing input.
    /// </summary>
    let tryRedisplay (input: Data) : string option =
        tryRedisplayValue input

    /// <summary>
    /// Redisplays a scalar structured data value, returning blank text for missing, object-shaped, or collection-shaped input.
    /// </summary>
    let redisplay (input: Data) : string =
        tryRedisplay input |> Option.defaultValue ""

    /// <summary>Attempts to redisplay the scalar structured data value at a parsed input path.</summary>
    let tryRedisplayAt (path: DataPath) (input: Data) : string option =
        lookup path input |> tryRedisplayValue

    /// <summary>
    /// Redisplays the scalar structured data value at a parsed input path, returning blank text when the value cannot be
    /// redisplayed as a scalar.
    /// </summary>
    let redisplayAt (path: DataPath) (input: Data) : string =
        tryRedisplayAt path input |> Option.defaultValue ""

    /// <summary>Attempts to parse an input path and redisplay the addressed scalar structured data value.</summary>
    let tryRedisplayPath (path: string) (input: Data) : string option =
        DataPath.tryParse path |> Option.bind (fun parsedPath -> tryRedisplayAt parsedPath input)

    /// <summary>Parses an input path and redisplays the addressed scalar structured data value.</summary>
    let redisplayPath (path: string) (input: Data) : string =
        DataPath.parse path |> fun parsedPath -> redisplayAt parsedPath input
