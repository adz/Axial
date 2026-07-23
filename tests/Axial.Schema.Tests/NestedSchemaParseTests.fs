namespace Axial.Tests

open Axial

open Axial.ErrorHandling

open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module NestedSchemaParseTests =
    type private Address = { Street: string; City: string }

    type private VerifiedAddress =
        private
            { Street: string
              City: string }

        static member Create street city =
            if street <> city then
                Ok { Street = street; City = city }
            else
                Error "Street and city must differ."

    type private Customer = { Name: string; Address: Address }

    type private VerifiedCustomer = { Name: string; Address: VerifiedAddress }

    let private addressSchema =
        SchemaCE.schema<Address> {
            SchemaCE.field "street" (fun (address: Address) -> address.Street) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "city" (fun (address: Address) -> address.City) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.construct (fun street city -> ({ Street = street; City = city }: Address))
        }

    let private customerSchema =
        SchemaCE.schema<Customer> {
            SchemaCE.field "name" (fun (customer: Customer) -> customer.Name) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "address" (fun (customer: Customer) -> customer.Address) {
                withSchema (addressSchema |> Schema.constrain Constraint.required)
            }
            SchemaCE.construct (fun name address -> ({ Name = name; Address = address }: Customer))
        }

    let private verifiedAddressSchema =
        SchemaCE.schema<VerifiedAddress> {
            SchemaCE.field "street" (fun (address: VerifiedAddress) -> address.Street) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "city" (fun (address: VerifiedAddress) -> address.City) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.constructResult VerifiedAddress.Create
        }

    let private verifiedCustomerSchema =
        SchemaCE.schema<VerifiedCustomer> {
            SchemaCE.field "name" (fun (customer: VerifiedCustomer) -> customer.Name) {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "address" (fun (customer: VerifiedCustomer) -> customer.Address) {
                withSchema (verifiedAddressSchema |> Schema.constrain Constraint.required)
            }
            SchemaCE.construct (fun name address -> ({ Name = name; Address = address }: VerifiedCustomer))
        }

    let private validAddress =
        Data.objectOfMap (Map.ofList [ "street", Data.Text "1 Infinite Loop"; "city", Data.Text "Cupertino" ])

    [<Fact>]
    let ``parse builds a nested model from object-shaped structured data`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "address", validAddress ])

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Value = { Name = "Ada"; Address = { Street = "1 Infinite Loop"; City = "Cupertino" } } @>

    [<Fact>]
    let ``parse prefixes nested field diagnostics with the nested field's name`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "address", Data.objectOfMap (Map.ofList [ "street", Data.Text "1 Infinite Loop"; "city", Data.Null ]) ]
            )

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "address"; PathSegment.Name "city" ]; Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse attaches nested constructor errors to the nested object root by default`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "address", Data.objectOfMap (Map.ofList [ "street", Data.Text "Same"; "city", Data.Text "Same" ]) ]
            )

        let parsed = Schema.parseRetainingInput verifiedCustomerSchema raw

        test <@ not parsed.IsValid @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "address" ]
                                    Error = SchemaError.ConstructorFailed "Street and city must differ." } ]
            @>

    [<Fact>]
    let ``parse reports expected object when nested structured data is a scalar`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "address", Data.Text "not-an-object" ])

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "address" ]; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``parse reports expected object when nested structured data is a collection`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "address", Data.List [ Data.Text "not-an-object" ] ]
            )

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "address" ]; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``parse reports required when the nested raw field is missing`` () =
        let raw = Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada" ])

        let parsed = Schema.parseRetainingInput customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "address" ]; Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse accumulates every failing nested field alongside sibling failures`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Null
                      "address", Data.objectOfMap (Map.ofList [ "street", Data.Null; "city", Data.Null ]) ]
            )

        let parsed = Schema.parseRetainingInput customerSchema raw

        let sortedErrors =
            parsed.Errors
            |> List.sortBy (fun error -> error.Path |> List.map string |> String.concat ".")

        test <@ not parsed.IsValid @>

        test
            <@ sortedErrors = [ { Path = [ PathSegment.Name "address"; PathSegment.Name "city" ]; Error = SchemaError.Required }
                                { Path = [ PathSegment.Name "address"; PathSegment.Name "street" ]; Error = SchemaError.Required }
                                { Path = [ PathSegment.Name "name" ]; Error = SchemaError.Required } ] @>
