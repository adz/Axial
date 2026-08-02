namespace Axial

/// <summary>Internal lookup and scalar redisplay operations over owned structured data.</summary>
[<RequireQualifiedAccess>]
module internal DataLookup =
    let private tryRedisplayValue (input: Data) =
        match input with
        | Data.Null -> Some ""
        | Data.Text value -> Some value
        | Data.Number token -> Some token
        | Data.Bool value -> Some(if value then "true" else "false")
        | Data.List _
        | Data.Object _ -> None

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
    /// <example>
    /// <code>
    /// Data.Text "42" |> Data.redisplay
    /// // "42"
    ///
    /// Data.Null |> Data.redisplay
    /// // ""
    ///
    /// Data.objectOfList [ "name", Data.Text "Ada" ] |> Data.redisplay
    /// // "" (object-shaped input has no scalar to redisplay)
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// let input =
    ///     Data.objectOfList [
    ///         "address", Data.objectOfList [ "city", Data.Text "Boston" ]
    ///         "tags", Data.List [ Data.Text "admin"; Data.Text "billing" ]
    ///     ]
    ///
    /// Data.redisplayPath "address.city" input
    /// // "Boston"
    ///
    /// Data.redisplayPath "tags[1]" input
    /// // "billing"
    ///
    /// Data.redisplayPath "address.zip" input
    /// // "" (path not present)
    /// </code>
    /// </example>
    let redisplayPath (path: string) (input: Data) : string =
        DataPath.parse path |> fun parsedPath -> redisplayAt parsedPath input

