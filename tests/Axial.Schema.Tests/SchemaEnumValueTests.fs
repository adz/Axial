namespace Axial.Tests

open System
open Axial.Schema
open Swensen.Unquote
open Xunit

module SchemaEnumValueTests =
    type private Color =
        | Red
        | Green
        | Blue

    type private Swatch = { Color: Color }

    let private colorSchema () =
        Schema.enum [ EnumCase.create "red" Red; EnumCase.create "green" Green; EnumCase.create "blue" Blue ]

    [<Fact>]
    let ``enum value schema exposes case tags`` () =
        let schema =
            Schema.recordFor<Swatch, _> (fun color -> { Color = color })
            |> Schema.field "color" _.Color (colorSchema ())
            |> Schema.build

        let color =
            Inspect.model schema
            |> _.Fields
            |> List.exactlyOne

        match color.Schema.Shape with
        | SchemaShape.Enum enum -> test <@ enum.Cases |> List.map _.Tag = [ "red"; "green"; "blue" ] @>
        | _ -> failwith "Expected an enum value shape."

    [<Fact>]
    let ``enum value schemas lower to json schema string enum`` () =
        let schema =
            Schema.recordFor<Swatch, _> (fun color -> { Color = color })
            |> Schema.field "color" _.Color (colorSchema ())
            |> Schema.build

        let generated = JsonSchema.generate schema

        test <@ generated.Contains "\"color\":{\"type\":\"string\",\"enum\":[\"red\",\"green\",\"blue\"]}" @>

    [<Fact>]
    let ``enumOf rejects duplicate tags`` () =
        Assert.Throws<ArgumentException>(fun () ->
            Schema.enum [ EnumCase.create "red" Red; EnumCase.create "red" Green ] |> ignore)
        |> ignore
