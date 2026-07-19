namespace Axial.Tests

open Axial.ErrorHandling

open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module ManySchemaParseTests =
    type private ContactMethod = { Kind: string; Value: string }

    type private VerifiedContactMethod =
        private
            { Kind: string
              Value: string }

        static member Create kind value =
            if kind <> value then
                Ok { Kind = kind; Value = value }
            else
                Error "Kind and value must differ."

    type private Customer = { Name: string; Contacts: ContactMethod list }

    type private VerifiedCustomer = { Name: string; Contacts: VerifiedContactMethod list }

    type private Tags = { Values: string list }

    let private contactMethodSchema =
        Schema.define<ContactMethod>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "kind" (fun contact -> contact.Kind)
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "value" (fun contact -> contact.Value)
        |> construct (fun kind value -> ({ Kind = kind; Value = value }: ContactMethod))

    let private verifiedContactMethodSchema =
        Schema.define<VerifiedContactMethod>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "kind" (fun contact -> contact.Kind)
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "value" (fun contact -> contact.Value)
        |> constructResult VerifiedContactMethod.Create

    let private customerSchema =
        Schema.define<Customer>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "name" (fun customer -> customer.Name)
        |> fieldWith (Schema.listWith contactMethodSchema) "contacts" (fun customer -> customer.Contacts)
        |> construct (fun name contacts -> ({ Name = name; Contacts = contacts }: Customer))

    let private verifiedCustomerSchema =
        Schema.define<VerifiedCustomer>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "name" (fun customer -> customer.Name)
        |> fieldWith (Schema.listWith verifiedContactMethodSchema) "contacts" (fun customer -> customer.Contacts)
        |> construct (fun name contacts -> ({ Name = name; Contacts = contacts }: VerifiedCustomer))

    let private constrainedCustomerSchema =
        Schema.define<Customer>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "name" (fun customer -> customer.Name)
        |> fieldWith (Schema.listWith contactMethodSchema |> Schema.constrainAll [ Constraint.minCount 1; Constraint.maxCount 2 ]) "contacts" (fun customer -> customer.Contacts)
        |> construct (fun name contacts -> ({ Name = name; Contacts = contacts }: Customer))

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

        let parsed = Schema.parse customerSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Value = { Name = "Ada"; Contacts = [ { Kind = "email"; Value = "ada@example.com" } ] } @>

    [<Fact>]
    let ``parse builds an empty collection from an empty collection-shaped raw input`` () =
        let raw = RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Many [] ])

        let parsed = Schema.parse customerSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Value = { Name = "Ada"; Contacts = [] } @>

    [<Fact>]
    let ``parse accepts a collection whose item count satisfies field constraints`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "contacts",
                      RawInput.Many
                          [ validContact "email" "ada@example.com"
                            validContact "phone" "+61 400 000 000" ] ]
            )

        let parsed = Schema.parse constrainedCustomerSchema raw

        test <@ parsed.IsValid @>
        test
            <@
                parsed.Value =
                    { Name = "Ada"
                      Contacts =
                        [ { Kind = "email"; Value = "ada@example.com" }
                          { Kind = "phone"; Value = "+61 400 000 000" } ] }
            @>

    [<Fact>]
    let ``parse reports min count constraint failures at the collection field path`` () =
        let raw = RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Many [] ])

        let parsed = Schema.parse constrainedCustomerSchema raw

        test <@ not parsed.IsValid @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]
                                    Error = SchemaError.InvalidCount(CheckCountExpectation.MinimumCount 1, Some 0) } ]
            @>

    [<Fact>]
    let ``parse reports max count constraint failures at the collection field path`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "contacts",
                      RawInput.Many
                          [ validContact "email" "ada@example.com"
                            validContact "phone" "+61 400 000 000"
                            validContact "sms" "+61 400 000 000" ] ]
            )

        let parsed = Schema.parse constrainedCustomerSchema raw

        test <@ not parsed.IsValid @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]
                                    Error = SchemaError.InvalidCount(CheckCountExpectation.MaximumCount 2, Some 3) } ]
            @>

    [<Fact>]
    let ``parse reports expected collection when the collection field raw input is object-shaped`` () =
        let raw =
            RawInput.Object(
                Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Object(Map.ofList [ "kind", RawInput.Scalar "email" ]) ]
            )

        let parsed = Schema.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]; Error = SchemaError.ExpectedMany } ] @>

    [<Fact>]
    let ``parse reports expected collection when the collection field raw input is a scalar`` () =
        let raw =
            RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Scalar "not-a-collection" ])

        let parsed = Schema.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]; Error = SchemaError.ExpectedMany } ] @>

    [<Fact>]
    let ``parse reports expected object for an item that is not object-shaped`` () =
        let raw =
            RawInput.Object(
                Map.ofList [ "name", RawInput.Scalar "Ada"; "contacts", RawInput.Many [ RawInput.Scalar "not-an-object" ] ]
            )

        let parsed = Schema.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "contacts"; PathSegment.Index 0 ]
                                    Error = SchemaError.ExpectedObject } ]
            @>

    [<Fact>]
    let ``parse builds a collection from primitive item schemas`` () =
        let schema =
            Schema.define<Tags>
            |> fieldWith (Schema.listWith (Schema.text |> Schema.constrain Constraint.required)) "values" _.Values
            |> construct (fun values -> { Values = values })

        let raw =
            RawInput.Object(Map.ofList [ "values", RawInput.Many [ RawInput.Scalar "fsharp"; RawInput.Scalar "typed-errors" ] ])

        let parsed = Schema.parse schema raw

        test <@ parsed.Result = Ok { Values = [ "fsharp"; "typed-errors" ] } @>

    [<Fact>]
    let ``parse reports primitive item failures at collection index paths`` () =
        let schema =
            Schema.define<Tags>
            |> fieldWith (Schema.listWith (Schema.text |> Schema.constrain Constraint.required)) "values" _.Values
            |> construct (fun values -> { Values = values })

        let raw =
            RawInput.Object(Map.ofList [ "values", RawInput.Many [ RawInput.Scalar "fsharp"; RawInput.Scalar "   " ] ])

        let parsed = Schema.parse schema raw

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "values"; PathSegment.Index 1 ]
                                   Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse accumulates errors from every failing item instead of stopping at the first`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "contacts",
                      RawInput.Many
                          [ RawInput.Object(Map.ofList [ "kind", RawInput.Scalar ""; "value", RawInput.Scalar "ada@example.com" ])
                            RawInput.Object(Map.ofList [ "kind", RawInput.Scalar "email"; "value", RawInput.Scalar "" ]) ] ]
            )

        let parsed = Schema.parse customerSchema raw

        test <@ not parsed.IsValid @>

        test
            <@
                parsed.Errors
                |> List.sortBy (fun diagnostic -> diagnostic.Path)
                |> (=)
                    [ { Path = [ PathSegment.Name "contacts"; PathSegment.Index 0; PathSegment.Name "kind" ]
                        Error = SchemaError.Required }
                      { Path = [ PathSegment.Name "contacts"; PathSegment.Index 1; PathSegment.Name "value" ]
                        Error = SchemaError.Required } ]
            @>

    [<Fact>]
    let ``parse attaches collection item constructor errors to the item root by default`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "contacts",
                      RawInput.Many
                          [ validContact "email" "ada@example.com"
                            RawInput.Object(Map.ofList [ "kind", RawInput.Scalar "same"; "value", RawInput.Scalar "same" ]) ] ]
            )

        let parsed = Schema.parse verifiedCustomerSchema raw

        test <@ not parsed.IsValid @>

        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "contacts"; PathSegment.Index 1 ]
                                    Error = SchemaError.ConstructorFailed "Kind and value must differ." } ]
            @>

    [<Fact>]
    let ``parse prefixes each item's diagnostics with that item's index`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "contacts",
                      RawInput.Many
                          [ validContact "email" "ada@example.com"
                            RawInput.Object(Map.ofList [ "kind", RawInput.Scalar ""; "value", RawInput.Scalar "" ]) ] ]
            )

        let parsed = Schema.parse customerSchema raw

        test <@ not parsed.IsValid @>

        test
            <@
                parsed.Errors
                |> List.sortBy (fun diagnostic -> diagnostic.Path)
                |> (=)
                    [ { Path = [ PathSegment.Name "contacts"; PathSegment.Index 1; PathSegment.Name "kind" ]
                        Error = SchemaError.Required }
                      { Path = [ PathSegment.Name "contacts"; PathSegment.Index 1; PathSegment.Name "value" ]
                        Error = SchemaError.Required } ]
            @>
