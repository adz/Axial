namespace Axial

open Axial.Schema
open Axial.Validation

[<RequireQualifiedAccess>]
module TestPath =
    let fromLegacy segments =
        segments
        |> List.fold (fun path segment ->
            let next =
                match segment with
                | PathSegment.Key key
                | PathSegment.Name key -> Path.key key
                | PathSegment.Index index -> Path.index index

            Path.append path next) Path.root
