namespace Axial.Tests

open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit

module MapSchemaParseTests =
    type private Thresholds = { Values: Map<string, decimal> }

    let private thresholdsSchema =
        Schema.recordFor<Thresholds, _> (fun values -> { Values = values })
        |> Schema.field "values" _.Values (Value.map (Value.decimal |> Value.withConstraint SchemaConstraint.required))
        |> Schema.build

    [<Fact>]
    let ``parse builds a Map from object-shaped raw input`` () =
        let raw =
            RawInput.Object(
                Map.ofList [ "values", RawInput.Object(Map.ofList [ "low", RawInput.Scalar "1.5"; "high", RawInput.Scalar "9.5" ]) ]
            )

        let parsed = Model.parse thresholdsSchema raw

        test <@ parsed.Result = Ok { Values = Map.ofList [ "low", 1.5M; "high", 9.5M ] } @>

    [<Fact>]
    let ``parse builds an empty Map from an empty object`` () =
        let raw = RawInput.Object(Map.ofList [ "values", RawInput.Object Map.empty ])

        let parsed = Model.parse thresholdsSchema raw

        test <@ parsed.Result = Ok { Values = Map.empty } @>

    [<Fact>]
    let ``parse reports expected object when the map field raw input is a scalar`` () =
        let raw = RawInput.Object(Map.ofList [ "values", RawInput.Scalar "not-an-object" ])

        let parsed = Model.parse thresholdsSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "values" ]; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``parse reports expected object when the map field raw input is a collection`` () =
        let raw = RawInput.Object(Map.ofList [ "values", RawInput.Many [ RawInput.Scalar "1.5" ] ])

        let parsed = Model.parse thresholdsSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "values" ]; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``parse reports entry failures at the entry's key path`` () =
        let raw =
            RawInput.Object(
                Map.ofList [ "values", RawInput.Object(Map.ofList [ "low", RawInput.Scalar "not-a-number" ]) ]
            )

        let parsed = Model.parse thresholdsSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors |> List.map (fun d -> d.Path) = [ [ PathSegment.Name "values"; PathSegment.Key "low" ] ] @>
