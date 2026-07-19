namespace Axial.Tests

open Axial

open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module MapSchemaParseTests =
    type private Thresholds = { Values: Map<string, decimal> }

    let private thresholdsSchema =
        Schema.define<Thresholds>
        |> fieldWith (Schema.mapWith (Schema.decimal |> Schema.constrain Constraint.required)) "values" _.Values
        |> construct (fun values -> { Values = values })

    [<Fact>]
    let ``parse builds a Map from object-shaped structured data`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "values", Data.objectOfMap (Map.ofList [ "low", Data.Text "1.5"; "high", Data.Text "9.5" ]) ]
            )

        let parsed = Schema.parseRetainingInput thresholdsSchema raw

        test <@ parsed.Result = Ok { Values = Map.ofList [ "low", 1.5M; "high", 9.5M ] } @>

    [<Fact>]
    let ``parse builds an empty Map from an empty object`` () =
        let raw = Data.objectOfMap (Map.ofList [ "values", Data.Object [] ])

        let parsed = Schema.parseRetainingInput thresholdsSchema raw

        test <@ parsed.Result = Ok { Values = Map.empty } @>

    [<Fact>]
    let ``parse reports expected object when the map field structured data is a scalar`` () =
        let raw = Data.objectOfMap (Map.ofList [ "values", Data.Text "not-an-object" ])

        let parsed = Schema.parseRetainingInput thresholdsSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "values" ]; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``parse reports expected object when the map field structured data is a collection`` () =
        let raw = Data.objectOfMap (Map.ofList [ "values", Data.List [ Data.Text "1.5" ] ])

        let parsed = Schema.parseRetainingInput thresholdsSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "values" ]; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``parse reports entry failures at the entry's key path`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "values", Data.objectOfMap (Map.ofList [ "low", Data.Text "not-a-number" ]) ]
            )

        let parsed = Schema.parseRetainingInput thresholdsSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors |> List.map (fun d -> d.Path) = [ [ PathSegment.Name "values"; PathSegment.Key "low" ] ] @>
