namespace Axial.Tests

open System
open Axial.Schema
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module InputParseTests =
    type private Signup = { Email: string; Age: int }

    type private AdultAge =
        private
            { Age: int }

        static member Create age =
            if age >= 18 then
                Ok { Age = age }
            else
                Error "Age must be at least 18."

    type private AgeError =
        | Underage

    type private MappedAdultAge =
        private
            { Age: int }

        static member Create age =
            if age >= 18 then
                Ok { Age = age }
            else
                Error Underage

    type private DateRange =
        private
            { Start: DateOnly
              End: DateOnly }

        static member Create start endDate =
            if start <= endDate then
                Ok { Start = start; End = endDate }
            else
                Error "End date must be on or after start date."

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
    let ``parse surfaces a constraint's custom message in place of the default error`` () =
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

        let raw =
            RawInput.Object(Map.ofList [ "email", RawInput.Missing; "age", RawInput.Scalar "10" ])

        let parsed = Input.parse messageSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Custom("required", Some "Email is required.") ] @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.Custom("atLeast", Some "Must be an adult.") ] @>

    [<Fact>]
    let ``parse falls back to the default error when a constraint has no custom message`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "   "
                      "age", RawInput.Scalar "42" ]
            )

        let parsed = Input.parse schema raw

        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>

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
    let ``parse builds a model from a constructor returning Ok`` () =
        let ageSchema =
            Schema.recordFor<AdultAge, _> AdultAge.Create
            |> Schema.int "age" _.Age
            |> Schema.buildResult

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "age", RawInput.Scalar "21" ]
            )

        let parsed = Input.parse ageSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Model = { Age = 21 } @>

    [<Fact>]
    let ``parse reports a constructor error from a constructor returning Error`` () =
        let ageSchema =
            Schema.recordFor<AdultAge, _> AdultAge.Create
            |> Schema.int "age" _.Age
            |> Schema.buildResult

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "age", RawInput.Scalar "17" ]
            )

        let parsed = Input.parse ageSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.TryModel = None @>
        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ConstructorFailed "Age must be at least 18." } ] @>

    [<Fact>]
    let ``parse can attach a constructor error to a field path`` () =
        let ageSchema =
            Schema.recordFor<AdultAge, _> AdultAge.Create
            |> Schema.int "age" _.Age
            |> Schema.buildResult

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "age", RawInput.Scalar "17" ]
            )

        let parsed = Input.parseWith (Input.constructorErrorAt "age") ageSchema raw

        test <@ not parsed.IsValid @>
        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "age" ]
                                   Error = SchemaError.ConstructorFailed "Age must be at least 18." } ] @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.ConstructorFailed "Age must be at least 18." ] @>

    [<Fact>]
    let ``field diagnostics gate constructor diagnostics`` () =
        let mutable constructorCalls = 0

        let gatedSchema =
            Schema.recordFor<AdultAge, _> (fun age ->
                constructorCalls <- constructorCalls + 1
                AdultAge.Create age)
            |> Schema.field "age" _.Age (Value.``int`` |> Value.withConstraint (SchemaConstraint.atLeast 0))
            |> Schema.buildResult

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "age", RawInput.Scalar "-1" ]
            )

        let parsed = Input.parse gatedSchema raw

        test <@ not parsed.IsValid @>
        test <@ constructorCalls = 0 @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "age" ]
                                    Error = SchemaError.RangeOutOfRange("atLeast 0", Some "-1") } ]
            @>

    [<Fact>]
    let ``parse maps constructor error values through buildResultWith`` () =
        let ageSchema =
            Schema.recordFor<MappedAdultAge, _> MappedAdultAge.Create
            |> Schema.int "age" _.Age
            |> Schema.buildResultWith (function Underage -> "Adult age is required.")

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "age", RawInput.Scalar "17" ]
            )

        let parsed = Input.parse ageSchema raw

        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ConstructorFailed "Adult age is required." } ] @>

    [<Fact>]
    let ``parse builds a DateRange when cross-field constructor invariant passes`` () =
        let rangeSchema =
            Schema.recordFor<DateRange, _> DateRange.Create
            |> Schema.date "start" _.Start
            |> Schema.date "end" _.End
            |> Schema.buildResult

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "start", RawInput.Scalar "2026-01-10"
                      "end", RawInput.Scalar "2026-01-12" ]
            )

        let parsed = Input.parse rangeSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Model.Start = DateOnly(2026, 1, 10) @>
        test <@ parsed.Model.End = DateOnly(2026, 1, 12) @>

    [<Fact>]
    let ``parse reports DateRange constructor invariant errors at root by default`` () =
        let rangeSchema =
            Schema.recordFor<DateRange, _> DateRange.Create
            |> Schema.date "start" _.Start
            |> Schema.date "end" _.End
            |> Schema.buildResult

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "start", RawInput.Scalar "2026-01-12"
                      "end", RawInput.Scalar "2026-01-10" ]
            )

        let parsed = Input.parse rangeSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.TryModel = None @>
        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ConstructorFailed "End date must be on or after start date." } ] @>

    [<Fact>]
    let ``parse can attach DateRange constructor invariant errors to the end field`` () =
        let rangeSchema =
            Schema.recordFor<DateRange, _> DateRange.Create
            |> Schema.date "start" _.Start
            |> Schema.date "end" _.End
            |> Schema.buildResult

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "start", RawInput.Scalar "2026-01-12"
                      "end", RawInput.Scalar "2026-01-10" ]
            )

        let parsed = Input.parseWith (Input.constructorErrorAt "end") rangeSchema raw

        test <@ not parsed.IsValid @>
        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "end" ]
                                   Error = SchemaError.ConstructorFailed "End date must be on or after start date." } ] @>
        test <@ parsed.ErrorsFor "end" = [ SchemaError.ConstructorFailed "End date must be on or after start date." ] @>

    [<Fact>]
    let ``DateRange field diagnostics gate cross-field constructor invariant errors`` () =
        let mutable constructorCalls = 0

        let rangeSchema =
            Schema.recordFor<DateRange, _> (fun start endDate ->
                constructorCalls <- constructorCalls + 1
                DateRange.Create start endDate)
            |> Schema.date "start" _.Start
            |> Schema.date "end" _.End
            |> Schema.buildResult

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "start", RawInput.Scalar "not-a-date"
                      "end", RawInput.Scalar "2026-01-10" ]
            )

        let parsed = Input.parse rangeSchema raw

        test <@ not parsed.IsValid @>
        test <@ constructorCalls = 0 @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "start" ]; Error = SchemaError.InvalidFormat "date" } ] @>

    [<Fact>]
    let ``parse retains raw input for redisplay after a failed parse`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ada@example.com"
                      "age", RawInput.Scalar "not-an-int" ]
            )

        let parsed = Input.parse schema raw

        test <@ not parsed.IsValid @>
        test <@ RawInput.redisplayPath "email" parsed.Input = "ada@example.com" @>
        test <@ RawInput.redisplayPath "age" parsed.Input = "not-an-int" @>

    [<Fact>]
    let ``parse maps a check failure from a value constraint to a schema error`` () =
        let minLengthSchema =
            Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
            |> Schema.field "email" _.Email (Value.text |> Value.withConstraint (SchemaConstraint.minLength 5))
            |> Schema.int "age" _.Age
            |> Schema.build

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ab"
                      "age", RawInput.Scalar "42" ]
            )

        let parsed = Input.parse minLengthSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.TooShort(5, Some 2) ] @>

    [<Fact>]
    let ``schema errors are identical across differently named fields, only the diagnostics path differs`` () =
        let makeSchema fieldName =
            Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
            |> Schema.field fieldName _.Email (Value.text |> Value.withConstraint SchemaConstraint.required)
            |> Schema.int "age" _.Age
            |> Schema.build

        let rawFor fieldName =
            RawInput.Object(Map.ofList [ fieldName, RawInput.Missing; "age", RawInput.Scalar "42" ])

        let shortNameParsed = Input.parse (makeSchema "email") (rawFor "email")
        let longNameParsed = Input.parse (makeSchema "emailAddress") (rawFor "emailAddress")

        test <@ (shortNameParsed.Errors |> List.map _.Error) = (longNameParsed.Errors |> List.map _.Error) @>
        test <@ shortNameParsed.Errors <> longNameParsed.Errors @>

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
