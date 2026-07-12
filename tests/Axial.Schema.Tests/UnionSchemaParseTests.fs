namespace Axial.Tests

open Axial.ErrorHandling

open Axial.Refined
open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit

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
        Schema.recordFor<CardDetails, _> (fun number -> { Number = number })
        |> Schema.field "number" _.Number RefinedSchemas.nonBlankString
        |> Schema.build

    let private paymentValue () =
        Schema.union
            "type"
            "value"
            [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) ((cardSchema ()))
              UnionCase.create "invoice" Invoice (function Invoice slug -> Some slug | _ -> None) RefinedSchemas.slug ]

    let private checkoutSchema () =
        Schema.recordFor<Checkout, _> (fun payment -> { Payment = payment })
        |> Schema.field "payment" _.Payment (paymentValue ())
        |> Schema.build

    [<Fact>]
    let ``parse builds tagged union cases from discriminator and payload`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "payment",
                      RawInput.Object(
                          Map.ofList
                              [ "type", RawInput.Scalar "card"
                                "value", RawInput.Object(Map.ofList [ "number", RawInput.Scalar "4242" ]) ]
                      ) ]
            )

        let parsed = Schema.parse (checkoutSchema ()) raw

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
            RawInput.Object(
                Map.ofList
                    [ "payment",
                      RawInput.Object(Map.ofList [ "type", RawInput.Scalar "cash"; "value", RawInput.Scalar "ignored" ]) ]
            )

        let parsed = Schema.parse (checkoutSchema ()) raw

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "payment"; PathSegment.Name "type" ]
                                   Error = SchemaError.NotOneOf "card|invoice" } ] @>

    [<Fact>]
    let ``parse prefixes case payload diagnostics under the payload field`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "payment",
                      RawInput.Object(
                          Map.ofList
                              [ "type", RawInput.Scalar "card"
                                "value", RawInput.Object(Map.ofList [ "number", RawInput.Scalar "" ]) ]
                      ) ]
            )

        let parsed = Schema.parse (checkoutSchema ()) raw

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "payment"; PathSegment.Name "value"; PathSegment.Name "number" ]
                                   Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse supports refined scalar case payloads`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "payment", RawInput.Object(Map.ofList [ "type", RawInput.Scalar "invoice"; "value", RawInput.Scalar "inv-42" ]) ]
            )

        let parsed = Schema.parse (checkoutSchema ()) raw

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
