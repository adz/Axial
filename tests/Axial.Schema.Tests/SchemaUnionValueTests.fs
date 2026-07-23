namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module SchemaUnionValueTests =
    type private CardDetails = { Number: string }

    type private Payment =
        | Card of CardDetails
        | Invoice of string

    type private Checkout = { Payment: Payment }

    let private cardSchema () =
        SchemaCE.schema<CardDetails> {
            SchemaCE.field "number" _.Number {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.construct (fun number -> { Number = number })
        }

    let private paymentSchema () =
        Schema.union
            "type"
            "value"
            [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) ((cardSchema ()))
              UnionCase.create "invoice" Invoice (function Invoice id -> Some id | _ -> None) Schema.text ]

    [<Fact>]
    let ``union value schema exposes discriminator payload and case descriptions`` () =
        let schema =
            SchemaCE.schema<Checkout> {
                SchemaCE.field "payment" _.Payment {
                    withSchema (paymentSchema ())
                }
                SchemaCE.construct (fun payment -> { Payment = payment })
            }

        let payment =
            Inspect.model schema
            |> _.Fields
            |> List.exactlyOne

        match payment.Schema.Shape with
        | SchemaShape.Union union ->
            test <@ union.DiscriminatorField = "type" @>
            test <@ union.PayloadField = "value" @>
            test <@ union.Cases |> List.map _.Tag = [ "card"; "invoice" ] @>

            match union.Cases[0].Payload.Shape with
            | SchemaShape.Nested model -> test <@ model.Fields |> List.map _.Name = [ "number" ] @>
            | _ -> failwith "Expected card payload to be a nested model."

            match union.Cases[1].Payload.Shape with
            | SchemaShape.Primitive PrimitiveValueKind.Text -> ()
            | _ -> failwith "Expected invoice payload to be text."
        | _ -> failwith "Expected a union value shape."

    [<Fact>]
    let ``union value schemas lower to json schema oneOf with const discriminators`` () =
        let schema =
            SchemaCE.schema<Checkout> {
                SchemaCE.field "payment" _.Payment {
                    withSchema (paymentSchema ())
                }
                SchemaCE.construct (fun payment -> { Payment = payment })
            }

        let generated = JsonSchema.generate schema

        test <@ generated.Contains "\"payment\":{\"oneOf\":[" @>
        test <@ generated.Contains "{\"type\":\"object\",\"properties\":{\"type\":{\"const\":\"card\"},\"value\":{\"type\":\"object\",\"properties\":{\"number\":{\"type\":\"string\"}},\"required\":[\"number\"]}},\"required\":[\"type\",\"value\"]}" @>
        test <@ generated.Contains "{\"type\":\"object\",\"properties\":{\"type\":{\"const\":\"invoice\"},\"value\":{\"type\":\"string\"}},\"required\":[\"type\",\"value\"]}" @>
