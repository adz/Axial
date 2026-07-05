namespace Axial.Tests

open Axial.Schema
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module InputParseTests =
    type private Signup = { Email: string; Age: int }

    let private schema =
        Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> Schema.field "email" _.Email (Value.text |> Value.withConstraint SchemaConstraint.required)
        |> Schema.int "age" _.Age
        |> Schema.build

    [<Fact>]
    let ``parse builds model from object input`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ada@example.com"
                      "age", RawInput.Scalar "42" ]
            )

        let parsed = Input.parse schema raw

        test <@ parsed.Input = raw @>
        test <@ parsed.IsValid @>
        test <@ parsed.Result = Ok { Email = "ada@example.com"; Age = 42 } @>
        test <@ parsed.Model = { Email = "ada@example.com"; Age = 42 } @>

    [<Fact>]
    let ``parse reports field diagnostics for invalid scalar input`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ada@example.com"
                      "age", RawInput.Scalar "not-an-int" ]
            )

        let parsed = Input.parse schema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.TryModel = None @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.InvalidFormat "int" ] @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "age" ]; Error = SchemaError.InvalidFormat "int" } ] @>

    [<Fact>]
    let ``parse reports root diagnostic when model input is not an object`` () =
        let parsed = Input.parse schema (RawInput.Scalar "ada@example.com")

        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ExpectedObject } ] @>
