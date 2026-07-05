namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

module SchemaUnionValueTests =
    type private CardDetails = { Number: string }

    type private Payment =
        | Card of CardDetails
        | Invoice of string

    type private Checkout = { Payment: Payment }

    let private cardSchema () =
        Schema.recordFor<CardDetails, _> (fun number -> { Number = number })
        |> Schema.field "number" _.Number (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.build

    let private paymentSchema () =
        Value.union
            "type"
            "value"
            [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) (Value.nested (cardSchema ()))
              UnionCase.create "invoice" Invoice (function Invoice id -> Some id | _ -> None) Value.text ]

    [<Fact>]
    let ``union value schema exposes discriminator payload and case descriptions`` () =
        let schema =
            Schema.recordFor<Checkout, _> (fun payment -> { Payment = payment })
            |> Schema.field "payment" _.Payment (paymentSchema ())
            |> Schema.build

        let payment =
            Inspect.model schema
            |> _.Fields
            |> List.exactlyOne

        match payment.Value.Shape with
        | ValueShape.Union union ->
            test <@ union.DiscriminatorField = "type" @>
            test <@ union.PayloadField = "value" @>
            test <@ union.Cases |> List.map _.Tag = [ "card"; "invoice" ] @>

            match union.Cases[0].Payload.Shape with
            | ValueShape.Nested model -> test <@ model.Fields |> List.map _.Name = [ "number" ] @>
            | _ -> failwith "Expected card payload to be a nested model."

            match union.Cases[1].Payload.Shape with
            | ValueShape.Primitive PrimitiveValueKind.Text -> ()
            | _ -> failwith "Expected invoice payload to be text."
        | _ -> failwith "Expected a union value shape."
