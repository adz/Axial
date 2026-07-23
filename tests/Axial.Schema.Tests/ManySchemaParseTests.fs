namespace Axial.Tests

open Axial

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
        SchemaCE.schema<ContactMethod> {
            SchemaCE.field "kind" (fun (contact: ContactMethod) -> contact.Kind) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "value" (fun (contact: ContactMethod) -> contact.Value) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.construct (fun kind value -> ({ Kind = kind; Value = value }: ContactMethod))
        }

    let private verifiedContactMethodSchema =
        SchemaCE.schema<VerifiedContactMethod> {
            SchemaCE.field "kind" (fun (contact: VerifiedContactMethod) -> contact.Kind) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "value" (fun (contact: VerifiedContactMethod) -> contact.Value) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.constructResult VerifiedContactMethod.Create
        }

    let private customerSchema =
        SchemaCE.schema<Customer> {
            SchemaCE.field "name" (fun (customer: Customer) -> customer.Name) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "contacts" (fun (customer: Customer) -> customer.Contacts) {
                withSchema (Schema.listWith contactMethodSchema)
            }
            SchemaCE.construct (fun name contacts -> ({ Name = name; Contacts = contacts }: Customer))
        }

    let private verifiedCustomerSchema =
        SchemaCE.schema<VerifiedCustomer> {
            SchemaCE.field "name" (fun (customer: VerifiedCustomer) -> customer.Name) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "contacts" (fun (customer: VerifiedCustomer) -> customer.Contacts) {
                withSchema (Schema.listWith verifiedContactMethodSchema)
            }
            SchemaCE.construct (fun name contacts -> ({ Name = name; Contacts = contacts }: VerifiedCustomer))
        }

    let private constrainedCustomerSchema =
        SchemaCE.schema<Customer> {
            SchemaCE.field "name" (fun (customer: Customer) -> customer.Name) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "contacts" (fun (customer: Customer) -> customer.Contacts) {
                withSchema (
                    Schema.listWith contactMethodSchema
                    |> Schema.constrainAll [ Constraint.minCount 1; Constraint.maxCount 2 ]
                )
            }
            SchemaCE.construct (fun name contacts -> ({ Name = name; Contacts = contacts }: Customer))
        }

    let private validContact kind value =
        Data.objectOfMap (Map.ofList [ "kind", Data.Text kind; "value", Data.Text value ])

    [<Fact>]
    let ``parse builds a collection from collection-shaped structured data`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "contacts", Data.List [ validContact "email" "ada@example.com" ] ]
            )

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Value = { Name = "Ada"; Contacts = [ { Kind = "email"; Value = "ada@example.com" } ] } @>

    [<Fact>]
    let ``parse builds an empty collection from an empty collection-shaped structured data`` () =
        let raw = Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "contacts", Data.List [] ])

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Value = { Name = "Ada"; Contacts = [] } @>

    [<Fact>]
    let ``parse accepts a collection whose item count satisfies field constraints`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "contacts",
                      Data.List
                          [ validContact "email" "ada@example.com"
                            validContact "phone" "+61 400 000 000" ] ]
            )

        let parsed = Schema.parseRetainingInput constrainedCustomerSchema raw

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
        let raw = Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "contacts", Data.List [] ])

        let parsed = Schema.parseRetainingInput constrainedCustomerSchema raw

        test <@ not parsed.IsValid @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]
                                    Error = SchemaError.InvalidCount(CheckCountExpectation.MinimumCount 1, Some 0) } ]
            @>

    [<Fact>]
    let ``parse reports max count constraint failures at the collection field path`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "contacts",
                      Data.List
                          [ validContact "email" "ada@example.com"
                            validContact "phone" "+61 400 000 000"
                            validContact "sms" "+61 400 000 000" ] ]
            )

        let parsed = Schema.parseRetainingInput constrainedCustomerSchema raw

        test <@ not parsed.IsValid @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]
                                    Error = SchemaError.InvalidCount(CheckCountExpectation.MaximumCount 2, Some 3) } ]
            @>

    [<Fact>]
    let ``parse reports expected collection when the collection field structured data is object-shaped`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "contacts", Data.objectOfMap (Map.ofList [ "kind", Data.Text "email" ]) ]
            )

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]; Error = SchemaError.ExpectedMany } ] @>

    [<Fact>]
    let ``parse reports expected collection when the collection field structured data is a scalar`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "contacts", Data.Text "not-a-collection" ])

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "contacts" ]; Error = SchemaError.ExpectedMany } ] @>

    [<Fact>]
    let ``parse reports expected object for an item that is not object-shaped`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "contacts", Data.List [ Data.Text "not-an-object" ] ]
            )

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ not parsed.IsValid @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "contacts"; PathSegment.Index 0 ]
                                    Error = SchemaError.ExpectedObject } ]
            @>

    [<Fact>]
    let ``parse builds a collection from primitive item schemas`` () =
        let schema =
            SchemaCE.schema<Tags> {
                SchemaCE.field "values" _.Values {
                    withSchema (Schema.listWith (Schema.text |> Schema.constrain Constraint.required))
                }
                SchemaCE.construct (fun values -> { Values = values })
            }

        let raw =
            Data.objectOfMap (Map.ofList [ "values", Data.List [ Data.Text "fsharp"; Data.Text "typed-errors" ] ])

        let parsed = Schema.parseRetainingInput schema raw

        test <@ parsed.Result = Ok { Values = [ "fsharp"; "typed-errors" ] } @>

    [<Fact>]
    let ``parse reports primitive item failures at collection index paths`` () =
        let schema =
            SchemaCE.schema<Tags> {
                SchemaCE.field "values" _.Values {
                    withSchema (Schema.listWith (Schema.text |> Schema.constrain Constraint.required))
                }
                SchemaCE.construct (fun values -> { Values = values })
            }

        let raw =
            Data.objectOfMap (Map.ofList [ "values", Data.List [ Data.Text "fsharp"; Data.Text "   " ] ])

        let parsed = Schema.parseRetainingInput schema raw

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "values"; PathSegment.Index 1 ]
                                   Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse accumulates errors from every failing item instead of stopping at the first`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "contacts",
                      Data.List
                          [ Data.objectOfMap (Map.ofList [ "kind", Data.Text ""; "value", Data.Text "ada@example.com" ])
                            Data.objectOfMap (Map.ofList [ "kind", Data.Text "email"; "value", Data.Text "" ]) ] ]
            )

        let parsed = Schema.parseRetainingInput customerSchema raw

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
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "contacts",
                      Data.List
                          [ validContact "email" "ada@example.com"
                            Data.objectOfMap (Map.ofList [ "kind", Data.Text "same"; "value", Data.Text "same" ]) ] ]
            )

        let parsed = Schema.parseRetainingInput verifiedCustomerSchema raw

        test <@ not parsed.IsValid @>

        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "contacts"; PathSegment.Index 1 ]
                                    Error = SchemaError.ConstructorFailed "Kind and value must differ." } ]
            @>

    [<Fact>]
    let ``parse prefixes each item's diagnostics with that item's index`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "contacts",
                      Data.List
                          [ validContact "email" "ada@example.com"
                            Data.objectOfMap (Map.ofList [ "kind", Data.Text ""; "value", Data.Text "" ]) ] ]
            )

        let parsed = Schema.parseRetainingInput customerSchema raw

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
