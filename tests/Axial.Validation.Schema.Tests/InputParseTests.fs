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

        test <@ parsed.Input = raw @>
        test <@ not parsed.IsValid @>
        test <@ parsed.TryModel = None @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.InvalidFormat "int" ] @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "age" ]; Error = SchemaError.InvalidFormat "int" } ] @>

    [<Fact>]
    let ``parse accumulates diagnostics for every failing sibling field`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "   "
                      "age", RawInput.Scalar "not-an-int" ]
            )

        let parsed = Input.parse schema raw

        test <@ parsed.Input = raw @>
        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.InvalidFormat "int" ] @>

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "age" ]; Error = SchemaError.InvalidFormat "int" }
                                 { Path = [ PathSegment.Name "email" ]; Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse reports root diagnostic when model input is not an object`` () =
        let raw = RawInput.Scalar "ada@example.com"
        let parsed = Input.parse schema raw

        test <@ parsed.Input = raw @>
        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``required reports missing raw field as required`` () =
        let raw = RawInput.Object(Map.ofList [ "age", RawInput.Scalar "42" ])

        let parsed = Input.parse schema raw

        test <@ parsed.Input = raw @>
        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "email" ]; Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse retains raw input on failure`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "   "
                      "age", RawInput.Scalar "not-an-int" ]
            )

        let parsed = Input.parse schema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Input = raw @>

    [<Fact>]
    let ``required reports explicit missing raw scalar as required`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Missing
                      "age", RawInput.Scalar "42" ]
            )

        let parsed = Input.parse schema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>

    [<Fact>]
    let ``required reports blank text scalar as required`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "   "
                      "age", RawInput.Scalar "42" ]
            )

        let parsed = Input.parse schema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>

    [<Fact>]
    let ``parse does not call the model constructor when a field fails to parse`` () =
        let mutable constructorCalls = 0

        let countingSchema =
            Schema.recordFor<Signup, _> (fun email age ->
                constructorCalls <- constructorCalls + 1
                { Email = email; Age = age })
            |> Schema.field "email" _.Email (Value.text |> Value.withConstraint SchemaConstraint.required)
            |> Schema.int "age" _.Age
            |> Schema.build

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ada@example.com"
                      "age", RawInput.Scalar "not-an-int" ]
            )

        let parsed = Input.parse countingSchema raw

        test <@ not parsed.IsValid @>
        test <@ constructorCalls = 0 @>

    [<Fact>]
    let ``parse calls the model constructor exactly once when every field parses`` () =
        let mutable constructorCalls = 0

        let countingSchema =
            Schema.recordFor<Signup, _> (fun email age ->
                constructorCalls <- constructorCalls + 1
                { Email = email; Age = age })
            |> Schema.field "email" _.Email (Value.text |> Value.withConstraint SchemaConstraint.required)
            |> Schema.int "age" _.Age
            |> Schema.build

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ada@example.com"
                      "age", RawInput.Scalar "42" ]
            )

        let parsed = Input.parse countingSchema raw

        test <@ parsed.IsValid @>
        test <@ constructorCalls = 1 @>

    [<Fact>]
    let ``required reports blank non-text scalar as required`` () =
        let requiredAgeSchema =
            Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
            |> Schema.text "email" _.Email
            |> Schema.field "age" _.Age (Value.``int`` |> Value.withConstraint SchemaConstraint.required)
            |> Schema.build

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ada@example.com"
                      "age", RawInput.Scalar "   " ]
            )

        let parsed = Input.parse requiredAgeSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.Required ] @>
