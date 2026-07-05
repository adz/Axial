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

    let private schema =
        Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> Schema.field
            "email"
            _.Email
            (Value.text
             |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.email; SchemaConstraint.maxLength 254 ])
        |> Schema.field "age" _.Age (Value.``int`` |> Value.withConstraint (SchemaConstraint.atLeast 18))
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
