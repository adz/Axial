namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves the <c>Axial.Schema.DSL</c> open surface builds the same schema metadata as the qualified pipeline, and
/// that the recommended module-scoped <c>open</c> pattern (including the deliberate shadowing of core <c>int</c> and
/// friends) compiles as documented.
/// </summary>
module SchemaDslTests =
    type private Signup =
        { Email: string
          Age: int
          Note: string }

    type private Address = { City: string }

    type private Contact = { Kind: string }

    type private Order =
        { Address: Address
          Contacts: Contact list
          Total: decimal }

    // The recommended usage pattern: DSL opened inside the module that defines the schema.
    module private SignupSchema =
        open Axial.Schema.DSL

        let schema () =
            recordFor<Signup, _> (fun email age note -> { Email = email; Age = age; Note = note })
            |> text [ required; email ] "email" _.Email
            |> int [ atLeast 13 ] "age" _.Age
            |> text [] "note" _.Note
            |> build

    module private OrderSchema =
        open Axial.Schema.DSL

        let citySchema () =
            recordFor<Address, _> (fun city -> { City = city })
            |> text [ required ] "city" _.City
            |> build

        let contactSchema () =
            recordFor<Contact, _> (fun kind -> { Kind = kind })
            |> text [ required ] "kind" _.Kind
            |> build

        let schema () =
            recordFor<Order, _> (fun address contacts total ->
                { Address = address
                  Contacts = contacts
                  Total = total })
            |> nested [ required ] "address" _.Address (citySchema ())
            |> many [ minCount 1 ] "contacts" _.Contacts (contactSchema ())
            |> decimal [ greaterThan 0 ] "total" _.Total
            |> build

    [<Fact>]
    let ``dsl pipeline produces the same field metadata as the qualified pipeline`` () =
        let qualified =
            Schema.recordFor<Signup, _> (fun email age note -> { Email = email; Age = age; Note = note })
            |> Schema.fieldWith [ SchemaConstraint.required; SchemaConstraint.email ] "email" _.Email Value.text
            |> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.``int``
            |> Schema.fieldWith [] "note" _.Note Value.text
            |> Schema.build

        let describe (schema: Schema<Signup>) =
            (Inspect.model schema).Fields
            |> List.map (fun field -> field.Name, field.Constraints |> List.map (fun c -> c.Code))

        test <@ describe (SignupSchema.schema ()) = describe qualified @>

    [<Fact>]
    let ``dsl nested, many, and decimal combinators carry their constraints and field order`` () =
        let description = Inspect.model (OrderSchema.schema ())

        test <@ description.Fields |> List.map _.Name = [ "address"; "contacts"; "total" ] @>

        let constraintCodes name =
            description.Fields
            |> List.find (fun field -> field.Name = name)
            |> _.Constraints
            |> List.map _.Code

        test <@ constraintCodes "address" = [ "required" ] @>
        test <@ constraintCodes "contacts" = [ "minCount" ] @>
        test <@ constraintCodes "total" = [ "greaterThan" ] @>
