namespace Axial.Tests

open Axial.Schema
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module NestedSchemaParseTests =
    type private Address = { Street: string; City: string }

    type private Customer = { Name: string; Address: Address }

    let private addressSchema =
        Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
        |> Schema.field "street" _.Street (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.field "city" _.City (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.build

    let private customerSchema =
        Schema.recordFor<Customer, _> (fun name address -> { Name = name; Address = address })
        |> Schema.field "name" _.Name (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.nestedWith [ SchemaConstraint.required ] "address" _.Address addressSchema
        |> Schema.build

    let private validAddress =
        RawInput.Object(Map.ofList [ "street", RawInput.Scalar "1 Infinite Loop"; "city", RawInput.Scalar "Cupertino" ])

    [<Fact>]
    let ``parse builds a nested model from object-shaped raw input`` () =
        let raw =
            RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada"; "address", validAddress ])

        let parsed = Input.parse customerSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Model = { Name = "Ada"; Address = { Street = "1 Infinite Loop"; City = "Cupertino" } } @>

    [<Fact>]
    let ``parse prefixes nested field diagnostics with the nested field's name`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "address", RawInput.Object(Map.ofList [ "street", RawInput.Scalar "1 Infinite Loop"; "city", RawInput.Missing ]) ]
            )

        let parsed = Input.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "address"; PathSegment.Name "city" ]; Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse reports expected object when nested raw input is a scalar`` () =
        let raw =
            RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada"; "address", RawInput.Scalar "not-an-object" ])

        let parsed = Input.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "address" ]; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``parse reports expected object when nested raw input is a collection`` () =
        let raw =
            RawInput.Object(
                Map.ofList [ "name", RawInput.Scalar "Ada"; "address", RawInput.Many [ RawInput.Scalar "not-an-object" ] ]
            )

        let parsed = Input.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "address" ]; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``parse reports required when the nested raw field is missing`` () =
        let raw = RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada" ])

        let parsed = Input.parse customerSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "address" ]; Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse accumulates every failing nested field alongside sibling failures`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Missing
                      "address", RawInput.Object(Map.ofList [ "street", RawInput.Missing; "city", RawInput.Missing ]) ]
            )

        let parsed = Input.parse customerSchema raw

        let sortedErrors =
            parsed.Errors
            |> List.sortBy (fun error -> error.Path |> List.map string |> String.concat ".")

        test <@ not parsed.IsValid @>

        test
            <@ sortedErrors = [ { Path = [ PathSegment.Name "address"; PathSegment.Name "city" ]; Error = SchemaError.Required }
                                { Path = [ PathSegment.Name "address"; PathSegment.Name "street" ]; Error = SchemaError.Required }
                                { Path = [ PathSegment.Name "name" ]; Error = SchemaError.Required } ] @>
