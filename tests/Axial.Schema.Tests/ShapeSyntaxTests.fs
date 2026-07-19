namespace Axial.Tests

open System
open Axial.Schema
open Axial.Schema.Syntax
open Axial.Validation
open Swensen.Unquote
open Xunit

/// Specs for the constructor-last authoring surface: Schema.define + field/constrain pipelines closed by
/// construct/constructResult, and Schema.admit for trusted domain construction over a draft schema.
module ShapeSyntaxTests =

    type private Person =
        { FirstName: string
          LastName: string
          BirthDate: DateTimeOffset }

        static member Create firstName lastName birthDate =
            { FirstName = firstName
              LastName = lastName
              BirthDate = birthDate }

    let private personSchema =
        Schema.define<Person>
        |> field "firstName" _.FirstName
        |> constrain (minLength 1)
        |> field "lastName" _.LastName
        |> constrain (minLength 1)
        |> field "birthDate" _.BirthDate
        |> construct Person.Create

    [<Fact>]
    let ``the target handwritten syntax parses valid input`` () =
        let input =
            RawInput.ofMap (
                Map.ofList
                    [ "firstName", "Ada"
                      "lastName", "Lovelace"
                      "birthDate", "1815-12-10T00:00:00Z" ]
            )

        match (Schema.parse personSchema input).Result with
        | Ok person ->
            test <@ person.FirstName = "Ada" @>
            test <@ person.LastName = "Lovelace" @>
            test <@ person.BirthDate.Year = 1815 @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``a constraint attaches to the field it follows`` () =
        let input =
            RawInput.ofMap (
                Map.ofList
                    [ "firstName", ""
                      "lastName", "Lovelace"
                      "birthDate", "1815-12-10T00:00:00Z" ]
            )

        match (Schema.parse personSchema input).Result with
        | Ok person -> failwithf "Expected a minLength diagnostic, parsed %A" person
        | Error errors ->
            let flattened = Diagnostics.flatten errors
            test <@ flattened |> List.exists (fun diagnostic -> diagnostic.Path = [ PathSegment.Name "firstName" ]) @>

    [<Fact>]
    let ``check accepts a trusted draft through the same schema`` () =
        let draft =
            { FirstName = "Grace"
              LastName = "Hopper"
              BirthDate = DateTimeOffset(System.DateTime(1906, 12, 9), TimeSpan.Zero) }

        test <@ Schema.check personSchema draft = Ok draft @>

    // ---- constructResult: checked construction ----

    type private Range =
        private
            { Low: int
              High: int }

        static member Create low high =
            if low <= high then
                Ok { Low = low; High = high }
            else
                Error "high must not precede low"

        member this.Bounds = this.Low, this.High

    let private rangeSchema =
        Schema.define<Range>
        |> field "low" (fun range -> fst range.Bounds)
        |> field "high" (fun range -> snd range.Bounds)
        |> constructResult Range.Create

    [<Fact>]
    let ``constructResult admits values the constructor accepts`` () =
        let input = RawInput.ofMap (Map.ofList [ "low", "1"; "high", "5" ])

        match (Schema.parse rangeSchema input).Result with
        | Ok range -> test <@ range.Bounds = (1, 5) @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``constructResult reports constructor rejections as diagnostics`` () =
        let input = RawInput.ofMap (Map.ofList [ "low", "9"; "high", "5" ])

        match (Schema.parse rangeSchema input).Result with
        | Ok range -> failwithf "Expected a constructor rejection, parsed %A" range
        | Error errors ->
            let flattened = Diagnostics.flatten errors
            test <@ flattened |> List.exists (fun diagnostic -> diagnostic.Error = SchemaError.ConstructorFailed "high must not precede low") @>

    // ---- fieldWith and inferred containers ----

    type private Tagged =
        { Name: string
          Tags: string list
          Note: string option }

        static member Create name tags note = { Name = name; Tags = tags; Note = note }

    let private taggedSchema =
        Schema.define<Tagged>
        |> field "name" _.Name
        |> field "tags" _.Tags
        |> constrain (minCount 1)
        |> field "note" _.Note
        |> construct Tagged.Create

    [<Fact>]
    let ``option and list fields infer their schemas`` () =
        let input =
            RawInput.ofJsonLikeValue (
                JsonLikeValue.Object(
                    Map.ofList
                        [ "name", JsonLikeValue.String "axial"
                          "tags", JsonLikeValue.Array [ JsonLikeValue.String "fsharp" ] ]
                )
            )

        match (Schema.parse taggedSchema input).Result with
        | Ok tagged ->
            test <@ tagged.Tags = [ "fsharp" ] @>
            test <@ tagged.Note = None @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    type Email =
        private
        | Email of string

        member this.Value = let (Email value) = this in value
        static member DefaultSchema(_: Email) = Schema.convert Email _.Value Schema.text

    type ContactBook =
        { Emails: Email list
          Contacts: Map<string, Email> }

        static member Create emails contacts = { Emails = emails; Contacts = contacts }

    [<Fact>]
    let ``field recursively infers a domain item schema for lists`` () =
        let schema =
            Schema.define<ContactBook>
            |> field "emails" _.Emails
            |> field "contacts" _.Contacts
            |> construct ContactBook.Create

        let input =
            RawInput.ofJsonLikeValue (
                JsonLikeValue.Object(
                    Map.ofList
                        [ "emails", JsonLikeValue.Array [ JsonLikeValue.String "ada@example.com" ]
                          "contacts", JsonLikeValue.Object(Map.ofList [ "primary", JsonLikeValue.String "ada@example.com" ]) ]
                )
            )

        match (Schema.parse schema input).Result with
        | Ok contactBook -> test <@ contactBook.Emails |> List.map _.Value = [ "ada@example.com" ] @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``type-directed list and map schemas resolve their member schema`` () =
        let emails = Schema.list<Email>()
        let contacts = Schema.map<Email>()
        let emailInput = RawInput.ofJsonLikeValue (JsonLikeValue.Array [ JsonLikeValue.String "ada@example.com" ])
        let contactInput = RawInput.ofJsonLikeValue (JsonLikeValue.Object(Map.ofList [ "primary", JsonLikeValue.String "ada@example.com" ]))

        test <@ (Schema.parse emails emailInput).Result |> Result.isOk @>
        test <@ (Schema.parse contacts contactInput).Result |> Result.isOk @>

    [<Fact>]
    let ``nested constraints apply to list items and map values`` () =
        let names = Schema.list<string>() |> constrainItems (minLength 2)
        let labels = Schema.map<string>() |> constrainValues (minLength 2)
        let nameInput = RawInput.ofJsonLikeValue (JsonLikeValue.Array [ JsonLikeValue.String "x" ])
        let labelInput = RawInput.ofJsonLikeValue (JsonLikeValue.Object(Map.ofList [ "short", JsonLikeValue.String "x" ]))

        test <@ (Schema.parse names nameInput).Result |> Result.isError @>
        test <@ (Schema.parse labels labelInput).Result |> Result.isError @>

    // ---- Schema.admit: the trusted-construction boundary over a draft ----

    type private BookingDraft =
        { Start: DateTimeOffset
          End: DateTimeOffset }

        static member Create start finish = { Start = start; End = finish }

    type private Booking =
        private
            { Start: DateTimeOffset
              End: DateTimeOffset }

        static member Create(draft: BookingDraft) =
            if draft.Start <= draft.End then
                Ok { Start = draft.Start; End = draft.End }
            else
                Error "End must not precede start"

        member this.ToDraft: BookingDraft = { Start = this.Start; End = this.End }
        member this.Nights = (this.End - this.Start).Days

    let private bookingDraftSchema =
        Schema.define<BookingDraft>
        |> field "start" _.Start
        |> field "end" _.End
        |> construct BookingDraft.Create

    let private bookingSchema =
        bookingDraftSchema |> Schema.admit Booking.Create _.ToDraft

    [<Fact>]
    let ``admit parses through the draft shape into the domain type`` () =
        let input =
            RawInput.ofMap (
                Map.ofList
                    [ "start", "2026-08-01T00:00:00Z"
                      "end", "2026-08-05T00:00:00Z" ]
            )

        match (Schema.parse bookingSchema input).Result with
        | Ok booking -> test <@ booking.Nights = 4 @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``admit reports admission failures as diagnostics`` () =
        let input =
            RawInput.ofMap (
                Map.ofList
                    [ "start", "2026-08-05T00:00:00Z"
                      "end", "2026-08-01T00:00:00Z" ]
            )

        match (Schema.parse bookingSchema input).Result with
        | Ok booking -> failwithf "Expected an admission failure, parsed %A" booking
        | Error errors ->
            let flattened = Diagnostics.flatten errors
            test <@ flattened |> List.exists (fun diagnostic -> diagnostic.Error = SchemaError.ConstructorFailed "End must not precede start") @>

    [<Fact>]
    let ``admit preserves the draft's field shape for inspection`` () =
        let description = Inspect.model bookingSchema
        test <@ description.Fields |> List.map _.Name = [ "start"; "end" ] @>

    [<Fact>]
    let ``admit checks trusted domain values through the projection`` () =
        let booking =
            { BookingDraft.Start = DateTimeOffset(System.DateTime(2026, 8, 1), TimeSpan.Zero)
              End = DateTimeOffset(System.DateTime(2026, 8, 5), TimeSpan.Zero) }
            |> Booking.Create
            |> Result.defaultWith (fun message -> failwith message)

        test <@ Schema.check bookingSchema booking = Ok booking @>
