namespace Axial.Tests

open Axial

open System
open Axial.Schema
open Swensen.Unquote
open Xunit

/// Specs for the schema computation expression and Schema.admit over a draft schema.
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
        schema<Person> {
            field "firstName" _.FirstName {
                constrain (Syntax.minLength 1)
            }
            field "lastName" _.LastName {
                constrain (Syntax.minLength 1)
            }
            field "birthDate" _.BirthDate
            construct Person.Create
        }

    [<Fact>]
    let ``the target handwritten syntax parses valid input`` () =
        let input =
            Data.ofMap (
                Map.ofList
                    [ "firstName", "Ada"
                      "lastName", "Lovelace"
                      "birthDate", "1815-12-10T00:00:00Z" ]
            )

        match (Schema.parse personSchema input) with
        | Ok person ->
            test <@ person.FirstName = "Ada" @>
            test <@ person.LastName = "Lovelace" @>
            test <@ person.BirthDate.Year = 1815 @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``a constraint attaches to the field it follows`` () =
        let input =
            Data.ofMap (
                Map.ofList
                    [ "firstName", ""
                      "lastName", "Lovelace"
                      "birthDate", "1815-12-10T00:00:00Z" ]
            )

        match (Schema.parse personSchema input) with
        | Ok person -> failwithf "Expected a minLength diagnostic, parsed %A" person
        | Error errors ->
            let flattened = SchemaErrors.toList errors
            test <@ flattened |> List.exists (fun diagnostic -> diagnostic.Path = TestPath.fromLegacy [ PathSegment.Name "firstName" ]) @>

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
        schema<Range> {
            field "low" (fun (range: Range) -> fst range.Bounds)
            field "high" (fun (range: Range) -> snd range.Bounds)
            constructResult Range.Create
        }

    [<Fact>]
    let ``constructResult admits values the constructor accepts`` () =
        let input = Data.ofMap (Map.ofList [ "low", "1"; "high", "5" ])

        match (Schema.parse rangeSchema input) with
        | Ok range -> test <@ range.Bounds = (1, 5) @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``constructResult reports constructor rejections as diagnostics`` () =
        let input = Data.ofMap (Map.ofList [ "low", "9"; "high", "5" ])

        match (Schema.parse rangeSchema input) with
        | Ok range -> failwithf "Expected a constructor rejection, parsed %A" range
        | Error errors ->
            let flattened = SchemaErrors.toList errors
            test <@ flattened |> List.exists (fun diagnostic -> diagnostic.Error = SchemaError.ConstructorFailed "high must not precede low") @>

    // ---- fieldWith and inferred containers ----

    type private Tagged =
        { Name: string
          Tags: string list
          Note: string option }

        static member Create name tags note = { Name = name; Tags = tags; Note = note }

    let private taggedSchema =
        schema<Tagged> {
            field "name" _.Name
            field "tags" _.Tags {
                constrain (Syntax.minCount 1)
            }
            field "note" _.Note
            construct Tagged.Create
        }

    [<Fact>]
    let ``option and list fields infer their schemas`` () =
        let input =
            (
                Data.objectOfMap (Map.ofList
                        [ "name", Data.Text "axial"
                          "tags", Data.List [ Data.Text "fsharp" ] ]
                )
            )

        match (Schema.parse taggedSchema input) with
        | Ok tagged ->
            test <@ tagged.Tags = [ "fsharp" ] @>
            test <@ tagged.Note = None @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    type Email =
        private
        | Email of string

        member this.Value = let (Email value) = this in value
        static member Schema(_: Email) = Schema.convert Email _.Value Schema.text

    type ContactBook =
        { Emails: Email list
          Contacts: Map<string, Email>
          Preferred: Email option }

        static member Create emails contacts preferred =
            { Emails = emails
              Contacts = contacts
              Preferred = preferred }

    [<Fact>]
    let ``field recursively infers domain schemas through collections and options`` () =
        let schema =
            schema<ContactBook> {
                field "emails" _.Emails
                field "contacts" _.Contacts
                field "preferred" _.Preferred
                construct ContactBook.Create
            }

        let input =
            (
                Data.objectOfMap (Map.ofList
                        [ "emails", Data.List [ Data.Text "ada@example.com" ]
                          "contacts", Data.objectOfMap (Map.ofList [ "primary", Data.Text "ada@example.com" ]) ]
                )
            )

        match (Schema.parse schema input) with
        | Ok contactBook ->
            test <@ contactBook.Emails |> List.map _.Value = [ "ada@example.com" ] @>
            test <@ contactBook.Preferred = None @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``type-directed list and map schemas resolve their member schema`` () =
        let emails = Schema.list<Email>()
        let contacts = Schema.map<Email>()
        let emailInput = (Data.List [ Data.Text "ada@example.com" ])
        let contactInput = (Data.objectOfMap (Map.ofList [ "primary", Data.Text "ada@example.com" ]))

        test <@ (Schema.parse emails emailInput) |> Result.isOk @>
        test <@ (Schema.parse contacts contactInput) |> Result.isOk @>

    [<Fact>]
    let ``nested constraints apply to list items and map values`` () =
        let names = Schema.list<string>() |> Syntax.constrainItems (Syntax.minLength 2)
        let labels = Schema.map<string>() |> Syntax.constrainValues (Syntax.minLength 2)
        let nameInput = (Data.List [ Data.Text "x" ])
        let labelInput = (Data.objectOfMap (Map.ofList [ "short", Data.Text "x" ]))

        test <@ (Schema.parse names nameInput) |> Result.isError @>
        test <@ (Schema.parse labels labelInput) |> Result.isError @>

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
        schema<BookingDraft> {
            field "start" (fun (value: BookingDraft) -> value.Start)
            field "end" (fun (value: BookingDraft) -> value.End)
            construct BookingDraft.Create
        }

    let private bookingSchema =
        bookingDraftSchema |> Schema.admit Booking.Create _.ToDraft

    [<Fact>]
    let ``admit parses through the draft shape into the domain type`` () =
        let input =
            Data.ofMap (
                Map.ofList
                    [ "start", "2026-08-01T00:00:00Z"
                      "end", "2026-08-05T00:00:00Z" ]
            )

        match (Schema.parse bookingSchema input) with
        | Ok booking -> test <@ booking.Nights = 4 @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``admit reports admission failures as diagnostics`` () =
        let input =
            Data.ofMap (
                Map.ofList
                    [ "start", "2026-08-05T00:00:00Z"
                      "end", "2026-08-01T00:00:00Z" ]
            )

        match (Schema.parse bookingSchema input) with
        | Ok booking -> failwithf "Expected an admission failure, parsed %A" booking
        | Error errors ->
            let flattened = SchemaErrors.toList errors
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
