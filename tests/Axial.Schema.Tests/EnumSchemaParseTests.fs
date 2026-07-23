namespace Axial.Tests

open Axial

open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module EnumSchemaParseTests =
    type private Color =
        | Red
        | Green
        | Blue

    type private Swatch = { Color: Color }

    let private colorSchema () =
        Schema.enum [ EnumCase.create "red" Red; EnumCase.create "green" Green; EnumCase.create "blue" Blue ]

    let private swatchSchema () =
        SchemaCE.schema<Swatch> {
            SchemaCE.field "color" _.Color {
                withSchema (colorSchema ())
            }
            SchemaCE.construct (fun color -> { Color = color })
        }

    [<Fact>]
    let ``parse builds the enum case matching the tag`` () =
        let raw = Data.objectOfMap (Map.ofList [ "color", Data.Text "green" ])

        let parsed = Schema.parseRetainingInput (swatchSchema ()) raw

        test <@ parsed.Result = Ok { Color = Green } @>

    [<Fact>]
    let ``parse reports an unknown tag at the field path`` () =
        let raw = Data.objectOfMap (Map.ofList [ "color", Data.Text "purple" ])

        let parsed = Schema.parseRetainingInput (swatchSchema ()) raw

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "color" ]
                                   Error = SchemaError.NotOneOf "red|green|blue" } ] @>

    [<Fact>]
    let ``validate checks existing enum values through case equality`` () =
        let result = Schema.check (swatchSchema ()) { Color = Blue }

        test <@ result = Ok { Color = Blue } @>
