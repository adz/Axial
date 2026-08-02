namespace Axial

open System
open System.Globalization
open System.Text

type PatternConversion =
    static member ToPattern(value: Data) = DataPattern(Exact value)
    static member ToPattern(value: DataPattern) = value
    static member ToPattern(value: string) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: bool) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: int) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: int64) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: decimal) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: float) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: Guid) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: DateTimeOffset) = DataPattern(Exact(Data.From value))
#if NET8_0_OR_GREATER
    static member ToPattern(value: DateOnly) = DataPattern(Exact(Data.From value))
#endif

    static member private ExactList(values: Data list) =
        if isNull (box values) then DataPattern.CreateExact Data.Null
        else DataPattern.CreateExact(Data.List values)

    static member private ExactListOf(values: 'value list, convert: 'value -> Data) =
        if isNull (box values) then DataPattern.CreateExact Data.Null
        else values |> List.map convert |> PatternConversion.ExactList

    static member ToPattern(values: Data list) = PatternConversion.ExactList values
    static member ToPattern(values: string list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: bool list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: int list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: int64 list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: decimal list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: float list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: Guid list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: DateTimeOffset list) = PatternConversion.ExactListOf(values, Data.From)
#if NET8_0_OR_GREATER
    static member ToPattern(values: DateOnly list) = PatternConversion.ExactListOf(values, Data.From)
#endif

    static member ToPattern(fields: DataField list) =
        let values =
            fields
            |> List.choose (fun field ->
                if field.Omitted then
                    None
                else
                    match field.Pattern.Node with
                    | Exact value -> Some(field.Name, value)
                    | _ ->
                        invalidArg
                            (nameof fields)
                            "A data literal can contain only exact values. Use containing for partial object patterns.")

        DataPattern(Exact(Data.Object values))

    static member inline ToPattern(values: ^value list) =
        if isNull (box values) then
            DataPattern.CreateExact Data.Null
        else
            let inline convert (witness: ^w) (value: ^v) =
                ((^w or ^v): (static member ToPattern: ^v -> DataPattern) value)

            let items =
                values
                |> List.map (convert Unchecked.defaultof<PatternConversion>)
                |> List.map (fun pattern -> pattern.RequireExact(nameof values))

            DataPattern.CreateExact(Data.List items)

[<AutoOpen>]
module DataErgonomicsHelpers =
    let inline toPatternWith (witness: ^witness) (value: ^value) : DataPattern =
        ((^witness or ^value): (static member ToPattern: ^value -> DataPattern) value)

    let inline toPattern (value: ^value) : DataPattern =
        toPatternWith Unchecked.defaultof<PatternConversion> value

    let exactValue argumentName (pattern: DataPattern) = pattern.RequireExact argumentName

    let ensureText argumentName (value: string) =
        if isNull value then nullArg argumentName
        value

    let ensureNonEmptyText argumentName (value: string) =
        ensureText argumentName value |> ignore
        if value = "" then invalidArg argumentName "The value cannot be empty."
        value

    let isJsonNumberToken (token: string) =
        let length = token.Length
        let isDigitAt index = index < length && token[index] >= '0' && token[index] <= '9'
        let rec consumeDigits index = if isDigitAt index then consumeDigits (index + 1) else index

        let afterSign = if length > 0 && token[0] = '-' then 1 else 0

        let afterInteger =
            if afterSign < length && token[afterSign] = '0' then
                Some(afterSign + 1)
            elif afterSign < length && token[afterSign] >= '1' && token[afterSign] <= '9' then
                Some(consumeDigits (afterSign + 1))
            else
                None

        match afterInteger with
        | None -> false
        | Some integerEnd ->
            let afterFraction =
                if integerEnd < length && token[integerEnd] = '.' then
                    let fractionStart = integerEnd + 1
                    if isDigitAt fractionStart then Some(consumeDigits fractionStart) else None
                else
                    Some integerEnd

            match afterFraction with
            | None -> false
            | Some fractionEnd when fractionEnd < length && (token[fractionEnd] = 'e' || token[fractionEnd] = 'E') ->
                let exponentStart = fractionEnd + 1
                let digitsStart =
                    if exponentStart < length && (token[exponentStart] = '+' || token[exponentStart] = '-') then
                        exponentStart + 1
                    else
                        exponentStart

                isDigitAt digitsStart && consumeDigits digitsStart = length
            | Some fractionEnd -> fractionEnd = length

    let appendPath segment path = path @ [ segment ]

    let shapeName value =
        match value with
        | Data.Null -> "null"
        | Data.Text _ -> "text"
        | Data.Number _ -> "number"
        | Data.Bool _ -> "Boolean"
        | Data.List _ -> "list"
        | Data.Object _ -> "object"

    let tryResolveDetailed (path: DataPath) (input: Data) =
        let rec loop traversed remaining current =
            match remaining with
            | [] -> Ok current
            | DataPathSegment.Name name :: rest ->
                match current with
                | Data.Object fields ->
                    match fields |> List.tryFindIndexBack (fun (fieldName, _) -> fieldName = name) with
                    | Some index -> loop (appendPath (DataPathSegment.Name name) traversed) rest (snd fields[index])
                    | None -> Error(traversed, $"Object field '{name}' does not exist.")
                | actual -> Error(traversed, $"Expected an object but found {shapeName actual}.")
            | DataPathSegment.Index index :: rest ->
                match current with
                | Data.List items when index < items.Length ->
                    loop (appendPath (DataPathSegment.Index index) traversed) rest items[index]
                | Data.List items -> Error(traversed, $"List index {index} is outside the list of {items.Length} items.")
                | actual -> Error(traversed, $"Expected a list but found {shapeName actual}.")

        loop [] path input

    let renderText (value: string) =
        let builder = StringBuilder()

        value
        |> Seq.iter (function
            | '"' -> builder.Append("\\\"") |> ignore
            | '\\' -> builder.Append("\\\\") |> ignore
            | '\b' -> builder.Append("\\b") |> ignore
            | '\f' -> builder.Append("\\f") |> ignore
            | '\n' -> builder.Append("\\n") |> ignore
            | '\r' -> builder.Append("\\r") |> ignore
            | '\t' -> builder.Append("\\t") |> ignore
            | character when int character < 0x20 ->
                builder.Append("\\u").Append((int character).ToString("x4", CultureInfo.InvariantCulture)) |> ignore
            | character -> builder.Append(character) |> ignore)

        $"\"{builder}\""

    let renderName (name: string) =
        let isPlainStart character = Char.IsLetter character || character = '_'
        let isPlain character = Char.IsLetterOrDigit character || character = '_' || character = '-'

        if name.Length > 0 && isPlainStart name[0] && (name |> Seq.skip 1 |> Seq.forall isPlain) then
            name
        else
            renderText name

    let jsonRenderCompact input =

        let rec render value =
            match value with
            | Data.Null -> "null"
            | Data.Text text -> renderText text
            | Data.Number token -> token
            | Data.Bool true -> "true"
            | Data.Bool false -> "false"
            | Data.List items -> items |> List.map render |> String.concat "," |> fun body -> $"[{body}]"
            | Data.Object fields ->
                fields
                |> List.map (fun (name, field) -> $"{renderText name}:{render field}")
                |> String.concat ","
                |> fun body -> $"{{{body}}}"

        render input

    let jsonRenderIndented input =
        let compactScalar value =
            match value with
            | Data.List _
            | Data.Object _ -> None
            | scalar -> Some(jsonRenderCompact scalar)

        let rec render level value =
            let indent count = String(' ', count * 2)

            match compactScalar value with
            | Some scalar -> scalar
            | None ->
                match value with
                | Data.List [] -> "[]"
                | Data.List items ->
                    items
                    |> List.map (fun item -> $"{indent (level + 1)}{render (level + 1) item}")
                    |> String.concat ",\n"
                    |> fun body -> $"[\n{body}\n{indent level}]"
                | Data.Object [] -> "{}"
                | Data.Object fields ->
                    fields
                    |> List.map (fun (name, field) ->
                        let encodedName = renderText name
                        $"{indent (level + 1)}{encodedName}: {render (level + 1) field}")
                    |> String.concat ",\n"
                    |> fun body -> $"{{\n{body}\n{indent level}}}"
                | _ -> failwith "Unreachable scalar rendering branch."

        render 0 input

    let renderCompact input =
        let rec render value =
            match value with
            | Data.Null -> "null"
            | Data.Text text -> renderText text
            | Data.Number token -> token
            | Data.Bool true -> "true"
            | Data.Bool false -> "false"
            | Data.List items -> items |> List.map render |> String.concat ", " |> fun body -> $"[{body}]"
            | Data.Object fields ->
                match fields with
                | [] -> "{}"
                | _ ->
                    fields
                    |> List.map (fun (name, field) -> $"{renderName name}: {render field}")
                    |> String.concat ", "
                    |> fun body -> $"{{ {body} }}"

        render input

    let renderIndented input =
        let compactScalar value =
            match value with
            | Data.List _
            | Data.Object _ -> None
            | scalar -> Some(renderCompact scalar)

        let rec render level value =
            let indent count = String(' ', count * 2)

            match compactScalar value with
            | Some scalar -> scalar
            | None ->
                match value with
                | Data.List [] -> "[]"
                | Data.List items ->
                    items
                    |> List.map (fun item -> $"{indent (level + 1)}{render (level + 1) item}")
                    |> String.concat ",\n"
                    |> fun body -> $"[\n{body}\n{indent level}]"
                | Data.Object [] -> "{}"
                | Data.Object fields ->
                    fields
                    |> List.map (fun (name, field) ->
                        $"{indent (level + 1)}{renderName name}: {render (level + 1) field}")
                    |> String.concat ",\n"
                    |> fun body -> $"{{\n{body}\n{indent level}}}"
                | _ -> failwith "Unreachable scalar rendering branch."

        render 0 input

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Data =
    /// <summary>Associates an object field name with one exact value.</summary>
    /// <example><code>Data.assoc "name" "Ada" // one DataField</code></example>
    let inline assoc (name: string) value : DataField =
        ensureNonEmptyText (nameof name) name |> ignore
        DataField.Create(name, toPattern value, false)

    /// <summary>Associates an object field name with <c>Some</c> value, or omits <c>None</c>.</summary>
    /// <example><code>Data.optionalAssoc "nickname" None // an omitted DataField</code></example>
    let inline optionalAssoc (name: string) (value: ^value option) : DataField =
        ensureNonEmptyText (nameof name) name |> ignore
        match value with
        | Some supplied -> DataField.Create(name, toPattern supplied, false)
        | None -> DataField.Create(name, DataPattern.CreateExact Data.Null, true)

    /// <summary>Builds an object from ordered field instructions.</summary>
    /// <example><code>Data.data [ Data.assoc "name" "Ada" ]
    /// // Data.Object [ "name", Data.Text "Ada" ]</code></example>
    let data (fields: DataField list) : Data =
        if isNull (box fields) then nullArg (nameof fields)
        PatternConversion.ToPattern fields |> exactValue (nameof fields)

    /// <summary>Returns exact field instructions from an existing object.</summary>
    /// <example><code>Data.fields (Data.data [ Data.assoc "name" "Ada" ])
    /// // one field instruction for name</code></example>
    let fields value =
        match value with
        | Data.Object values -> values |> List.map (fun (name, item) -> DataField.Create(name, DataPattern.CreateExact item, false))
        | actual -> invalidArg (nameof value) $"Expected an object but found {shapeName actual}."

    /// <summary>Constructs a number from one JSON number token, validated identically on .NET and Fable.</summary>
    /// <example><code>Data.number "1.2300e+4" // Data.Number "1.2300e+4"</code></example>
    let number (token: string) =
        ensureText (nameof token) token |> ignore
        if not (isJsonNumberToken token) then
            invalidArg (nameof token) "The token is not a valid JSON number."
        Data.Number token

    /// <summary>Builds an object from an F# map, ordered by key.</summary>
    let objectOfMap = DataCore.objectOfMap

    /// <summary>Builds an object from ordered name and value pairs.</summary>
    let objectOfList = DataCore.objectOfList

    /// <summary>Builds object-shaped data from a map of scalar values.</summary>
    let ofMap = DataCore.ofMap

    /// <summary>Builds object-shaped data from a .NET dictionary of scalar values.</summary>
    let ofDictionary = DataCore.ofDictionary

    /// <summary>Builds object-shaped data from name and value pairs.</summary>
    let ofNameValues = DataCore.ofNameValues

    /// <summary>Builds object-shaped data from a .NET name-value collection.</summary>
    let ofNameValueCollection = DataCore.ofNameValueCollection

    /// <summary>Builds structured data from command-line arguments.</summary>
    let ofCliArgs = DataCore.ofCliArgs

#if NET8_0_OR_GREATER && !FABLE_COMPILER
    /// <summary>Copies a .NET 8+ <c>System.Text.Json.JsonElement</c> into structured data.</summary>
    /// <remarks>This platform-specific convenience conversion is not available under Fable.</remarks>
    let ofJsonElement = DataCore.ofJsonElement

    /// <summary>Copies a .NET 8+ <c>System.Text.Json.JsonDocument</c> into structured data.</summary>
    /// <remarks>This platform-specific convenience conversion is not available under Fable.</remarks>
    let ofJsonDocument = DataCore.ofJsonDocument
#endif

#if FABLE_COMPILER
    /// <summary>Copies a value returned by JavaScript <c>JSON.parse</c> into structured data.</summary>
    /// <remarks>
    /// This Fable-specific convenience conversion is not available on .NET. JavaScript parsing has already discarded
    /// duplicate object fields and the original spelling of number tokens.
    /// </remarks>
    let ofJsonValue = DataCore.ofJsonValue
#endif

    /// <summary>Builds structured data from flattened configuration keys.</summary>
    let ofConfiguration = DataCore.ofConfiguration

    /// <summary>Builds structured data from .NET configuration key and value pairs.</summary>
    let ofConfigurationPairs = DataCore.ofConfigurationPairs

    /// <summary>Attempts to find a value at a parsed path.</summary>
    let tryFind = DataCore.tryFind

    /// <summary>Finds a value at a parsed path or returns <c>Null</c>.</summary>
    let lookup = DataCore.lookup

    /// <summary>Attempts to parse a path and find its value.</summary>
    let tryFindPath = DataCore.tryFindPath

    /// <summary>Parses a path and finds its value or returns <c>Null</c>.</summary>
    let lookupPath = DataCore.lookupPath

    /// <summary>Attempts to render one scalar value for redisplay.</summary>
    let tryRedisplay = DataCore.tryRedisplay

    /// <summary>Renders one scalar value for redisplay.</summary>
    let redisplay = DataCore.redisplay

    /// <summary>Attempts to redisplay a scalar at a parsed path.</summary>
    let tryRedisplayAt = DataCore.tryRedisplayAt

    /// <summary>Redisplays a scalar at a parsed path.</summary>
    let redisplayAt = DataCore.redisplayAt

    /// <summary>Attempts to parse a path and redisplay its scalar.</summary>
    let tryRedisplayPath = DataCore.tryRedisplayPath

    /// <summary>Parses a path and redisplays its scalar.</summary>
    let redisplayPath = DataCore.redisplayPath

    let private replaceAt index replacement values =
        values |> List.mapi (fun position value -> if position = index then replacement else value)

    let private removeAt index values =
        values |> List.mapi (fun position value -> position, value) |> List.choose (fun (position, value) -> if position = index then None else Some value)

    let private updateAtPath path change input =
        let rec loop remaining current =
            match remaining with
            | [] -> change current
            | DataPathSegment.Name name :: rest ->
                match current with
                | Data.Object fields ->
                    match fields |> List.tryFindIndexBack (fun (fieldName, _) -> fieldName = name) with
                    | None -> Error $"Object field '{name}' does not exist."
                    | Some index ->
                        loop rest (snd fields[index])
                        |> Result.map (fun updated -> Data.Object(replaceAt index (name, updated) fields))
                | actual -> Error $"Expected an object but found {shapeName actual}."
            | DataPathSegment.Index index :: rest ->
                match current with
                | Data.List items when index < items.Length ->
                    loop rest items[index] |> Result.map (fun updated -> Data.List(replaceAt index updated items))
                | Data.List items -> Error $"List index {index} is outside the list of {items.Length} items."
                | actual -> Error $"Expected a list but found {shapeName actual}."

        loop path input

    let private updateParent path change input =
        match List.rev path with
        | [] -> Error "The root has no parent."
        | final :: reversedParent -> updateAtPath (List.rev reversedParent) (change final) input

    let private tryApplyEdit (edit: DataEdit) input =
        match edit.Node with
        | Replace(path, value) -> updateAtPath path (fun _ -> Ok value) input
        | Set(path, value) ->
            match List.rev path with
            | [] -> Ok value
            | DataPathSegment.Name name :: reversedParent ->
                updateAtPath (List.rev reversedParent) (fun parent ->
                    match parent with
                    | Data.Object fields ->
                        match fields |> List.tryFindIndexBack (fun (fieldName, _) -> fieldName = name) with
                        | Some index -> Ok(Data.Object(replaceAt index (name, value) fields))
                        | None -> Ok(Data.Object(fields @ [ name, value ]))
                    | actual -> Error $"Expected an object parent but found {shapeName actual}.") input
            | DataPathSegment.Index index :: reversedParent ->
                updateAtPath (List.rev reversedParent) (fun parent ->
                    match parent with
                    | Data.List items when index < items.Length -> Ok(Data.List(replaceAt index value items))
                    | Data.List items -> Error $"List index {index} is outside the list of {items.Length} items."
                    | actual -> Error $"Expected a list parent but found {shapeName actual}.") input
        | Remove path ->
            updateParent path (fun final parent ->
                match final, parent with
                | DataPathSegment.Name name, Data.Object fields ->
                    match fields |> List.tryFindIndexBack (fun (fieldName, _) -> fieldName = name) with
                    | Some index -> Ok(Data.Object(removeAt index fields))
                    | None -> Error $"Object field '{name}' does not exist."
                | DataPathSegment.Index index, Data.List items when index < items.Length -> Ok(Data.List(removeAt index items))
                | DataPathSegment.Index index, Data.List items -> Error $"List index {index} is outside the list of {items.Length} items."
                | DataPathSegment.Name _, actual -> Error $"Expected an object parent but found {shapeName actual}."
                | DataPathSegment.Index _, actual -> Error $"Expected a list parent but found {shapeName actual}.") input
        | Append(path, value) ->
            updateAtPath path (function
                | Data.List items -> Ok(Data.List(items @ [ value ]))
                | actual -> Error $"Expected a list but found {shapeName actual}.") input
        | Prepend(path, value) ->
            updateAtPath path (function
                | Data.List items -> Ok(Data.List(value :: items))
                | actual -> Error $"Expected a list but found {shapeName actual}.") input
        | Insert(path, index, value) ->
            updateAtPath path (function
                | Data.List items when index <= items.Length ->
                    let before, after = List.splitAt index items
                    Ok(Data.List(before @ [ value ] @ after))
                | Data.List items -> Error $"List index {index} is outside the insertion range of {items.Length} items."
                | actual -> Error $"Expected a list but found {shapeName actual}.") input
        | Rename(path, newName) ->
            updateParent path (fun final parent ->
                match final, parent with
                | DataPathSegment.Name name, Data.Object fields ->
                    match fields |> List.tryFindIndexBack (fun (fieldName, _) -> fieldName = name) with
                    | Some index -> Ok(Data.Object(replaceAt index (newName, snd fields[index]) fields))
                    | None -> Error $"Object field '{name}' does not exist."
                | DataPathSegment.Index _, _ -> Error "Only object fields can be renamed."
                | DataPathSegment.Name _, actual -> Error $"Expected an object parent but found {shapeName actual}.") input
        | Update(path, change) -> updateAtPath path (change >> Ok) input

    /// <summary>Applies immutable edits atomically in declaration order.</summary>
    /// <example><code>Data.tryPatch [ replace "name" "Grace" ] (data [ "name" =&gt; "Ada" ])
    /// // Ok (data [ "name" =&gt; "Grace" ])</code></example>
    let tryPatch (edits: DataEdit list) (input: Data) : Result<Data, DataPatchFailure list> =
        if isNull (box edits) then nullArg (nameof edits)

        let rec loop index current (remaining: DataEdit list) : Result<Data, DataPatchFailure list> =
            match remaining with
            | [] -> Ok current
            | edit :: rest ->
                match tryApplyEdit edit current with
                | Ok updated -> loop (index + 1) updated rest
                | Error message -> Error [ { EditIndex = index; Path = edit.Path; Message = message } ]

        loop 0 input edits

    /// <summary>Applies edits atomically or raises <c>DataPatchException</c>.</summary>
    /// <example><code>Data.data [ Data.assoc "name" "Ada" ]
    /// |&gt; Data.patch [ DataEdit.replace "name" "Grace" ]
    /// // Data.data [ Data.assoc "name" "Grace" ]</code></example>
    let patch edits input =
        match tryPatch edits input with
        | Ok value -> value
        | Error failures -> raise (DataPatchException failures)

    /// <summary>Applies one prepared edit or raises <c>DataPatchException</c>.</summary>
    /// <example><code>data [ "name" =&gt; "Ada" ] |&gt; Data.applyEdit (replace "name" "Grace")
    /// // data [ "name" =&gt; "Grace" ]</code></example>
    let applyEdit edit input =
        match tryPatch [ edit ] input with
        | Ok value -> value
        | Error failures -> raise (DataPatchException failures)

    /// <summary>Replaces one value, or adds a missing final object field, and returns the changed tree.</summary>
    /// <example><code>data [ "name" =&gt; "Ada" ] |&gt; Data.set "active" true
    /// // data [ "name" =&gt; "Ada"; "active" =&gt; true ]</code></example>
    let inline set path value input =
        DataEdit.set path value
        |> fun edit -> applyEdit edit input

    /// <summary>Replaces one existing value and returns the changed tree.</summary>
    /// <example><code>data [ "name" =&gt; "Ada" ] |&gt; Data.replace "name" "Grace"
    /// // data [ "name" =&gt; "Grace" ]</code></example>
    let inline replace path value input =
        DataEdit.replace path value
        |> fun edit -> applyEdit edit input

    /// <summary>Removes one existing field or list item and returns the changed tree.</summary>
    /// <example><code>data [ "name" =&gt; "Ada"; "active" =&gt; true ] |&gt; Data.remove "active"
    /// // data [ "name" =&gt; "Ada" ]</code></example>
    let remove path input = applyEdit (DataEdit.remove path) input

    /// <summary>Appends one item to an existing list and returns the changed tree.</summary>
    /// <example><code>data [ "roles" =&gt; [ "author" ] ] |&gt; Data.append "roles" "admin"
    /// // data [ "roles" =&gt; [ "author"; "admin" ] ]</code></example>
    let inline append path value input =
        DataEdit.append path value
        |> fun edit -> applyEdit edit input

    /// <summary>Prepends one item to an existing list and returns the changed tree.</summary>
    /// <example><code>data [ "roles" =&gt; [ "admin" ] ] |&gt; Data.prepend "roles" "author"
    /// // data [ "roles" =&gt; [ "author"; "admin" ] ]</code></example>
    let inline prepend path value input =
        DataEdit.prepend path value
        |> fun edit -> applyEdit edit input

    /// <summary>Inserts one item at a valid list index and returns the changed tree.</summary>
    /// <example><code>data [ "roles" =&gt; [ "author" ] ] |&gt; Data.insert "roles" 1 "admin"
    /// // data [ "roles" =&gt; [ "author"; "admin" ] ]</code></example>
    let inline insert path index value input =
        DataEdit.insert path index value
        |> fun edit -> applyEdit edit input

    /// <summary>Renames one existing object field without moving it and returns the changed tree.</summary>
    /// <example><code>data [ "name" =&gt; "Ada" ] |&gt; Data.rename "name" "displayName"
    /// // data [ "displayName" =&gt; "Ada" ]</code></example>
    let rename path name input =
        applyEdit (DataEdit.rename path name) input

    /// <summary>Applies one function to an existing value and returns the changed tree.</summary>
    /// <example><code>data [ "active" =&gt; true ] |&gt; Data.update "active" (fun _ -&gt; Data.Bool false)
    /// // data [ "active" =&gt; false ]</code></example>
    let update path change input =
        applyEdit (DataEdit.update path change) input

    /// <summary>Returns all exact structural differences between two values.</summary>
    /// <example><code>Data.diff (data [ "name" =&gt; "Ada" ]) (data [ "name" =&gt; "Grace" ])
    /// // one DifferentValue difference at path "name"</code></example>
    let diff (expected: Data) (actual: Data) : DataDifference list =
        let difference path expected actual cause =
            { Path = path; Expected = expected; Actual = actual; Cause = cause }

        let rec compare path expected actual =
            match expected, actual with
            | _ when expected = actual -> []
            | Data.List expectedItems, Data.List actualItems ->
                let common = min expectedItems.Length actualItems.Length
                let shared = [ 0 .. common - 1 ] |> List.collect (fun index -> compare (appendPath (DataPathSegment.Index index) path) expectedItems[index] actualItems[index])
                let missing = [ common .. expectedItems.Length - 1 ] |> List.map (fun index -> difference (appendPath (DataPathSegment.Index index) path) (Some expectedItems[index]) None DataDifferenceCause.Missing)
                let unexpected = [ common .. actualItems.Length - 1 ] |> List.map (fun index -> difference (appendPath (DataPathSegment.Index index) path) None (Some actualItems[index]) DataDifferenceCause.Unexpected)
                shared @ missing @ unexpected
            | Data.Object expectedFields, Data.Object actualFields ->
                let common = min expectedFields.Length actualFields.Length
                let shared =
                    [ 0 .. common - 1 ]
                    |> List.collect (fun index ->
                        let expectedName, expectedValue = expectedFields[index]
                        let actualName, actualValue = actualFields[index]
                        let fieldPath = appendPath (DataPathSegment.Name expectedName) path
                        if expectedName <> actualName then
                            [ difference fieldPath (Some expectedValue) (Some actualValue) DataDifferenceCause.DifferentFieldName ]
                        else compare fieldPath expectedValue actualValue)
                let missing = [ common .. expectedFields.Length - 1 ] |> List.map (fun index -> let name, value = expectedFields[index] in difference (appendPath (DataPathSegment.Name name) path) (Some value) None DataDifferenceCause.Missing)
                let unexpected = [ common .. actualFields.Length - 1 ] |> List.map (fun index -> let name, value = actualFields[index] in difference (appendPath (DataPathSegment.Name name) path) None (Some value) DataDifferenceCause.Unexpected)
                shared @ missing @ unexpected
            | Data.List _, _
            | Data.Object _, _
            | _, Data.List _
            | _, Data.Object _ -> [ difference path (Some expected) (Some actual) DataDifferenceCause.DifferentShape ]
            | _ -> [ difference path (Some expected) (Some actual) DataDifferenceCause.DifferentValue ]

        compare DataPath.empty expected actual

    /// <summary>Compares complete values and returns every structural difference.</summary>
    /// <example><code>Data.compare (data [ "name" =&gt; "Ada" ]) (data [ "name" =&gt; "Ada" ])
    /// // Ok ()</code></example>
    let compare expected actual =
        match diff expected actual with
        | [] -> Ok()
        | differences -> Error differences

    let rec private matchPattern path (pattern: DataPattern) actual =
        let mismatch expected actual = [ path, expected, actual ]

        match pattern.Node with
        | Exact expected ->
            match compare expected actual with
            | Ok() -> []
            | Error differences ->
                differences
                |> List.map (fun difference ->
                    path @ difference.Path,
                    (difference.Expected |> Option.map renderCompact |> Option.defaultValue "an absent value"),
                    difference.Actual)
        | Any -> []
        | AnyText -> match actual with Data.Text _ -> [] | _ -> mismatch "text" (Some actual)
        | AnyNumber -> match actual with Data.Number _ -> [] | _ -> mismatch "number" (Some actual)
        | ObjectContaining fields ->
            match actual with
            | Data.Object actualFields ->
                let rec assign (remainingActual: (string * Data) list) (remainingExpected: DataField list) =
                    match remainingExpected with
                    | [] -> []
                    | field :: rest ->
                        let fieldPath = appendPath (DataPathSegment.Name field.Name) path

                        let candidates =
                            remainingActual
                            |> List.mapi (fun index (name, value) -> index, name, value)
                            |> List.filter (fun (_, name, value) ->
                                name = field.Name && (matchPattern fieldPath field.Pattern value |> List.isEmpty))

                        candidates
                        |> List.tryPick (fun (index, _, _) ->
                            let later = assign (removeAt index remainingActual) rest
                            if List.isEmpty later then Some [] else None)
                        |> Option.defaultWith (fun () ->
                            match remainingActual |> List.tryFindBack (fun (name, _) -> name = field.Name) with
                            | Some(_, value) -> matchPattern fieldPath field.Pattern value
                            | None -> [ fieldPath, "a present matching field", None ])

                fields |> List.filter (fun field -> not field.Omitted) |> assign actualFields
            | _ -> mismatch "object" (Some actual)
        | ListInOrder patterns ->
            match actual with
            | Data.List items ->
                let rec consume patternIndex itemIndex remainingPatterns =
                    match remainingPatterns with
                    | [] -> []
                    | expected :: rest when itemIndex >= items.Length ->
                        [ appendPath (DataPathSegment.Index patternIndex) path, "an item in order", None ]
                    | expected :: rest ->
                        if matchPattern (appendPath (DataPathSegment.Index itemIndex) path) expected items[itemIndex] |> List.isEmpty then
                            consume (patternIndex + 1) (itemIndex + 1) rest
                        else consume patternIndex (itemIndex + 1) remainingPatterns
                consume 0 0 patterns
            | _ -> mismatch "list" (Some actual)
        | ListContaining patterns ->
            match actual with
            | Data.List items ->
                let rec assign remainingItems patternIndex remainingPatterns =
                    match remainingPatterns with
                    | [] -> []
                    | expected :: rest ->
                        let candidates =
                            remainingItems
                            |> List.mapi (fun index item -> index, item)
                            |> List.filter (fun (_, item) -> matchPattern path expected item |> List.isEmpty)

                        candidates
                        |> List.tryPick (fun (index, _) ->
                            let later = assign (removeAt index remainingItems) (patternIndex + 1) rest
                            if List.isEmpty later then Some [] else None)
                        |> Option.defaultValue [ appendPath (DataPathSegment.Index patternIndex) path, "a matching list item", None ]
                assign items 0 patterns
            | _ -> mismatch "list" (Some actual)
        | EveryItem pattern ->
            match actual with
            | Data.List items -> items |> List.mapi (fun index item -> matchPattern (appendPath (DataPathSegment.Index index) path) pattern item) |> List.concat
            | _ -> mismatch "list" (Some actual)
        | SomeItem pattern ->
            match actual with
            | Data.List items when items |> List.exists (fun item -> matchPattern path pattern item |> List.isEmpty) -> []
            | Data.List _ -> mismatch "at least one matching list item" (Some actual)
            | _ -> mismatch "list" (Some actual)
        | OneOf patterns ->
            if patterns |> List.exists (fun candidate -> matchPattern path candidate actual |> List.isEmpty) then []
            else mismatch "one of the supplied patterns" (Some actual)
        | Predicate(description, predicate) ->
            try if predicate actual then [] else mismatch description (Some actual)
            with error -> mismatch $"{description} (predicate threw: {error.Message})" (Some actual)

    /// <summary>Checks path-based expectations and accumulates structured mismatches.</summary>
    /// <example><code>Data.tryMatch [ at "name" "Ada" ] (data [ "name" =&gt; "Grace" ])
    /// // Error [ mismatch at path "name": expected "Ada", found "Grace" ]</code></example>
    let tryMatch (expectations: DataExpectation list) (actual: Data) : Result<unit, DataMismatch list> =
        if isNull (box expectations) then nullArg (nameof expectations)

        let mismatches =
            expectations
            |> List.mapi (fun index expectation ->
                match expectation.Pattern, tryResolveDetailed expectation.ParsedPath actual with
                | None, Error _ -> []
                | None, Ok value -> [ { ExpectationIndex = index; Path = expectation.ParsedPath; Expected = "an absent value"; Actual = Some value } ]
                | Some pattern, Error _ -> [ { ExpectationIndex = index; Path = expectation.ParsedPath; Expected = "a present value"; Actual = None } ]
                | Some pattern, Ok value ->
                    matchPattern expectation.ParsedPath pattern value
                    |> List.map (fun (path, expected, found) -> { ExpectationIndex = index; Path = path; Expected = expected; Actual = found }))
            |> List.concat

        if List.isEmpty mismatches then Ok() else Error mismatches

    /// <summary>Renders structured data in a compact, human-readable form.</summary>
    /// <example><code>Data.render (data [ "name" =&gt; "Ada"; "active" =&gt; true ])
    /// // { name: "Ada", active: true }</code></example>
    let render input = renderCompact input

    /// <summary>Renders structured data in an indented, human-readable form.</summary>
    /// <example><code>Data.renderIndented (data [ "name" =&gt; "Ada" ])
    /// // {
    /// //   name: "Ada"
    /// // }</code></example>
    let renderIndented input = DataErgonomicsHelpers.renderIndented input

    /// <summary>Attempts to extract text from one structured value.</summary>
    /// <example><code>Data.tryText (Data.Text "Ada") // Some "Ada"</code></example>
    let tryText = function Data.Text value -> Some value | _ -> None

    /// <summary>Attempts to extract a Boolean from one structured value.</summary>
    /// <example><code>Data.tryBool (Data.Bool true) // Some true</code></example>
    let tryBool = function Data.Bool value -> Some value | _ -> None

    /// <summary>Attempts to extract the preserved token from one number value.</summary>
    /// <example><code>Data.tryNumberToken (Data.Number "1e3") // Some "1e3"</code></example>
    let tryNumberToken = function Data.Number token -> Some token | _ -> None

    /// <summary>Attempts to extract items from one list value.</summary>
    /// <example><code>Data.tryList (Data.List []) // Some []</code></example>
    let tryList = function Data.List items -> Some items | _ -> None

    /// <summary>Attempts to extract ordered fields from one object value.</summary>
    /// <example><code>Data.tryObject (Data.Object []) // Some []</code></example>
    let tryObject = function Data.Object fields -> Some fields | _ -> None

    /// <summary>Concise opt-in syntax for literals, immutable edits, cases, and matching.</summary>
    module Syntax =
        /// <summary>Associates a field name with an exact value or recursive data pattern.</summary>
        let inline (=>) (name: string) (value: ^value) : DataField =
            assoc name value

        /// <summary>Associates a field name with an optional exact value, omitting <c>None</c>.</summary>
        let inline (?=>) (name: string) (value: ^value option) : DataField =
            optionalAssoc name value

        /// <summary>An explicit structured null used by literals and edits.</summary>
        let nil = Data.Null

        /// <summary>Constructs an exact number from a validated portable JSON number token.</summary>
        let num = number

        /// <summary>Builds an object from ordered field instructions.</summary>
        /// <example><code>data [ "name" =&gt; "Ada"; "active" =&gt; true ]
        /// // Data.Object [ "name", Data.Text "Ada"; "active", Data.Bool true ]</code></example>
        let data = data

        /// <summary>Returns exact field instructions for spreading an existing object literal.</summary>
        let fields = fields

        /// <summary>Replaces a value or adds a missing final object field.</summary>
        let inline set path value = DataEdit.set path value

        /// <summary>Replaces an existing value.</summary>
        let inline replace path value = DataEdit.replace path value

        /// <summary>Removes an existing field or list item.</summary>
        let remove = DataEdit.remove

        /// <summary>Appends an item to an existing list.</summary>
        let inline append path value = DataEdit.append path value

        /// <summary>Prepends an item to an existing list.</summary>
        let inline prepend path value = DataEdit.prepend path value

        /// <summary>Inserts an item at a valid list insertion index.</summary>
        let inline insert path index value =
            DataEdit.insert path index value

        /// <summary>Renames an existing object field without moving it.</summary>
        let rename = DataEdit.rename

        /// <summary>Applies an ordinary function to an existing value.</summary>
        let update = DataEdit.update

        /// <summary>Declares one named variation from a baseline.</summary>
        let variant name edits =
            ensureNonEmptyText (nameof name) name |> ignore
            if isNull (box edits) then nullArg (nameof edits)
            { Name = name; Edits = edits }

        /// <summary>Materializes named variations from one baseline.</summary>
        /// <example><code>variants [ variant "inactive" [ replace "active" false ] ] (data [ "active" =&gt; true ])
        /// // [ { Name = "inactive"; Value = data [ "active" =&gt; false ] } ]</code></example>
        let variants (variations: DataVariation list) baseline : DataCase list =
            if isNull (box variations) then nullArg (nameof variations)
            let duplicate = variations |> List.countBy _.Name |> List.tryFind (fun (_, count) -> count > 1)
            match duplicate with
            | Some(name, _) -> invalidArg (nameof variations) $"Variation name '{name}' is duplicated."
            | None ->
                variations
                |> List.map (fun (variation: DataVariation) ->
                    match tryPatch variation.Edits baseline with
                    | Ok value -> ({ Name = variation.Name; Value = value }: DataCase)
                    | Error failures -> raise (DataPatchException failures))

        /// <summary>Declares one named dimension in a Cartesian matrix.</summary>
        let dimension name variations =
            ensureNonEmptyText (nameof name) name |> ignore
            if isNull (box variations) then nullArg (nameof variations)
            { Name = name; Variations = variations }

        /// <summary>Materializes a deterministic Cartesian matrix, limited to 256 cases.</summary>
        /// <example><code>matrix [ dimension "status" [ variant "active" []; variant "inactive" [ replace "active" false ] ] ] baseline
        /// // cases named "status: active" and "status: inactive"</code></example>
        let matrix dimensions baseline =
            if isNull (box dimensions) then nullArg (nameof dimensions)
            let maximum = 256
            let count = dimensions |> List.fold (fun product dimension -> product * int64 dimension.Variations.Length) 1L
            if count > int64 maximum then invalidArg (nameof dimensions) $"The matrix would create {count} cases; the maximum is {maximum}."

            let folder cases dimension =
                [ for case in cases do
                      for variation in dimension.Variations do
                          let value = patch variation.Edits case.Value
                          let part = $"{dimension.Name}: {variation.Name}"
                          let name = if case.Name = "" then part else $"{case.Name} / {part}"
                          yield { Name = name; Value = value } ]

            dimensions |> List.fold folder [ { Name = ""; Value = baseline } ]

        /// <summary>Creates an exact recursive pattern.</summary>
        let inline exactly value = toPattern value

        /// <summary>Creates a partial object pattern from required fields.</summary>
        /// <example><code>Data.tryMatch [ at "" (containing [ "id" =&gt; 42 ]) ] (data [ "id" =&gt; 42; "extra" =&gt; true ])
        /// // Ok ()</code></example>
        let containing fields =
            if isNull (box fields) then nullArg (nameof fields)
            DataPattern(ObjectContaining fields)

        let inline private patterns values = values |> List.map toPattern

        /// <summary>Matches expected items as an unordered consumed subset.</summary>
        /// <example><code>Data.tryMatch [ at "items" (containingItems [ "Ada"; "Grace" ]) ] actual
        /// // Ok () when both values occur, in either order</code></example>
        let inline containingItems values = DataPattern.CreateListContaining(patterns values)

        /// <summary>Matches expected items as an ordered subsequence.</summary>
        let inline inOrder values = DataPattern.CreateListInOrder(patterns values)

        /// <summary>Requires every actual list item to satisfy a pattern.</summary>
        let inline allItems value = DataPattern.CreateEveryItem(toPattern value)

        /// <summary>Requires at least one actual list item to satisfy a pattern.</summary>
        let inline someItem value = DataPattern.CreateSomeItem(toPattern value)

        /// <summary>Matches any present value.</summary>
        let any = DataPattern Any

        /// <summary>Matches any text value.</summary>
        let anyText = DataPattern AnyText

        /// <summary>Matches any number token.</summary>
        let anyNumber = DataPattern AnyNumber

        /// <summary>Matches when one supplied alternative matches.</summary>
        let oneOf patterns = DataPattern(OneOf patterns)

        /// <summary>Matches an ordinary predicate and uses its description in diagnostics.</summary>
        let satisfying description predicate =
            ensureNonEmptyText (nameof description) description |> ignore
            if isNull (box predicate) then nullArg (nameof predicate)
            DataPattern(DataPatternNode.Predicate(description, predicate))

        /// <summary>Requires a path to contain an exact value or recursive pattern.</summary>
        let inline at path expected =
            ensureText (nameof path) path |> ignore
            DataExpectation.Create(path, toPattern expected)

        /// <summary>Requires a path to be absent.</summary>
        let absent path =
            ensureText (nameof path) path |> ignore
            DataExpectation(DataPath.parse path, None, path)

        /// <summary>Checks expectations or raises <c>DataMatchException</c>.</summary>
        /// <example><code>matching [ at "user.name" "Ada"; absent "error" ] actual
        /// // returns unit when both expectations hold; otherwise raises DataMatchException</code></example>
        let matching expectations actual =
            match tryMatch expectations actual with
            | Ok() -> ()
            | Error mismatches -> raise (DataMatchException mismatches)

    /// <summary>Deterministic JSON rendering for structured values.</summary>
    module Json =
        /// <summary>Renders compact deterministic JSON.</summary>
        /// <example><code>Data.Json.render (Data.Object [ "name", Data.Text "Ada" ])
        /// // {"name":"Ada"}</code></example>
        let render = jsonRenderCompact

        /// <summary>Renders indented deterministic JSON.</summary>
        /// <example><code>Data.Json.renderIndented (Data.Object [ "name", Data.Text "Ada" ])
        /// // {
        /// //   "name": "Ada"
        /// // }</code></example>
        let renderIndented = jsonRenderIndented
