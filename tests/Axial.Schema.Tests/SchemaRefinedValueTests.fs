namespace Axial.Tests

open System
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>
/// Proves that <c>Schema.convert</c> describes a named refined/domain value as a portable
/// <c>ValueSchema&lt;'value&gt;</c>: it retains the raw value schema as inspectable metadata, it round-trips a value
/// through the supplied construction and inspection functions, it supports refinement over every primitive value
/// schema — especially text — while keeping the primitive foundation and raw constraints inspectable, and the result
/// composes with the rest of the schema core (constraints and constructor-last shapes) exactly like a
/// primitive value schema.
/// </summary>
module SchemaRefinedValueTests =
    /// <summary>A minimal named refined/domain type standing in for a real <c>Email</c>-style value.</summary>
    type private Email =
        private
        | Email of string

        member this.Value =
            let (Email value) = this
            value

    module private Email =
        let create (value: string) = Email value
        let value (email: Email) = email.Value

        let schema : Schema<Email> = Schema.convert create value Schema.text

    /// <summary>A bounded-text domain value whose raw text schema carries its own constraint metadata.</summary>
    type private ContactName = private ContactName of string

    module private ContactName =
        let create (value: string) = ContactName value
        let value (ContactName value) = value

        let schema : Schema<ContactName> =
            Schema.text
            |> Schema.constrainAll [ Constraint.minLength 2; Constraint.maxLength 40 ]
            |> Schema.convert create value

    /// <summary>An email address refined a second time over the already refined <c>Email</c> schema.</summary>
    type private NormalizedEmail = private NormalizedEmail of Email

    module private NormalizedEmail =
        let create (email: Email) = NormalizedEmail email
        let value (NormalizedEmail email) = email

        let schema : Schema<NormalizedEmail> = Schema.convert create value Email.schema

    type private Age = private Age of int

    module private Age =
        let create (value: int) = Age value
        let value (Age value) = value

        let schema : Schema<Age> = Schema.convert create value Schema.int

    type private Contact = { Email: Email; Name: string }

    [<Fact>]
    let ``parse interprets a refined schema at the root`` () =
        let parsed = Schema.parseRetainingInput Email.schema (RawInput.Scalar "ada@example.com")
        test <@ parsed.Result = Ok(Email.create "ada@example.com") @>

    [<Fact>]
    let ``fallible refinement failures become root diagnostics`` () =
        let schema =
            Schema.text
            |> Schema.refine
                (fun _ -> Error "email.blocked")
                (fun code -> [ SchemaError.Custom(code, Some "This address is blocked.") ])
                Email.value

        let parsed = Schema.parseRetainingInput schema (RawInput.Scalar "ada@example.com")

        match parsed.Result with
        | Ok _ -> failwith "Expected refinement to reject the value."
        | Error diagnostics ->
            let errors = diagnostics |> Axial.Validation.Diagnostics.flatten
            test <@ errors |> List.map _.Path = [ [] ] @>
            test <@ errors |> List.map _.Error = [ SchemaError.Custom("email.blocked", Some "This address is blocked.") ] @>

    [<Fact>]
    let ``refined retains the raw value schema as inspectable metadata`` () =
        match Email.schema.ValueDefinition.Shape with
        | RefinedValueDefinition(raw, _) -> test <@ raw = Schema.text.ValueDefinition @>
        | _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``refined construct and inspect round-trip through the stored operations`` () =
        match Email.schema.ValueDefinition.Shape with
        | RefinedValueDefinition(_, ops) ->
            let constructed = ops.Construct(box "ada@example.com") |> Result.map unbox<Email>
            test <@ constructed = Ok(Email.create "ada@example.com") @>
            test <@ ops.Inspect(box (Result.defaultValue (Email.create "fallback@example.com") constructed)) |> unbox<string> = "ada@example.com" @>
        | _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``refined value schemas start with no constraints and accept constraints like any value schema`` () =
        test <@ Schema.constraints Email.schema = [] @>

        let required = Email.schema |> Schema.constrain Constraint.required
        test <@ Schema.constraints required |> List.map Constraint.code = [ "required" ] @>

    [<Fact>]
    let ``refined value schemas compose with an object shape like a primitive value schema`` () =
        let emailField =
            Field.create "email" (fun (contact: Contact) -> contact.Email) Email.schema
            |> Field.withConstraint Constraint.required

        let nameField = Field.create "name" (fun (contact: Contact) -> contact.Name) Schema.text

        let schema =
            Schema.define<Contact>
            |> fieldWith (Email.schema |> Schema.constrainAll [ Constraint.required ]) "email" _.Email
            |> fieldWith Schema.text "name" _.Name
            |> construct (fun email name -> { Email = email; Name = name })

        let contact = { Email = Email.create "ada@example.com"; Name = "Ada" }

        test <@ Field.getValue emailField contact = Email.create "ada@example.com" @>
        test <@ Field.getValue nameField contact = "Ada" @>

        match schema.Definition with
        | ModelDefinition model ->
            let email = model.Fields |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "email")
            test <@ email.ValueSchema.Constraints |> List.map Constraint.code = [ "required" ] @>
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``model schemas can attach required to a refined field's value schema, matching field "email" _.Email Email.schema { required }`` () =
        let requiredEmail = Email.schema |> Schema.constrain Constraint.required

        let schema =
            Schema.define<Contact>
            |> fieldWith requiredEmail "email" _.Email
            |> fieldWith Schema.text "name" _.Name
            |> construct (fun email name -> { Email = email; Name = name })

        match schema.Definition with
        | ModelDefinition model ->
            let email = model.Fields |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "email")
            test <@ email.ValueSchema.Constraints |> List.map Constraint.code = [ "required" ] @>

            match email.ValueSchema.Shape with
            | RefinedValueDefinition _ -> ()
            | _ -> failwith "Expected the email field to keep its refined value schema shape."

            let contact = { Email = Email.create "ada@example.com"; Name = "Ada" }
            test <@ ConstructorApplication.apply model.Constructor [| box contact.Email; box contact.Name |] = contact @>
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``refined value schemas can layer over every primitive value schema`` () =
        let kinds =
            [ Schema.convert Email.create Email.value Schema.text |> Schema.underlyingPrimitiveKind
              Schema.convert Age.create Age.value Schema.int |> Schema.underlyingPrimitiveKind
              Schema.convert (fun (value: decimal) -> value) id Schema.decimal |> Schema.underlyingPrimitiveKind
              Schema.convert (fun (value: bool) -> value) id Schema.bool |> Schema.underlyingPrimitiveKind
              Schema.convert (fun (value: DateOnly) -> value) id Schema.date |> Schema.underlyingPrimitiveKind
              Schema.convert (fun (value: DateTimeOffset) -> value) id Schema.dateTime |> Schema.underlyingPrimitiveKind
              Schema.convert (fun (value: Guid) -> value) id Schema.guid |> Schema.underlyingPrimitiveKind ]

        test <@
            kinds =
                [ PrimitiveValueKind.Text
                  PrimitiveValueKind.Int
                  PrimitiveValueKind.Decimal
                  PrimitiveValueKind.Bool
                  PrimitiveValueKind.Date
                  PrimitiveValueKind.DateTime
                  PrimitiveValueKind.Guid ]
        @>

    [<Fact>]
    let ``refined value schemas over non-text primitives round-trip like text-based ones`` () =
        match Age.schema.ValueDefinition.Shape with
        | RefinedValueDefinition(raw, ops) ->
            test <@ raw = Schema.int.ValueDefinition @>
            let constructed = ops.Construct(box 42) |> Result.map unbox<Age>
            test <@ constructed = Ok(Age.create 42) @>
            test <@ ops.Inspect(box (Result.defaultValue (Age.create 0) constructed)) |> unbox<int> = 42 @>
        | _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``isRefined distinguishes refined value schemas from primitive value schemas`` () =
        test <@ Schema.isRefined Email.schema @>
        test <@ Schema.isRefined Age.schema @>
        test <@ not (Schema.isRefined Schema.text) @>
        test <@ not (Schema.isRefined Schema.int) @>

    [<Fact>]
    let ``underlyingPrimitiveKind matches primitiveKind for primitive value schemas`` () =
        test <@ Schema.underlyingPrimitiveKind Schema.text = Schema.primitiveKind Schema.text @>
        test <@ Schema.underlyingPrimitiveKind Schema.int = Schema.primitiveKind Schema.int @>
        test <@ Schema.underlyingPrimitiveKind Schema.guid = Schema.primitiveKind Schema.guid @>

    [<Fact>]
    let ``layered refined value schemas bottom out on their primitive foundation`` () =
        test <@ Schema.isRefined NormalizedEmail.schema @>
        test <@ Schema.underlyingPrimitiveKind NormalizedEmail.schema = PrimitiveValueKind.Text @>

        match NormalizedEmail.schema.ValueDefinition.Shape with
        | RefinedValueDefinition(raw, ops) ->
            test <@ raw = Email.schema.ValueDefinition @>
            let constructed = ops.Construct(box (Email.create "ada@example.com")) |> Result.map unbox<NormalizedEmail>
            test <@ constructed = Ok(NormalizedEmail.create (Email.create "ada@example.com")) @>
        | _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``refined text value schemas keep raw text constraint metadata inspectable`` () =
        test <@ Schema.rawConstraints ContactName.schema |> List.map Constraint.code = [ "minLength"; "maxLength" ] @>
        test <@ Schema.underlyingPrimitiveKind ContactName.schema = PrimitiveValueKind.Text @>

        // Constraints attached to the refined schema itself stay separate from the raw schema's constraints.
        test <@ Schema.constraints ContactName.schema = [] @>

        let required = ContactName.schema |> Schema.constrain Constraint.required
        test <@ Schema.constraints required |> List.map Constraint.code = [ "required" ] @>
        test <@ Schema.rawConstraints required |> List.map Constraint.code = [ "minLength"; "maxLength" ] @>

    [<Fact>]
    let ``rawConstraints raises for primitive value schemas`` () =
        raises<ArgumentException> <@ Schema.rawConstraints Schema.text |> ignore @>

    [<Fact>]
    let ``refined inspection helpers raise for null schemas`` () =
        raises<ArgumentNullException> <@ Schema.isRefined Unchecked.defaultof<Schema<Email>> |> ignore @>
        raises<ArgumentNullException> <@ Schema.underlyingPrimitiveKind Unchecked.defaultof<Schema<Email>> |> ignore @>
        raises<ArgumentNullException> <@ Schema.rawConstraints Unchecked.defaultof<Schema<Email>> |> ignore @>

    [<Fact>]
    let ``refined raises for null construct, inspect, or raw schema`` () =
        raises<ArgumentNullException> <@ Schema.convert Unchecked.defaultof<string -> Email> Email.value Schema.text |> ignore @>
        raises<ArgumentNullException> <@ Schema.convert Email.create Unchecked.defaultof<Email -> string> Schema.text |> ignore @>
        raises<ArgumentNullException> <@ Schema.convert Email.create Email.value Unchecked.defaultof<Schema<string>> |> ignore @>

    [<Fact>]
    let ``every refined value schema carries both construction and inspection operations`` () =
        match Email.schema.ValueDefinition.Shape with
        | RefinedValueDefinition(_, ops) ->
            test <@ not (isNull (box ops.Construct)) @>
            test <@ not (isNull (box ops.Inspect)) @>
        | _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``refined value operations reject a missing construction or inspection function`` () =
        raises<ArgumentNullException> <@ RefinedValueOps(Unchecked.defaultof<obj -> Result<obj, SchemaError list>>, id) |> ignore @>
        raises<ArgumentNullException> <@ RefinedValueOps(Ok, Unchecked.defaultof<obj -> obj>) |> ignore @>
