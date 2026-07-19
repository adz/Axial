namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that a JSON object can be described as a dictionary with <c>Schema.map</c>, keyed by text, and that the
/// resulting value schema is inspectable the same way other collection value schemas are.
/// </summary>
type MapVisibility =
    | Private
    | Team

module SchemaMapValueTests =
    [<Fact>]
    let ``map builds a dictionary value schema from a primitive item schema`` () =
        let thresholds = Schema.mapWith (Schema.decimal |> Schema.constrain Constraint.required)

        match thresholds.ValueDefinition.Shape with
        | MapValueDefinition collection ->
            match collection.Item.Shape with
            | PrimitiveValueDefinition PrimitiveValueKind.Decimal -> ()
            | _ -> failwith "Expected the map item to keep the supplied primitive value schema."

            test <@ collection.Item.Constraints |> List.map Constraint.code = [ "required" ] @>
        | PrimitiveValueDefinition _
        | RefinedValueDefinition _
        | NestedValueDefinition _
        | ManyValueDefinition _
        | UnionValueDefinition _
        | UnionInlineValueDefinition _
        | EnumValueDefinition _
        | OptionValueDefinition _ -> failwith "Expected a map value schema."

    [<Fact>]
    let ``a map value schema built from Schema.mapWith is not a refined or primitive value schema`` () =
        let mapValue = Schema.mapWith Schema.text

        test <@ not (Schema.isRefined mapValue) @>

    [<Fact>]
    let ``BoxEntries builds a Map from parsed key/value entries`` () =
        let mapValue = Schema.mapWith Schema.``int``

        match mapValue.ValueDefinition.Shape with
        | MapValueDefinition collection ->
            let boxed = collection.BoxEntries [ "a", box 1; "b", box 2 ]
            test <@ unbox<Map<string, int>> boxed = Map.ofList [ "a", 1; "b", 2 ] @>
        | _ -> failwith "Expected a map value schema."

    [<Fact>]
    let ``Entries projects a trusted Map back into type-erased key/value pairs`` () =
        let mapValue = Schema.mapWith Schema.``int``

        match mapValue.ValueDefinition.Shape with
        | MapValueDefinition collection ->
            let entries = collection.Entries(box (Map.ofList [ "a", 1; "b", 2 ]))
            test <@ entries |> List.map (fun (k, v) -> k, unbox<int> v) |> List.sort = [ "a", 1; "b", 2 ] @>
        | _ -> failwith "Expected a map value schema."

    [<Fact>]
    let ``JsonSchema lowers a map value schema to object with additionalProperties`` () =
        let document = JsonSchema.generateValue (Schema.mapWith Schema.``int``)

        test <@ document.Contains "\"type\":\"object\"" @>
        test <@ document.Contains "\"additionalProperties\":{\"type\":\"integer\"}" @>

    [<Fact>]
    let ``JsonSchema lowers withDefault to the default keyword`` () =
        let document = JsonSchema.generateValue (Schema.``int`` |> Schema.withDefault 30)

        test <@ document.Contains "\"default\":30" @>

    [<Fact>]
    let ``JsonSchema lowers an enum default to its wire tag`` () =
        let cases = [ EnumCase.create "private" MapVisibility.Private; EnumCase.create "team" MapVisibility.Team ]
        let document = JsonSchema.generateValue (Schema.enum cases |> Schema.withDefault MapVisibility.Private)

        test <@ document.Contains "\"default\":\"private\"" @>

    [<Fact>]
    let ``Schema.defaultValue returns the nearest declared default`` () =
        let schema = Schema.``int`` |> Schema.withDefault 30

        test <@ Schema.defaultValue schema = Some 30 @>
        test <@ Schema.defaultValue Schema.``int`` = None @>

    [<Fact>]
    let ``JsonSchema lowers the multipleOf constraint`` () =
        let document = JsonSchema.generateValue (Schema.``int`` |> Schema.constrain (Constraint.multipleOf 5))

        test <@ document.Contains "\"multipleOf\":5" @>
