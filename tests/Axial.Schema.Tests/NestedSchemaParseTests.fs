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
        Schema.define<Address>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "street" (fun address -> address.Street)
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "city" (fun address -> address.City)
        |> construct (fun street city -> ({ Street = street; City = city }: Address))

    let private customerSchema =
        Schema.define<Customer>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "name" (fun customer -> customer.Name)
        |> fieldWith (addressSchema |> Schema.constrain Constraint.required) "address" (fun customer -> customer.Address)
        |> construct (fun name address -> ({ Name = name; Address = address }: Customer))

    let private verifiedAddressSchema =
        Schema.define<VerifiedAddress>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "street" (fun address -> address.Street)
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "city" (fun address -> address.City)
        |> constructResult VerifiedAddress.Create

    let private verifiedCustomerSchema =
        Schema.define<VerifiedCustomer>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "name" (fun customer -> customer.Name)
        |> fieldWith (verifiedAddressSchema |> Schema.constrain Constraint.required) "address" (fun customer -> customer.Address)
        |> construct (fun name address -> ({ Name = name; Address = address }: VerifiedCustomer))

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
