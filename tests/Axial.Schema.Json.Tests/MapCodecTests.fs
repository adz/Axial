namespace Axial.Schema.Json.Tests

open Axial.Schema.Json
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>Covers the compiled JSON codec's handling of <c>Schema.map</c> dictionary value schemas.</summary>
module MapCodecTests =
    type private Thresholds = { Values: Map<string, decimal> }

    let private thresholdsSchema () =
        SchemaCE.schema<Thresholds> {
            SchemaCE.field "values" _.Values {
                withSchema (Schema.mapWith Schema.decimal)
            }
            SchemaCE.construct (fun values -> { Values = values })
        }

    [<Fact>]
    let ``round trips a Map field through the compiled codec`` () =
        let codec = Json.compile (thresholdsSchema ())
        let thresholds = { Values = Map.ofList [ "low", 1.5M; "high", 9.5M ] }

        let json = Json.serialize codec thresholds
        let roundTripped = Json.deserialize codec json

        test <@ roundTripped = thresholds @>

    [<Fact>]
    let ``serializes a Map field as a JSON object`` () =
        let codec = Json.compile (thresholdsSchema ())

        let json = Json.serialize codec { Values = Map.ofList [ "low", 1.5M ] }

        test <@ json.Contains "\"values\":{\"low\":1.5}" @>

    [<Fact>]
    let ``deserializes an empty JSON object into an empty Map`` () =
        let codec = Json.compile (thresholdsSchema ())

        test <@ Json.deserialize codec "{\"values\":{}}" = { Values = Map.empty } @>
