namespace Axial.Tests

open Axial

open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module UnionInlineSchemaParseTests =
    type private CardDetails = { Number: string }
    type private InvoiceDetails = { Reference: string }

    type private Payment =
        | Card of CardDetails
        | Invoice of InvoiceDetails

    type private Checkout = { Payment: Payment }

    let private cardSchema () =
        schema<CardDetails> {
            field "number" _.Number {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            construct (fun number -> { Number = number })
        }

    let private invoiceSchema () =
        schema<InvoiceDetails> {
            field "reference" _.Reference
            construct (fun reference -> { Reference = reference })
        }

    let private paymentValue () =
        Schema.inlineUnion
            "type"
            [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) ((cardSchema ()))
              UnionCase.create
                  "invoice"
                  Invoice
                  (function Invoice details -> Some details | _ -> None)
                  ((invoiceSchema ())) ]

    let private checkoutSchema () =
        schema<Checkout> {
            field "payment" _.Payment {
                withSchema (paymentValue ())
            }
            construct (fun payment -> { Payment = payment })
        }

    [<Fact>]
    let ``parse builds the case matching the discriminator from spliced fields`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "payment",
                      Data.objectOfMap (Map.ofList [ "type", Data.Text "card"; "number", Data.Text "4242" ]) ]
            )

        let parsed = Schema.parseRetainingInput (checkoutSchema ()) raw

        test <@ parsed.Result = Ok { Payment = Card { Number = "4242" } } @>

    [<Fact>]
    let ``parse reports an unknown tag at the discriminator field`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "payment", Data.objectOfMap (Map.ofList [ "type", Data.Text "cash"; "number", Data.Text "4242" ]) ]
            )

        let parsed = Schema.parseRetainingInput (checkoutSchema ()) raw

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "payment"; PathSegment.Name "type" ]
                                   Error = SchemaError.NotOneOf "card|invoice" } ] @>

    [<Fact>]
    let ``parse reports missing spliced payload fields at their own path`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "payment", Data.objectOfMap (Map.ofList [ "type", Data.Text "card" ]) ])

        let parsed = Schema.parseRetainingInput (checkoutSchema ()) raw

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "payment"; PathSegment.Name "number" ]
                                   Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``validate checks existing union-inline values through case extractors`` () =
        let model = { Payment = Invoice { Reference = "inv-42" } }

        let result = Schema.check (checkoutSchema ()) model

        test <@ result = Ok model @>
