namespace Axial

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
    let objectOfMap = DataConversions.objectOfMap

    /// <summary>Builds an object from ordered name and value pairs.</summary>
    let objectOfList = DataConversions.objectOfList

    /// <summary>Builds object-shaped data from a map of scalar values.</summary>
    let ofMap = DataConversions.ofMap

    /// <summary>Builds object-shaped data from a .NET dictionary of scalar values.</summary>
    let ofDictionary = DataConversions.ofDictionary

    /// <summary>Builds object-shaped data from name and value pairs.</summary>
    let ofNameValues = DataConversions.ofNameValues

    /// <summary>Builds object-shaped data from a .NET name-value collection.</summary>
    let ofNameValueCollection = DataConversions.ofNameValueCollection

    /// <summary>Builds structured data from command-line arguments.</summary>
    let ofCliArgs = DataConversions.ofCliArgs

#if NET8_0_OR_GREATER && !FABLE_COMPILER
    /// <summary>Copies a .NET 8+ <c>System.Text.Json.JsonElement</c> into structured data.</summary>
    /// <remarks>This platform-specific convenience conversion is not available under Fable.</remarks>
    let ofJsonElement = DataConversions.ofJsonElement

    /// <summary>Copies a .NET 8+ <c>System.Text.Json.JsonDocument</c> into structured data.</summary>
    /// <remarks>This platform-specific convenience conversion is not available under Fable.</remarks>
    let ofJsonDocument = DataConversions.ofJsonDocument
#endif

#if FABLE_COMPILER
    /// <summary>Copies a value returned by JavaScript <c>JSON.parse</c> into structured data.</summary>
    /// <remarks>
    /// This Fable-specific convenience conversion is not available on .NET. JavaScript parsing has already discarded
    /// duplicate object fields and the original spelling of number tokens.
    /// </remarks>
    let ofJsonValue = DataConversions.ofJsonValue
#endif

    /// <summary>Builds structured data from flattened configuration keys.</summary>
    let ofConfiguration = DataConversions.ofConfiguration

    /// <summary>Builds structured data from .NET configuration key and value pairs.</summary>
    let ofConfigurationPairs = DataConversions.ofConfigurationPairs

    /// <summary>Attempts to find a value at a parsed path.</summary>
    let tryFind = DataLookup.tryFind

    /// <summary>Finds a value at a parsed path or returns <c>Null</c>.</summary>
    let lookup = DataLookup.lookup

    /// <summary>Attempts to parse a path and find its value.</summary>
    let tryFindPath = DataLookup.tryFindPath

    /// <summary>Parses a path and finds its value or returns <c>Null</c>.</summary>
    let lookupPath = DataLookup.lookupPath

    /// <summary>Attempts to render one scalar value for redisplay.</summary>
    let tryRedisplay = DataLookup.tryRedisplay

    /// <summary>Renders one scalar value for redisplay.</summary>
    let redisplay = DataLookup.redisplay

    /// <summary>Attempts to redisplay a scalar at a parsed path.</summary>
    let tryRedisplayAt = DataLookup.tryRedisplayAt

    /// <summary>Redisplays a scalar at a parsed path.</summary>
    let redisplayAt = DataLookup.redisplayAt

    /// <summary>Attempts to parse a path and redisplay its scalar.</summary>
    let tryRedisplayPath = DataLookup.tryRedisplayPath

    /// <summary>Parses a path and redisplays its scalar.</summary>
    let redisplayPath = DataLookup.redisplayPath

    /// <summary>Applies immutable edits atomically in declaration order.</summary>
    /// <example><code>Data.tryPatch [ replace "name" "Grace" ] (data [ "name" =&gt; "Ada" ])
    /// // Ok (data [ "name" =&gt; "Grace" ])</code></example>
    let tryPatch (edits: DataEdit list) (input: Data) : Result<Data, DataPatchFailure list> =
        DataPatching.tryPatch edits input

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
        DataComparison.diff expected actual

    /// <summary>Compares complete values and returns every structural difference.</summary>
    /// <example><code>Data.compare (data [ "name" =&gt; "Ada" ]) (data [ "name" =&gt; "Ada" ])
    /// // Ok ()</code></example>
    let compare expected actual = DataComparison.compare expected actual

    /// <summary>Checks path-based expectations and accumulates structured mismatches.</summary>
    /// <example><code>Data.tryMatch [ at "name" "Ada" ] (data [ "name" =&gt; "Grace" ])
    /// // Error [ mismatch at path "name": expected "Ada", found "Grace" ]</code></example>
    let tryMatch (expectations: DataExpectation list) (actual: Data) : Result<unit, DataMismatch list> =
        DataMatching.tryMatch expectations actual

    /// <summary>Renders structured data in a compact, human-readable form.</summary>
    /// <example><code>Data.render (data [ "name" =&gt; "Ada"; "active" =&gt; true ])
    /// // { name: "Ada", active: true }</code></example>
    let render input = DataRendering.renderCompact input

    /// <summary>Renders structured data in an indented, human-readable form.</summary>
    /// <example><code>Data.renderIndented (data [ "name" =&gt; "Ada" ])
    /// // {
    /// //   name: "Ada"
    /// // }</code></example>
    let renderIndented input = DataRendering.renderIndented input

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
        let render = DataRendering.jsonRenderCompact

        /// <summary>Renders indented deterministic JSON.</summary>
        /// <example><code>Data.Json.renderIndented (Data.Object [ "name", Data.Text "Ada" ])
        /// // {
        /// //   "name": "Ada"
        /// // }</code></example>
        let renderIndented = DataRendering.jsonRenderIndented
