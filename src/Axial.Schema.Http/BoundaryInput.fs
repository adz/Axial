namespace Axial.Schema.Http

open Axial.Schema

/// <summary>Builds <see cref="T:Axial.Schema.RawInput" /> from the name/value surfaces an HTTP server hands over.</summary>
/// <remarks>
/// These constructors are host-neutral: adapters extract plain name/value pairs from their request type and the
/// conversion rules live here, so every host produces identical raw input for identical wire data.
/// </remarks>
[<RequireQualifiedAccess>]
module BoundaryInput =
    /// <summary>Builds object-shaped raw input from query-string pairs, grouping repeated names into collections.</summary>
    /// <remarks>Names are used verbatim; query strings do not carry nesting.</remarks>
    let ofQuery (pairs: seq<string * string>) : RawInput = RawInput.ofNameValues pairs

    [<RequireQualifiedAccess>]
    type private Node =
        | Values of string list
        | Fields of Map<string, Node>

    let rec private insert (node: Node) (segments: string list) (value: string) : Node =
        match segments, node with
        | [], Node.Values values -> Node.Values(values @ [ value ])
        | [], Node.Fields _ -> node
        | segment :: rest, Node.Fields fields ->
            let child =
                match fields.TryFind segment with
                | Some child -> child
                | None -> if rest.IsEmpty then Node.Values [] else Node.Fields Map.empty

            Node.Fields(fields.Add(segment, insert child rest value))
        | _ :: _, Node.Values _ -> node

    let rec private toJsonLike (node: Node) : JsonLikeValue =
        match node with
        | Node.Values [ value ] -> JsonLikeValue.String value
        | Node.Values values -> values |> List.map JsonLikeValue.String |> JsonLikeValue.Array
        | Node.Fields fields ->
            let indexed =
                fields
                |> Map.toList
                |> List.map (fun (key, child) ->
                    match System.Int32.TryParse key with
                    | true, index -> Some(index, child)
                    | false, _ -> None)

            if not fields.IsEmpty && indexed |> List.forall Option.isSome then
                indexed
                |> List.choose id
                |> List.sortBy fst
                |> List.map (snd >> toJsonLike)
                |> JsonLikeValue.Array
            else
                fields |> Map.map (fun _ child -> toJsonLike child) |> JsonLikeValue.Object

    /// <summary>Builds raw input from form pairs, where dotted names such as <c>address.street</c> nest.</summary>
    /// <remarks>
    /// Repeated names become collections, matching how HTML forms post multi-value fields, and sibling numeric
    /// segments such as <c>tags.0</c>/<c>tags.1</c> become ordered collections. The dot convention matches the flat
    /// field names produced when a form is rendered from a schema's inspection metadata. A name that appears once
    /// stays a scalar, so a list field submitted with a single selection should be posted as a repeated or indexed
    /// name; only the schema knows which fields are collections, and this builder deliberately does not.
    /// </remarks>
    let ofForm (pairs: seq<string * string>) : RawInput =
        if isNull (box pairs) then
            nullArg (nameof pairs)

        let root =
            pairs
            |> Seq.fold
                (fun node (name, value) ->
                    if isNull name || name = "" then
                        node
                    else
                        insert node (name.Split '.' |> List.ofArray) value)
                (Node.Fields Map.empty)

        RawInput.ofJsonLikeValue (toJsonLike root)
