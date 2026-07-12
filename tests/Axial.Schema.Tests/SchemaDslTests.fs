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
            |> field "email" _.Email (text |> constrainAll [ required; email ])
            |> field "age" _.Age (int |> constrain (atLeast 13))
            |> field "note" _.Note text
            |> build

    module private OrderSchema =
        open Axial.Schema.DSL

        let citySchema () =
            recordFor<Address, _> (fun city -> { City = city })
            |> field "city" _.City (text |> constrain required)
            |> build

        let contactSchema () =
            recordFor<Contact, _> (fun kind -> { Kind = kind })
            |> field "kind" _.Kind (text |> constrain required)
            |> build

        let schema () =
            recordFor<Order, _> (fun address contacts total ->
                { Address = address
                  Contacts = contacts
                  Total = total })
            |> field "address" _.Address (citySchema () |> constrain required)
            |> field "contacts" _.Contacts (list (contactSchema ()) |> constrain (minCount 1))
            |> field "total" _.Total (decimal |> constrain (greaterThan 0m))
            |> build

    [<Fact>]
    let ``dsl pipeline produces the same field metadata as the qualified pipeline`` () =
        let qualified =
            Schema.recordFor<Signup, _> (fun email age note -> { Email = email; Age = age; Note = note })
            |> Schema.field "email" _.Email (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.email ])
            |> Schema.field "age" _.Age (Schema.``int`` |> Schema.constrainAll [ Constraint.atLeast 13 ])
            |> Schema.field "note" _.Note (Schema.text |> Schema.constrainAll [])
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
            |> _.Schema.Constraints
            |> List.map _.Code

        test <@ constraintCodes "address" = [ "required" ] @>
        test <@ constraintCodes "contacts" = [ "minCount" ] @>
        test <@ constraintCodes "total" = [ "greaterThan" ] @>
