namespace Axial.Tests

open System
open Axial.Schema
open Swensen.Unquote
open Xunit

module SchemaUnionInlineValueTests =
    type private CardDetails = { Number: string }
    type private InvoiceDetails = { Reference: string }

    type private Payment =
        | Card of CardDetails
        | Invoice of InvoiceDetails

    type private Checkout = { Payment: Payment }

    let private cardSchema () =
        Schema.recordFor<CardDetails, _> (fun number -> { Number = number })
        |> Schema.text "number" _.Number
        |> Schema.build

    let private invoiceSchema () =
        Schema.recordFor<InvoiceDetails, _> (fun reference -> { Reference = reference })
        |> Schema.text "reference" _.Reference
        |> Schema.build

    let private paymentSchema () =
        Value.unionInline
            "type"
            [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) (Value.nested (cardSchema ()))
              UnionCase.create
                  "invoice"
                  Invoice
                  (function Invoice details -> Some details | _ -> None)
                  (Value.nested (invoiceSchema ())) ]

    [<Fact>]
    let ``union-inline value schema exposes discriminator and spliced case fields`` () =
        let schema =
            Schema.recordFor<Checkout, _> (fun payment -> { Payment = payment })
            |> Schema.field "payment" _.Payment (paymentSchema ())
            |> Schema.build

        let payment =
            Inspect.model schema
            |> _.Fields
            |> List.exactlyOne

        match payment.Value.Shape with
        | ValueShape.UnionInline union ->
            test <@ union.DiscriminatorField = "type" @>
            test <@ union.Cases |> List.map _.Tag = [ "card"; "invoice" ] @>
            test <@ union.Cases[0].Payload.Fields |> List.map _.Name = [ "number" ] @>
            test <@ union.Cases[1].Payload.Fields |> List.map _.Name = [ "reference" ] @>
        | _ -> failwith "Expected a union-inline value shape."

    [<Fact>]
    let ``union-inline value schemas lower to json schema oneOf with spliced properties`` () =
        let schema =
            Schema.recordFor<Checkout, _> (fun payment -> { Payment = payment })
            |> Schema.field "payment" _.Payment (paymentSchema ())
            |> Schema.build

        let generated = JsonSchema.generate schema

        test <@ generated.Contains "\"payment\":{\"oneOf\":[" @>

        test
            <@ generated.Contains
                "{\"type\":\"object\",\"properties\":{\"type\":{\"const\":\"card\"},\"number\":{\"type\":\"string\"}},\"required\":[\"type\",\"number\"]}" @>

        test
            <@ generated.Contains
                "{\"type\":\"object\",\"properties\":{\"type\":{\"const\":\"invoice\"},\"reference\":{\"type\":\"string\"}},\"required\":[\"type\",\"reference\"]}" @>

    [<Fact>]
    let ``unionInline rejects payload field names that collide with the discriminator`` () =
        let colliding =
            Schema.recordFor<CardDetails, _> (fun number -> { Number = number })
            |> Schema.text "type" _.Number
            |> Schema.build

        Assert.Throws<ArgumentException>(fun () ->
            Value.unionInline
                "type"
                [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) (Value.nested colliding) ]
            |> ignore)
        |> ignore

    [<Fact>]
    let ``unionInline rejects payloads that are not nested model schemas`` () =
        Assert.Throws<ArgumentException>(fun () ->
            Value.unionInline
                "type"
                [ UnionCase.create
                      "invoice"
                      (fun reference -> Invoice { Reference = reference })
                      (function Invoice details -> Some details.Reference | _ -> None)
                      Value.text ]
            |> ignore)
        |> ignore
