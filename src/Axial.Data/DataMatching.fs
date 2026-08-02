namespace Axial

/// <summary>Internal recursive pattern matching and expectation evaluation for structured data.</summary>
[<RequireQualifiedAccess>]
module internal DataMatching =
    let private removeAt index values =
        values
        |> List.mapi (fun position value -> position, value)
        |> List.choose (fun (position, value) -> if position = index then None else Some value)

    let rec private matchPattern path (pattern: DataPattern) actual =
        let mismatch expected actual = [ path, expected, actual ]

        match pattern.Node with
        | Exact expected ->
            match DataComparison.compare expected actual with
            | Ok() -> []
            | Error differences ->
                differences
                |> List.map (fun difference ->
                    path @ difference.Path,
                    (difference.Expected |> Option.map DataRendering.renderCompact |> Option.defaultValue "an absent value"),
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

