namespace Axial.Codec.Tests

open System
open Axial.Codec
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Covers the compiled JSON codec: schema-driven round trips for every value shape, wire tolerance (unknown fields,
/// whitespace, escapes, field order), and path-aware decode failures.
/// </summary>
module JsonCodecTests =

    [<Fact>]
    let ``compiles and round trips a non-record root schema`` () =
        let schema = Schema.list (Schema.int |> Schema.constrain (Constraint.atLeast 0))
        let codec = Json.compile schema
        let value = [ 1; 2; 3 ]
        test <@ Json.deserialize codec (Json.serialize codec value) = value @>

    type private Email = private EmailValue of string

    type private Address = { Street: string; City: string }

    type private Tag = { Label: string }

    type private Customer =
        { Id: Guid
          Name: string
          Email: Email
          Age: int
          Balance: decimal
          Newsletter: bool
          Joined: DateOnly
          LastSeen: DateTimeOffset
          Address: Address
          Tags: Tag list
          Scores: int list }

    type private Payment =
        | Card of CardDetails
        | Invoice of InvoiceDetails

    and private CardDetails = { Number: string; Expiry: string }

    and private InvoiceDetails = { Reference: string }

    let private emailSchema () =
        Schema.text
        |> Schema.convert (fun raw -> EmailValue raw) (fun (EmailValue raw) -> raw)

    let private addressSchema () =
        Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
        |> Schema.field "street" _.Street Schema.text
        |> Schema.field "city" _.City Schema.text
        |> Schema.build

    let private tagSchema () =
        Schema.recordFor<Tag, _> (fun label -> { Label = label })
        |> Schema.field "label" _.Label Schema.text
        |> Schema.build

    let private customerSchema () =
        Schema.recordFor<Customer, _> (fun id name email age balance newsletter joined lastSeen address tags scores ->
            { Id = id
              Name = name
              Email = email
              Age = age
              Balance = balance
              Newsletter = newsletter
              Joined = joined
              LastSeen = lastSeen
              Address = address
              Tags = tags
              Scores = scores })
        |> Schema.field "id" _.Id Schema.guid
        |> Schema.field "name" _.Name Schema.text
        |> Schema.field "email" _.Email (emailSchema ())
        |> Schema.field "age" _.Age Schema.int
        |> Schema.field "balance" _.Balance Schema.decimal
        |> Schema.field "newsletter" _.Newsletter Schema.bool
        |> Schema.field "joined" _.Joined Schema.date
        |> Schema.field "lastSeen" _.LastSeen Schema.dateTime
        |> Schema.field "address" _.Address (addressSchema ())
        |> Schema.field "tags" _.Tags (Schema.list (tagSchema ()))
        |> Schema.field "scores" _.Scores (Schema.list Schema.int)
        |> Schema.build

    let private sampleCustomer () =
        { Id = Guid.Parse "7d9a2f5e-95c8-4f2b-b1e3-2f6d3a1c9b42"
          Name = "Ada \"L\" Lovelace"
          Email = EmailValue "ada@example.com"
          Age = 36
          Balance = 1234.56m
          Newsletter = true
          Joined = DateOnly(2024, 3, 15)
          LastSeen = DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.FromHours 2.0)
          Address = { Street = "12 Analytical Way"; City = "London" }
          Tags = [ { Label = "vip" }; { Label = "early-adopter" } ]
          Scores = [ 3; 1; 4 ] }

    let private paymentSchema () =
        let cardSchema =
            Schema.recordFor<CardDetails, _> (fun number expiry -> { Number = number; Expiry = expiry })
            |> Schema.field "number" _.Number Schema.text
            |> Schema.field "expiry" _.Expiry Schema.text
            |> Schema.build

        let invoiceSchema =
            Schema.recordFor<InvoiceDetails, _> (fun reference -> { Reference = reference })
            |> Schema.field "reference" _.Reference Schema.text
            |> Schema.build

        Schema.union
            "type"
            "value"
            [ UnionCase.create
                  "card"
                  Card
                  (function
                  | Card details -> Some details
                  | _ -> None)
                  (cardSchema)
              UnionCase.create
                  "invoice"
                  Invoice
                  (function
                  | Invoice details -> Some details
                  | _ -> None)
                  (invoiceSchema) ]

    type private Order =
        { Reference: string
          Payment: Payment }

    let private orderSchema () =
        Schema.recordFor<Order, _> (fun reference payment -> { Order.Reference = reference; Payment = payment })
        |> Schema.field "reference" _.Reference Schema.text
        |> Schema.field "payment" _.Payment (paymentSchema ())
        |> Schema.build

    let private paymentInlineSchema () =
        let cardSchema =
            Schema.recordFor<CardDetails, _> (fun number expiry -> { Number = number; Expiry = expiry })
            |> Schema.field "number" _.Number Schema.text
            |> Schema.field "expiry" _.Expiry Schema.text
            |> Schema.build

        let invoiceSchema =
            Schema.recordFor<InvoiceDetails, _> (fun reference -> { Reference = reference })
            |> Schema.field "reference" _.Reference Schema.text
            |> Schema.build

        Schema.inlineUnion
            "type"
            [ UnionCase.create
                  "card"
                  Card
                  (function
                  | Card details -> Some details
                  | _ -> None)
                  (cardSchema)
              UnionCase.create
                  "invoice"
                  Invoice
                  (function
                  | Invoice details -> Some details
                  | _ -> None)
                  (invoiceSchema) ]

    type private InlineOrder =
        { Reference: string
          Payment: Payment }

    let private inlineOrderSchema () =
        Schema.recordFor<InlineOrder, _> (fun reference payment ->
            { InlineOrder.Reference = reference; Payment = payment })
        |> Schema.field "reference" _.Reference Schema.text
        |> Schema.field "payment" _.Payment (paymentInlineSchema ())
        |> Schema.build

    type private Color =
        | Red
        | Green
        | Blue

    type private Swatch = { Color: Color }

    let private swatchSchema () =
        Schema.recordFor<Swatch, _> (fun color -> { Color = color })
        |> Schema.field
            "color"
            _.Color
            (Schema.enum [ EnumCase.create "red" Red; EnumCase.create "green" Green; EnumCase.create "blue" Blue ])
        |> Schema.build

    [<Fact>]
    let ``round trips every value shape through one schema declaration`` () =
        let codec = Json.compile (customerSchema ())
        let customer = sampleCustomer ()

        let json = Json.serialize codec customer
        let roundTripped = Json.deserialize codec json

        test <@ roundTripped = customer @>

    [<Fact>]
    let ``serializes fields in declared order with schema wire names`` () =
        let codec = Json.compile (customerSchema ())

        let json = Json.serialize codec (sampleCustomer ())

        test <@ json.StartsWith "{\"id\":\"7d9a2f5e-95c8-4f2b-b1e3-2f6d3a1c9b42\",\"name\":\"Ada \\\"L\\\" Lovelace\"," @>
        test <@ json.Contains "\"joined\":\"2024-03-15\"" @>
        test <@ json.Contains "\"address\":{\"street\":\"12 Analytical Way\",\"city\":\"London\"}" @>
        test <@ json.Contains "\"tags\":[{\"label\":\"vip\"},{\"label\":\"early-adopter\"}]" @>
        test <@ json.Contains "\"scores\":[3,1,4]" @>

    [<Fact>]
    let ``decoding tolerates whitespace unknown fields reordering and escaped keys`` () =
        let schema =
            Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
            |> Schema.field "street" _.Street Schema.text
            |> Schema.field "city" _.City Schema.text
            |> Schema.build

        let codec = Json.compile schema

        let json =
            """
            {
                "unknown" : { "nested": [1, 2, {"deep": true}] },
                "city" : "København",
                "street" : "1 Main St"
            }
            """

        test <@ Json.deserialize codec json = { Street = "1 Main St"; City = "København" } @>

    [<Fact>]
    let ``round trips tagged unions and accepts payload before discriminator`` () =
        let codec = Json.compile (orderSchema ())

        let cardOrder: Order =
            { Reference = "ord-1"
              Payment = Card { Number = "4111"; Expiry = "12/28" } }

        let invoiceOrder: Order =
            { Reference = "ord-2"
              Payment = Invoice { Reference = "inv-42" } }

        let cardJson = Json.serialize codec cardOrder
        test <@ cardJson.Contains "\"payment\":{\"type\":\"card\",\"value\":{\"number\":\"4111\",\"expiry\":\"12/28\"}}" @>
        test <@ Json.deserialize codec cardJson = cardOrder @>
        test <@ Json.deserialize codec (Json.serialize codec invoiceOrder) = invoiceOrder @>

        let payloadFirst =
            """{"reference":"ord-1","payment":{"value":{"number":"4111","expiry":"12/28"},"type":"card"}}"""

        test <@ Json.deserialize codec payloadFirst = cardOrder @>

    [<Fact>]
    let ``missing required fields report their schema path`` () =
        let codec = Json.compile (addressSchema ())

        let ex =
            Assert.Throws<JsonCodecException>(fun () -> Json.deserialize codec """{"street":"1 Main St"}""" |> ignore)

        test <@ ex.Path = "$.city" @>
        test <@ ex.Message.Contains "missing required field" @>

    [<Fact>]
    let ``invalid nested values report a nested path`` () =
        let codec = Json.compile (customerSchema ())
        let json = (Json.serialize codec (sampleCustomer ())).Replace("\"age\":36", "\"age\":\"not-a-number\"")

        let ex = Assert.Throws<JsonCodecException>(fun () -> Json.deserialize codec json |> ignore)

        test <@ ex.Path = "$.age" @>

    [<Fact>]
    let ``invalid collection items report an indexed path`` () =
        let codec = Json.compile (customerSchema ())

        let json =
            (Json.serialize codec (sampleCustomer ()))
                .Replace("\"tags\":[{\"label\":\"vip\"}", "\"tags\":[{\"label\":7}")

        let ex = Assert.Throws<JsonCodecException>(fun () -> Json.deserialize codec json |> ignore)

        test <@ ex.Path = "$.tags[0].label" @>

    [<Fact>]
    let ``unknown union tags and missing payloads fail with union paths`` () =
        let codec = Json.compile (orderSchema ())

        let unknownTag =
            Assert.Throws<JsonCodecException>(fun () ->
                Json.deserialize codec """{"reference":"o","payment":{"type":"cash","value":{}}}""" |> ignore)

        test <@ unknownTag.Path = "$.payment.type" @>
        test <@ unknownTag.Message.Contains "unknown union case tag: cash" @>

        let missingPayload =
            Assert.Throws<JsonCodecException>(fun () ->
                Json.deserialize codec """{"reference":"o","payment":{"type":"card"}}""" |> ignore)

        test <@ missingPayload.Path = "$.payment.value" @>

    [<Fact>]
    let ``constructor results from buildResult schemas surface as decode failures`` () =
        let schema =
            Schema.recordFor<Address, _> (fun (street: string) (city: string) ->
                if street = "" then
                    Error "street must not be blank"
                else
                    Ok { Street = street; City = city })
            |> Schema.field "street" _.Street Schema.text
            |> Schema.field "city" _.City Schema.text
            |> Schema.buildResult

        let codec = Json.compile schema

        test <@ Json.deserialize codec """{"street":"1 Main St","city":"Leeds"}""" = { Street = "1 Main St"; City = "Leeds" } @>

        let ex =
            Assert.Throws<JsonCodecException>(fun () ->
                Json.deserialize codec """{"street":"","city":"Leeds"}""" |> ignore)

        test <@ ex.Message.Contains "street must not be blank" @>

    [<Fact>]
    let ``tryDeserialize returns rendered decode failures instead of raising`` () =
        let codec = Json.compile (addressSchema ())

        test <@ Json.tryDeserialize codec """{"street":"1 Main St","city":"Leeds"}""" = Ok { Street = "1 Main St"; City = "Leeds" } @>

        match Json.tryDeserialize codec """{"street":"1 Main St"}""" with
        | Error message -> test <@ message.Contains "$.city" @>
        | Ok _ -> failwith "Expected a decode failure."

    [<Fact>]
    let ``trailing content after the root value fails`` () =
        let codec = Json.compile (addressSchema ())

        let ex =
            Assert.Throws<JsonCodecException>(fun () ->
                Json.deserialize codec """{"street":"a","city":"b"} {}""" |> ignore)

        test <@ ex.Message.Contains "trailing content" @>

    [<Fact>]
    let ``serializeBytes and deserializeBytes round trip utf8 payloads`` () =
        let codec = Json.compile (customerSchema ())
        let customer = sampleCustomer ()

        let bytes = Json.serializeBytes codec customer

        test <@ Json.deserializeBytes codec bytes = customer @>

    [<Fact>]
    let ``serializeToStream and deserializeStreamAsync round trip through a stream`` () =
        task {
            let codec = Json.compile (customerSchema ())
            let customer = sampleCustomer ()

            use stream = new IO.MemoryStream()
            Json.serializeToStream codec stream customer
            test <@ stream.Position = int64 stream.Length @>

            stream.Position <- 0L
            let! roundTripped = Json.deserializeStreamAsync codec stream

            test <@ roundTripped = customer @>
        }

    [<Fact>]
    let ``escaped strings round trip control characters and unicode`` () =
        let codec = Json.compile (tagSchema ())
        let tag = { Label = "line1\nline2\ttab \"quoted\" \\slash  ünïcødé" }

        test <@ Json.deserialize codec (Json.serialize codec tag) = tag @>

    type private OptionalProfile =
        { Nickname: string option
          Name: string
          Age: int option
          Ratings: int option list }

    let private optionalProfileSchema () =
        Schema.recordFor<OptionalProfile, _> (fun nickname name age ratings ->
            { Nickname = nickname
              Name = name
              Age = age
              Ratings = ratings })
        |> Schema.field "nickname" _.Nickname (Schema.option Schema.text)
        |> Schema.field "name" _.Name Schema.text
        |> Schema.field "age" _.Age (Schema.option Schema.int)
        |> Schema.field "ratings" _.Ratings (Schema.list (Schema.option Schema.int))
        |> Schema.build

    [<Fact>]
    let ``encodes None optional fields as omitted even in first position`` () =
        let codec = Json.compile (optionalProfileSchema ())

        let json =
            Json.serialize codec { Nickname = None; Name = "Ada"; Age = None; Ratings = [] }

        test <@ json = "{\"name\":\"Ada\",\"ratings\":[]}" @>

    [<Fact>]
    let ``encodes Some optional fields as their payload`` () =
        let codec = Json.compile (optionalProfileSchema ())

        let json =
            Json.serialize codec { Nickname = Some "Lady A"; Name = "Ada"; Age = Some 36; Ratings = [ Some 5; None ] }

        test <@ json = "{\"nickname\":\"Lady A\",\"name\":\"Ada\",\"age\":36,\"ratings\":[5,null]}" @>

    [<Fact>]
    let ``decodes absent and null optional fields to None`` () =
        let codec = Json.compile (optionalProfileSchema ())

        let decoded = Json.deserialize codec "{\"name\":\"Ada\",\"age\":null,\"ratings\":[null,2]}"

        test <@ decoded = { Nickname = None; Name = "Ada"; Age = None; Ratings = [ None; Some 2 ] } @>

    [<Fact>]
    let ``round trips optional fields through one schema declaration`` () =
        let codec = Json.compile (optionalProfileSchema ())

        let profile =
            { Nickname = Some "Lady A"
              Name = "Ada"
              Age = None
              Ratings = [ Some 1; None; Some 3 ] }

        test <@ Json.deserialize codec (Json.serialize codec profile) = profile @>

    [<Fact>]
    let ``still requires non-optional fields when optional fields are absent`` () =
        let codec = Json.compile (optionalProfileSchema ())

        raisesWith<JsonCodecException>
            <@ Json.deserialize codec "{\"ratings\":[]}" @>
            (fun ex -> <@ ex.Message.Contains "name" @>)

    [<Fact>]
    let ``round trips union-inline payments with spliced fields beside the discriminator`` () =
        let codec = Json.compile (inlineOrderSchema ())

        let cardOrder: InlineOrder =
            { Reference = "ord-1"
              Payment = Card { Number = "4111"; Expiry = "12/28" } }

        let json = Json.serialize codec cardOrder
        test <@ json.Contains "\"payment\":{\"type\":\"card\",\"number\":\"4111\",\"expiry\":\"12/28\"}" @>
        test <@ Json.deserialize codec json = cardOrder @>

        let invoiceOrder: InlineOrder =
            { Reference = "ord-2"
              Payment = Invoice { Reference = "inv-42" } }

        test <@ Json.deserialize codec (Json.serialize codec invoiceOrder) = invoiceOrder @>

        let discriminatorLast =
            """{"reference":"ord-1","payment":{"number":"4111","expiry":"12/28","type":"card"}}"""

        test <@ Json.deserialize codec discriminatorLast = cardOrder @>

    [<Fact>]
    let ``round trips bare-string enum values`` () =
        let codec = Json.compile (swatchSchema ())

        let swatch = { Color = Green }
        let json = Json.serialize codec swatch

        test <@ json = "{\"color\":\"green\"}" @>
        test <@ Json.deserialize codec json = swatch @>
