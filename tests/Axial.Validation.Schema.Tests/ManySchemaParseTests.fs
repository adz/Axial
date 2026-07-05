namespace Axial.Tests

open Axial.Schema
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module ManySchemaParseTests =
    type private ContactMethod = { Kind: string; Value: string }

    type private Customer = { Name: string; Contacts: ContactMethod list }

    let private contactMethodSchema =
        Schema.recordFor<ContactMethod, _> (fun kind value -> { Kind = kind; Value = value })
        |> Schema.field "kind" _.Kind (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.field "value" _.Value (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.build

    let private customerSchema =
        Schema.recordFor<Customer, _> (fun name contacts -> { Name = name; Contacts = contacts })
        |> Schema.field "name" _.Name (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.many "contacts" _.Contacts contactMethodSchema
        |> Schema.build

    let private validContact kind value =
        RawInput.Object(Map.ofList [ "kind", RawInput.Scalar kind; "value", RawInput.Scalar value ])

    [<Fact>]
    let ``parse builds a collection from collection-shaped raw input`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "contacts", RawInput.Many [ validContact "email" "ada@example.com" ] ]
            )

        let parsed = Input.parse customerSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Model = { Name = "Ada"; Contacts = [ { Kind = "email"; Value = "ada@example.com" } ] } @>

    [<Fact>]
    let ``parse builds an empty collection from an empty collection-shaped raw input`` () =
        let raw = RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Many [] ])

        let parsed = Input.parse customerSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Model = { Name = "Ada"; Contacts = [] } @>

    [<Fact>]
    let ``parse reports expected collection when the collection field raw input is object-shaped`` () =
        let raw =
            RawInput.Object(
                Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Object(Map.ofList [ "kind", RawInput.Scalar "email" ]) ]
            )

        let parsed = Input.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]; Error = SchemaError.ExpectedMany } ] @>

    [<Fact>]
    let ``parse reports expected collection when the collection field raw input is a scalar`` () =
        let raw =
            RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Scalar "not-a-collection" ])

        let parsed = Input.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]; Error = SchemaError.ExpectedMany } ] @>

    [<Fact>]
    let ``parse reports expected object for an item that is not object-shaped`` () =
        let raw =
            RawInput.Object(
                Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Many [ RawInput.Scalar "not-an-object" ] ]
            )

        let parsed = Input.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]; Error = SchemaError.ExpectedObject } ] @>
