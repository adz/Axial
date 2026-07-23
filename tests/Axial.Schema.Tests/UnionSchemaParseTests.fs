namespace Axial.Tests

open Axial

open Axial.ErrorHandling

open Axial.Refined
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module UnionSchemaParseTests =
    type private CardDetails =
        {
            Number: NonBlankString
        }

    type private Payment =
        | Card of CardDetails
        | Invoice of Slug

    type private Checkout =
        {
            Payment: Payment
        }

    let private cardSchema () =
        SchemaCE.schema<CardDetails> {
            SchemaCE.field "number" _.Number {
                withSchema RefinedSchemas.nonBlankString
            }
            SchemaCE.construct (fun number -> { Number = number })
        }

    let private paymentValue () =
        Schema.union
            "type"
            "value"
            [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) ((cardSchema ()))
              UnionCase.create "invoice" Invoice (function Invoice slug -> Some slug | _ -> None) RefinedSchemas.slug ]

    let private checkoutSchema () =
        SchemaCE.schema<Checkout> {
            SchemaCE.field "payment" _.Payment {
                withSchema (paymentValue ())
            }
            SchemaCE.construct (fun payment -> { Payment = payment })
        }

    [<Fact>]
    let ``parse builds tagged union cases from discriminator and payload`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "payment",
                      Data.objectOfMap (Map.ofList
                              [ "type", Data.Text "card"
                                "value", Data.objectOfMap (Map.ofList [ "number", Data.Text "4242" ]) ]
                      ) ]
            )

        let parsed = Schema.parseRetainingInput (checkoutSchema ()) raw

        test
            <@ parsed.Result
               |> Result.map (fun checkout ->
                   match checkout.Payment with
                   | Card details -> details.Number.Value
                   | Invoice slug -> slug.Value) =
                Ok "4242" @>

    [<Fact>]
    let ``parse attaches wrong tag diagnostics to the discriminator field`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "payment",
                      Data.objectOfMap (Map.ofList [ "type", Data.Text "cash"; "value", Data.Text "ignored" ]) ]
            )

        let parsed = Schema.parseRetainingInput (checkoutSchema ()) raw

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "payment"; PathSegment.Name "type" ]
                                   Error = SchemaError.NotOneOf "card|invoice" } ] @>

    [<Fact>]
    let ``parse prefixes case payload diagnostics under the payload field`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "payment",
                      Data.objectOfMap (Map.ofList
                              [ "type", Data.Text "card"
                                "value", Data.objectOfMap (Map.ofList [ "number", Data.Text "" ]) ]
                      ) ]
            )

        let parsed = Schema.parseRetainingInput (checkoutSchema ()) raw

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "payment"; PathSegment.Name "value"; PathSegment.Name "number" ]
                                   Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse supports refined scalar case payloads`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "payment", Data.objectOfMap (Map.ofList [ "type", Data.Text "invoice"; "value", Data.Text "inv-42" ]) ]
            )

        let parsed = Schema.parseRetainingInput (checkoutSchema ()) raw

        test
            <@ parsed.Result
               |> Result.map (fun checkout ->
                   match checkout.Payment with
                   | Invoice slug -> slug.Value
                   | Card details -> details.Number.Value) =
                Ok "inv-42" @>

    [<Fact>]
    let ``validate checks existing union values through case extractors`` () =
        let model =
            { Payment =
                match Refine.slug "inv-42" with
                | Ok slug -> Invoice slug
                | Error error -> failwithf "Unexpected slug failure: %A" error }

        let result = Schema.check (checkoutSchema ()) model

        test <@ result = Ok model @>
