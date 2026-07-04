namespace Axial.Tests

open System
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that <c>Value.refined</c> describes a named refined/domain value as a portable
/// <c>ValueSchema&lt;'value&gt;</c>: it retains the raw value schema as inspectable metadata, it round-trips a value
/// through the supplied construction and inspection functions, and the result composes with the rest of the schema
/// core (constraints and the progressive schema builder) exactly like a primitive value schema.
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
            Schema.record (fun email name -> { Email = email; Name = name })
            |> Schema.fieldWith [ SchemaConstraint.required ] "email" (fun (contact: Contact) -> contact.Email) Email.schema
            |> Schema.field "name" (fun (contact: Contact) -> contact.Name) Value.text
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
