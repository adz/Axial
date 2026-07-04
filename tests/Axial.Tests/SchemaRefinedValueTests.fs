namespace Axial.Tests

open System
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that <c>Value.refined</c> describes a named refined/domain value as a portable
/// <c>ValueSchema&lt;'value&gt;</c>: it retains the raw value schema as inspectable metadata, it round-trips a value
/// through the supplied construction and inspection functions, it supports refinement over every primitive value
/// schema — especially text — while keeping the primitive foundation and raw constraints inspectable, and the result
/// composes with the rest of the schema core (constraints and the progressive schema builder) exactly like a
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

        let schema : ValueSchema<Email> = Value.refined create value Value.text

    /// <summary>A bounded-text domain value whose raw text schema carries its own constraint metadata.</summary>
    type private ContactName = private ContactName of string

    module private ContactName =
        let create (value: string) = ContactName value
        let value (ContactName value) = value

        let schema : ValueSchema<ContactName> =
            Value.text
            |> Value.withConstraints [ SchemaConstraint.minLength 2; SchemaConstraint.maxLength 40 ]
            |> Value.refined create value

    /// <summary>An email address refined a second time over the already refined <c>Email</c> schema.</summary>
    type private NormalizedEmail = private NormalizedEmail of Email

    module private NormalizedEmail =
        let create (email: Email) = NormalizedEmail email
        let value (NormalizedEmail email) = email

        let schema : ValueSchema<NormalizedEmail> = Value.refined create value Email.schema

    type private Age = private Age of int

    module private Age =
        let create (value: int) = Age value
        let value (Age value) = value

        let schema : ValueSchema<Age> = Value.refined create value Value.``int``

    type private Contact = { Email: Email; Name: string }

    [<Fact>]
    let ``refined retains the raw value schema as inspectable metadata`` () =
        match Email.schema.Definition.Shape with
        | RefinedValueDefinition(raw, _) -> test <@ raw = Value.text.Definition @>
        | PrimitiveValueDefinition _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``refined construct and inspect round-trip through the stored operations`` () =
        match Email.schema.Definition.Shape with
        | RefinedValueDefinition(_, ops) ->
            let constructed = ops.Construct(box "ada@example.com") |> unbox<Email>
            test <@ constructed = Email.create "ada@example.com" @>
            test <@ ops.Inspect(box constructed) |> unbox<string> = "ada@example.com" @>
        | PrimitiveValueDefinition _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``refined value schemas start with no constraints and accept constraints like any value schema`` () =
        test <@ Value.constraints Email.schema = [] @>

        let required = Email.schema |> Value.withConstraint SchemaConstraint.required
        test <@ Value.constraints required |> List.map SchemaConstraint.code = [ "required" ] @>

    [<Fact>]
    let ``refined value schemas compose with the schema builder like a primitive value schema`` () =
        let emailField =
            Field.create "email" (fun (contact: Contact) -> contact.Email) Email.schema
            |> Field.withConstraint SchemaConstraint.required

        let nameField = Field.create "name" (fun (contact: Contact) -> contact.Name) Value.text

        let schema =
            Schema.recordFor<Contact, _> (fun email name -> { Email = email; Name = name })
            |> Schema.fieldWith [ SchemaConstraint.required ] "email" _.Email Email.schema
            |> Schema.text "name" _.Name
            |> Schema.build

        let contact = { Email = Email.create "ada@example.com"; Name = "Ada" }

        test <@ Field.getValue emailField contact = Email.create "ada@example.com" @>
        test <@ Field.getValue nameField contact = "Ada" @>

        match schema.Definition with
        | ModelDefinition model ->
            let email = model.Fields |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "email")
            test <@ email.Constraints |> List.map SchemaConstraint.code = [ "required" ] @>
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``refined value schemas can layer over every primitive value schema`` () =
        let kinds =
            [ Value.refined Email.create Email.value Value.text |> Value.underlyingPrimitiveKind
              Value.refined Age.create Age.value Value.``int`` |> Value.underlyingPrimitiveKind
              Value.refined (fun (value: decimal) -> value) id Value.``decimal`` |> Value.underlyingPrimitiveKind
              Value.refined (fun (value: bool) -> value) id Value.``bool`` |> Value.underlyingPrimitiveKind
              Value.refined (fun (value: DateOnly) -> value) id Value.date |> Value.underlyingPrimitiveKind
              Value.refined (fun (value: DateTimeOffset) -> value) id Value.dateTime |> Value.underlyingPrimitiveKind
              Value.refined (fun (value: Guid) -> value) id Value.guid |> Value.underlyingPrimitiveKind ]

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
        match Age.schema.Definition.Shape with
        | RefinedValueDefinition(raw, ops) ->
            test <@ raw = Value.``int``.Definition @>
            let constructed = ops.Construct(box 42) |> unbox<Age>
            test <@ constructed = Age.create 42 @>
            test <@ ops.Inspect(box constructed) |> unbox<int> = 42 @>
        | PrimitiveValueDefinition _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``isRefined distinguishes refined value schemas from primitive value schemas`` () =
        test <@ Value.isRefined Email.schema @>
        test <@ Value.isRefined Age.schema @>
        test <@ not (Value.isRefined Value.text) @>
        test <@ not (Value.isRefined Value.``int``) @>

    [<Fact>]
    let ``underlyingPrimitiveKind matches primitiveKind for primitive value schemas`` () =
        test <@ Value.underlyingPrimitiveKind Value.text = Value.primitiveKind Value.text @>
        test <@ Value.underlyingPrimitiveKind Value.``int`` = Value.primitiveKind Value.``int`` @>
        test <@ Value.underlyingPrimitiveKind Value.guid = Value.primitiveKind Value.guid @>

    [<Fact>]
    let ``layered refined value schemas bottom out on their primitive foundation`` () =
        test <@ Value.isRefined NormalizedEmail.schema @>
        test <@ Value.underlyingPrimitiveKind NormalizedEmail.schema = PrimitiveValueKind.Text @>

        match NormalizedEmail.schema.Definition.Shape with
        | RefinedValueDefinition(raw, ops) ->
            test <@ raw = Email.schema.Definition @>
            let constructed = ops.Construct(box (Email.create "ada@example.com")) |> unbox<NormalizedEmail>
            test <@ constructed = NormalizedEmail.create (Email.create "ada@example.com") @>
        | PrimitiveValueDefinition _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``refined text value schemas keep raw text constraint metadata inspectable`` () =
        test <@ Value.rawConstraints ContactName.schema |> List.map SchemaConstraint.code = [ "minLength"; "maxLength" ] @>
        test <@ Value.underlyingPrimitiveKind ContactName.schema = PrimitiveValueKind.Text @>

        // Constraints attached to the refined schema itself stay separate from the raw schema's constraints.
        test <@ Value.constraints ContactName.schema = [] @>

        let required = ContactName.schema |> Value.withConstraint SchemaConstraint.required
        test <@ Value.constraints required |> List.map SchemaConstraint.code = [ "required" ] @>
        test <@ Value.rawConstraints required |> List.map SchemaConstraint.code = [ "minLength"; "maxLength" ] @>

    [<Fact>]
    let ``rawConstraints raises for primitive value schemas`` () =
        raises<ArgumentException> <@ Value.rawConstraints Value.text |> ignore @>

    [<Fact>]
    let ``refined inspection helpers raise for null schemas`` () =
        raises<ArgumentNullException> <@ Value.isRefined Unchecked.defaultof<ValueSchema<Email>> |> ignore @>
        raises<ArgumentNullException> <@ Value.underlyingPrimitiveKind Unchecked.defaultof<ValueSchema<Email>> |> ignore @>
        raises<ArgumentNullException> <@ Value.rawConstraints Unchecked.defaultof<ValueSchema<Email>> |> ignore @>

    [<Fact>]
    let ``refined raises for null construct, inspect, or raw schema`` () =
        raises<ArgumentNullException> <@ Value.refined Unchecked.defaultof<string -> Email> Email.value Value.text |> ignore @>
        raises<ArgumentNullException> <@ Value.refined Email.create Unchecked.defaultof<Email -> string> Value.text |> ignore @>
        raises<ArgumentNullException> <@ Value.refined Email.create Email.value Unchecked.defaultof<ValueSchema<string>> |> ignore @>

    [<Fact>]
    let ``every refined value schema carries both construction and inspection operations`` () =
        match Email.schema.Definition.Shape with
        | RefinedValueDefinition(_, ops) ->
            test <@ not (isNull (box ops.Construct)) @>
            test <@ not (isNull (box ops.Inspect)) @>
        | PrimitiveValueDefinition _ -> failwith "Expected a refined value schema shape."

    [<Fact>]
    let ``refined value operations reject a missing construction or inspection function`` () =
        raises<ArgumentNullException> <@ RefinedValueOps(Unchecked.defaultof<obj -> obj>, id) |> ignore @>
        raises<ArgumentNullException> <@ RefinedValueOps(id, Unchecked.defaultof<obj -> obj>) |> ignore @>
