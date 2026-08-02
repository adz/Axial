namespace Axial

/// <summary>Internal immutable edit application and atomic patching for structured data.</summary>
[<RequireQualifiedAccess>]
module internal DataPatching =
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

