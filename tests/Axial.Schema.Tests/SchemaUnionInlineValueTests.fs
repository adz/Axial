namespace Axial.Tests

open System
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module SchemaUnionInlineValueTests =
    type private CardDetails = { Number: string }
    type private InvoiceDetails = { Reference: string }

    type private Payment =
        | Card of CardDetails
        | Invoice of InvoiceDetails

    type private Checkout = { Payment: Payment }

    let private cardSchema () =
        Schema.define<CardDetails>
        |> fieldWith Schema.text "number" _.Number
        |> construct (fun number -> { Number = number })

    let private invoiceSchema () =
        Schema.define<InvoiceDetails>
        |> fieldWith Schema.text "reference" _.Reference
        |> construct (fun reference -> { Reference = reference })

    let private paymentSchema () =
        Schema.inlineUnion
            "type"
            [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) ((cardSchema ()))
              UnionCase.create
                  "invoice"
                  Invoice
                  (function Invoice details -> Some details | _ -> None)
                  ((invoiceSchema ())) ]

    [<Fact>]
    let ``union-inline value schema exposes discriminator and spliced case fields`` () =
        let schema =
            Schema.define<Checkout>
            |> fieldWith (paymentSchema ()) "payment" _.Payment
            |> construct (fun payment -> { Payment = payment })

        let payment =
            Inspect.model schema
            |> _.Fields
            |> List.exactlyOne

        match payment.Schema.Shape with
        | SchemaShape.UnionInline union ->
            test <@ union.DiscriminatorField = "type" @>
            test <@ union.Cases |> List.map _.Tag = [ "card"; "invoice" ] @>
            test <@ union.Cases[0].Payload.Fields |> List.map _.Name = [ "number" ] @>
            test <@ union.Cases[1].Payload.Fields |> List.map _.Name = [ "reference" ] @>
        | _ -> failwith "Expected a union-inline value shape."

    [<Fact>]
    let ``union-inline value schemas lower to json schema oneOf with spliced properties`` () =
        let schema =
            Schema.define<Checkout>
            |> fieldWith (paymentSchema ()) "payment" _.Payment
            |> construct (fun payment -> { Payment = payment })

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
            Schema.define<CardDetails>
            |> fieldWith Schema.text "type" _.Number
            |> construct (fun number -> { Number = number })

        Assert.Throws<ArgumentException>(fun () ->
            Schema.inlineUnion
                "type"
                [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) (colliding) ]
            |> ignore)
        |> ignore

    [<Fact>]
    let ``unionInline rejects payloads that are not nested model schemas`` () =
        Assert.Throws<ArgumentException>(fun () ->
            Schema.inlineUnion
                "type"
                [ UnionCase.create
                      "invoice"
                      (fun reference -> Invoice { Reference = reference })
                      (function Invoice details -> Some details.Reference | _ -> None)
                      Schema.text ]
            |> ignore)
        |> ignore
