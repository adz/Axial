namespace Axial.Tests

open Axial.Schema
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module SchemaValidationTests =
    type private Signup = { Email: string; Age: int }

    type private SwappedFields =
        { Primary: string
          Secondary: string }

    type private Address =
        { Street: string
          City: string }

    type private Customer =
        { Name: string
          Address: Address }

    type private ContactMethod = { Kind: string; Value: string }

    type private ContactBook =
        { Name: string
          Contacts: ContactMethod list }

    let private schema =
        Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> Schema.field
            "email"
            _.Email
            (Value.text
             |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.email; SchemaConstraint.maxLength 254 ])
        |> Schema.field "age" _.Age (Value.``int`` |> Value.withConstraint (SchemaConstraint.atLeast 18))
        |> Schema.build

    let private contactMethodSchema =
        Schema.recordFor<ContactMethod, _> (fun kind value -> { Kind = kind; Value = value })
        |> Schema.field "kind" _.Kind (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.field "value" _.Value (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.build

    let private contactBookSchema =
        Schema.recordFor<ContactBook, _> (fun name contacts -> { Name = name; Contacts = contacts })
        |> Schema.field "name" _.Name (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.manyWith
            [ SchemaConstraint.minCount 1; SchemaConstraint.maxCount 2 ]
            "contacts"
            _.Contacts
            contactMethodSchema
        |> Schema.build

    [<Fact>]
    let ``validate returns the original model when schema constraints pass`` () =
        let model = { Email = "ada@example.com"; Age = 42 }

        let validation = Axial.Validation.Schema.Validation.validate schema model

        test <@ Axial.Validation.Validation.toResult validation = Ok model @>

    [<Fact>]
    let ``validate reports diagnostics for existing model values that violate schema constraints`` () =
        let validation =
            Axial.Validation.Schema.Validation.validate schema { Email = ""; Age = 10 }

        test
            <@
                Axial.Validation.Validation.toResult validation =
                    Error
                        {
                            Errors = []
                            Children =
                                Map.ofList
                                    [ PathSegment.Name "age",
                                      Diagnostics.singleton (SchemaError.RangeOutOfRange("atLeast 18", Some "10"))
                                      PathSegment.Name "email",
                                      {
                                          Errors = [ SchemaError.Required; SchemaError.InvalidFormat "email" ]
                                          Children = Map.empty
                                      } ]
                        }
            @>

    [<Fact>]
    let ``validate surfaces schema constraint custom messages through Check lowering`` () =
        let messageSchema =
            Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
            |> Schema.field
                "email"
                _.Email
                (Value.text
                 |> Value.withConstraint (SchemaConstraint.required |> SchemaConstraint.withMessage "Email is required."))
            |> Schema.field
                "age"
                _.Age
                (Value.``int``
                 |> Value.withConstraint (SchemaConstraint.atLeast 18 |> SchemaConstraint.withMessage "Must be an adult."))
            |> Schema.build

        let validation =
            Axial.Validation.Schema.Validation.validate messageSchema { Email = ""; Age = 10 }

        test
            <@
                Axial.Validation.Validation.toResult validation =
                    Error
                        {
                            Errors = []
                            Children =
                                Map.ofList
                                    [ PathSegment.Name "age",
                                      Diagnostics.singleton (SchemaError.Custom("atLeast", Some "Must be an adult."))
                                      PathSegment.Name "email",
                                      Diagnostics.singleton (SchemaError.Custom("required", Some "Email is required.")) ]
                        }
            @>

    [<Fact>]
    let ``validate reads existing model values through schema getters`` () =
        let swappedSchema =
            Schema.recordFor<SwappedFields, _> (fun primary secondary ->
                { Primary = primary
                  Secondary = secondary })
            |> Schema.field
                "secondary-on-wire"
                _.Primary
                (Value.text |> Value.withConstraint (SchemaConstraint.oneOf [ "primary-value" ]))
            |> Schema.field
                "primary-on-wire"
                _.Secondary
                (Value.text |> Value.withConstraint (SchemaConstraint.oneOf [ "secondary-value" ]))
            |> Schema.build

        let validation =
            Axial.Validation.Schema.Validation.validate
                swappedSchema
                { Primary = "primary-value"
                  Secondary = "wrong-secondary" }

        test
            <@
                Axial.Validation.Validation.toResult validation =
                    Error
                        {
                            Errors = []
                            Children =
                                Map.ofList
                                    [ PathSegment.Name "primary-on-wire",
                                      Diagnostics.singleton (SchemaError.NotOneOf "secondary-value") ]
                        }
            @>

    [<Fact>]
    let ``validate checks nested model values through their nested schema`` () =
        let addressSchema =
            Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
            |> Schema.field "street" _.Street (Value.text |> Value.withConstraint SchemaConstraint.required)
            |> Schema.field "city" _.City (Value.text |> Value.withConstraint SchemaConstraint.required)
            |> Schema.build

        let customerSchema =
            Schema.recordFor<Customer, _> (fun name address -> { Name = name; Address = address })
            |> Schema.field "name" _.Name (Value.text |> Value.withConstraint SchemaConstraint.required)
            |> Schema.nested "address" _.Address addressSchema
            |> Schema.build

        let validation =
            Axial.Validation.Schema.Validation.validate
                customerSchema
                { Name = "Ada"
                  Address = { Street = "1 Main Street"; City = "" } }

        test
            <@
                Axial.Validation.Validation.toResult validation =
                    Error
                        {
                            Errors = []
                            Children =
                                Map.ofList
                                    [ PathSegment.Name "address",
                                      {
                                          Errors = []
                                          Children =
                                              Map.ofList
                                                  [ PathSegment.Name "city",
                                                    Diagnostics.singleton SchemaError.Required ]
                                      } ]
                        }
            @>

    [<Fact>]
    let ``validate checks collection item values through their item schema`` () =
        let model =
            { Name = "Ada"
              Contacts =
                [ { Kind = ""; Value = "ada@example.com" }
                  { Kind = "phone"; Value = "" } ] }

        let validation =
            Axial.Validation.Schema.Validation.validate contactBookSchema model

        test
            <@
                Axial.Validation.Validation.toResult validation =
                    Error
                        {
                            Errors = []
                            Children =
                                Map.ofList
                                    [ PathSegment.Name "contacts",
                                      {
                                          Errors = []
                                          Children =
                                              Map.ofList
                                                  [ PathSegment.Index 0,
                                                    {
                                                        Errors = []
                                                        Children =
                                                            Map.ofList
                                                                [ PathSegment.Name "kind",
                                                                  Diagnostics.singleton SchemaError.Required ]
                                                    }
                                                    PathSegment.Index 1,
                                                    {
                                                        Errors = []
                                                        Children =
                                                            Map.ofList
                                                                [ PathSegment.Name "value",
                                                                  Diagnostics.singleton SchemaError.Required ]
                                                    } ]
                                      } ]
                        }
            @>

    [<Fact>]
    let ``validate reports collection count constraints at the collection field path`` () =
        let model =
            { Name = "Ada"
              Contacts =
                [ { Kind = "email"; Value = "ada@example.com" }
                  { Kind = "phone"; Value = "+61 400 000 000" }
                  { Kind = "sms"; Value = "+61 400 000 000" } ] }

        let validation =
            Axial.Validation.Schema.Validation.validate contactBookSchema model

        test
            <@
                Axial.Validation.Validation.toResult validation =
                    Error
                        {
                            Errors = []
                            Children =
                                Map.ofList
                                    [ PathSegment.Name "contacts",
                                      Diagnostics.singleton (SchemaError.CountOutOfRange("maxCount 2", Some 3)) ]
                        }
            @>

    [<Fact>]
    let ``values produced by input parsing validate through the same schema`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ada@example.com"
                      "age", RawInput.Scalar "42" ]
            )

        let parsed = Input.parse schema raw
        let validation = Axial.Validation.Schema.Validation.validate schema parsed.Model

        test <@ parsed.IsValid @>
        test <@ Axial.Validation.Validation.toResult validation = Ok parsed.Model @>
