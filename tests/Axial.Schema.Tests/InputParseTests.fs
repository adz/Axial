namespace Axial.Tests

open Axial

open Axial.ErrorHandling

open System
open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

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
        Schema.define<Signup>
        |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "email" _.Email
        |> fieldWith Schema.int "age" _.Age
        |> construct (fun email age -> { Email = email; Age = age })

    [<Fact>]
    let ``parse builds model from object input`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "age", Data.Text "42" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test <@ parsed.Input = raw @>
        test <@ parsed.IsValid @>
        test <@ parsed.Result = Ok { Email = "ada@example.com"; Age = 42 } @>
        test <@ parsed.Value = { Email = "ada@example.com"; Age = 42 } @>

    [<Fact>]
    let ``parse reports field diagnostics for invalid scalar input`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test <@ parsed.Input = raw @>
        test <@ not parsed.IsValid @>
        test <@ parsed.TryValue = None @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.InvalidFormat "int" ] @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "age" ]; Error = SchemaError.InvalidFormat "int" } ] @>

    [<Fact>]
    let ``parse accumulates diagnostics for every failing sibling field`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "   "
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

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
            Schema.define<Signup>
            |> fieldWith (Schema.text
                 |> Schema.constrain (Constraint.required |> Constraint.withMessage "Email is required.")) "email" _.Email
            |> fieldWith (Schema.int
                 |> Schema.constrain (Constraint.atLeast 18 |> Constraint.withMessage "Must be an adult.")) "age" _.Age
            |> construct (fun email age -> { Email = email; Age = age })

        let raw =
            Data.objectOfMap (Map.ofList [ "email", Data.Null; "age", Data.Text "10" ])

        let parsed = Schema.parseRetainingInput messageSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Custom("required", Some "Email is required.") ] @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.Custom("atLeast", Some "Must be an adult.") ] @>

    [<Fact>]
    let ``parse falls back to the default error when a constraint has no custom message`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "   "
                      "age", Data.Text "42" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>

    [<Fact>]
    let ``parse reports root diagnostic when model input is not an object`` () =
        let raw = Data.Text "ada@example.com"
        let parsed = Schema.parseRetainingInput schema raw

        test <@ parsed.Input = raw @>
        test <@ not parsed.IsValid @>
        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``required reports missing raw field as required`` () =
        let raw = Data.objectOfMap (Map.ofList [ "age", Data.Text "42" ])

        let parsed = Schema.parseRetainingInput schema raw

        test <@ parsed.Input = raw @>
        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "email" ]; Error = SchemaError.Required } ] @>

    [<Fact>]
    let ``parse retains structured data on failure`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "   "
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.Input = raw @>

    [<Fact>]
    let ``required reports explicit missing raw scalar as required`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Null
                      "age", Data.Text "42" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>

    [<Fact>]
    let ``required reports blank text scalar as required`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "   "
                      "age", Data.Text "42" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>

    [<Fact>]
    let ``parse does not call the model constructor when a field fails to parse`` () =
        let mutable constructorCalls = 0

        let countingSchema =
            Schema.define<Signup>
            |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "email" _.Email
            |> fieldWith Schema.int "age" _.Age
            |> construct (fun email age ->
                constructorCalls <- constructorCalls + 1
                { Email = email; Age = age })

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput countingSchema raw

        test <@ not parsed.IsValid @>
        test <@ constructorCalls = 0 @>

    [<Fact>]
    let ``parse calls the model constructor exactly once when every field parses`` () =
        let mutable constructorCalls = 0

        let countingSchema =
            Schema.define<Signup>
            |> fieldWith (Schema.text |> Schema.constrain Constraint.required) "email" _.Email
            |> fieldWith Schema.int "age" _.Age
            |> construct (fun email age ->
                constructorCalls <- constructorCalls + 1
                { Email = email; Age = age })

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "age", Data.Text "42" ]
            )

        let parsed = Schema.parseRetainingInput countingSchema raw

        test <@ parsed.IsValid @>
        test <@ constructorCalls = 1 @>

    [<Fact>]
    let ``parse builds a model from a constructor returning Ok`` () =
        let ageSchema =
            Schema.define<AdultAge>
            |> fieldWith Schema.int "age" _.Age
            |> constructResult AdultAge.Create

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "age", Data.Text "21" ]
            )

        let parsed = Schema.parseRetainingInput ageSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Value = { Age = 21 } @>

    [<Fact>]
    let ``parse reports a constructor error from a constructor returning Error`` () =
        let ageSchema =
            Schema.define<AdultAge>
            |> fieldWith Schema.int "age" _.Age
            |> constructResult AdultAge.Create

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "age", Data.Text "17" ]
            )

        let parsed = Schema.parseRetainingInput ageSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.TryValue = None @>
        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ConstructorFailed "Age must be at least 18." } ] @>

    [<Fact>]
    let ``parse can attach a constructor error to a field path`` () =
        let ageSchema =
            Schema.define<AdultAge>
            |> fieldWith Schema.int "age" _.Age
            |> constructResult AdultAge.Create

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "age", Data.Text "17" ]
            )

        let parsed =
            Schema.parseWith (Schema.constructorErrorAt "age") ageSchema raw
            |> RetainedParseResult.create raw

        test <@ not parsed.IsValid @>
        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "age" ]
                                   Error = SchemaError.ConstructorFailed "Age must be at least 18." } ] @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.ConstructorFailed "Age must be at least 18." ] @>

    [<Fact>]
    let ``field diagnostics gate constructor diagnostics`` () =
        let mutable constructorCalls = 0

        let gatedSchema =
            Schema.define<AdultAge>
            |> fieldWith (Schema.int |> Schema.constrain (Constraint.atLeast 0)) "age" _.Age
            |> constructResult (fun age ->
                constructorCalls <- constructorCalls + 1
                AdultAge.Create age)

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "age", Data.Text "-1" ]
            )

        let parsed = Schema.parseRetainingInput gatedSchema raw

        test <@ not parsed.IsValid @>
        test <@ constructorCalls = 0 @>
        test
            <@
                parsed.Errors = [ { Path = [ PathSegment.Name "age" ]
                                    Error = SchemaError.OutOfRange(CheckRangeExpectation.AtLeast "0", Some "-1") } ]
            @>

    [<Fact>]
    let ``parse maps domain constructor errors before closing the shape`` () =
        let ageSchema =
            Schema.define<MappedAdultAge>
            |> fieldWith Schema.int "age" _.Age
            |> constructResult (fun age ->
                MappedAdultAge.Create age
                |> Result.mapError (function Underage -> "Adult age is required."))

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "age", Data.Text "17" ]
            )

        let parsed = Schema.parseRetainingInput ageSchema raw

        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ConstructorFailed "Adult age is required." } ] @>

    [<Fact>]
    let ``parse builds a DateRange when cross-field constructor invariant passes`` () =
        let rangeSchema =
            Schema.define<DateRange>
            |> fieldWith Schema.date "start" _.Start
            |> fieldWith Schema.date "end" _.End
            |> constructResult DateRange.Create

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "2026-01-10"
                      "end", Data.Text "2026-01-12" ]
            )

        let parsed = Schema.parseRetainingInput rangeSchema raw

        test <@ parsed.IsValid @>
        test <@ parsed.Value.Start = DateOnly(2026, 1, 10) @>
        test <@ parsed.Value.End = DateOnly(2026, 1, 12) @>

    [<Fact>]
    let ``parse reports DateRange constructor invariant errors at root by default`` () =
        let rangeSchema =
            Schema.define<DateRange>
            |> fieldWith Schema.date "start" _.Start
            |> fieldWith Schema.date "end" _.End
            |> constructResult DateRange.Create

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "2026-01-12"
                      "end", Data.Text "2026-01-10" ]
            )

        let parsed = Schema.parseRetainingInput rangeSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.TryValue = None @>
        test <@ parsed.Errors = [ { Path = []; Error = SchemaError.ConstructorFailed "End date must be on or after start date." } ] @>

    [<Fact>]
    let ``parse can attach DateRange constructor invariant errors to the end field`` () =
        let rangeSchema =
            Schema.define<DateRange>
            |> fieldWith Schema.date "start" _.Start
            |> fieldWith Schema.date "end" _.End
            |> constructResult DateRange.Create

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "2026-01-12"
                      "end", Data.Text "2026-01-10" ]
            )

        let parsed =
            Schema.parseWith (Schema.constructorErrorAt "end") rangeSchema raw
            |> RetainedParseResult.create raw

        test <@ not parsed.IsValid @>
        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "end" ]
                                   Error = SchemaError.ConstructorFailed "End date must be on or after start date." } ] @>
        test <@ parsed.ErrorsFor "end" = [ SchemaError.ConstructorFailed "End date must be on or after start date." ] @>

    [<Fact>]
    let ``DateRange field diagnostics gate cross-field constructor invariant errors`` () =
        let mutable constructorCalls = 0

        let rangeSchema =
            Schema.define<DateRange>
            |> fieldWith Schema.date "start" _.Start
            |> fieldWith Schema.date "end" _.End
            |> constructResult (fun start endDate ->
                constructorCalls <- constructorCalls + 1
                DateRange.Create start endDate)

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "not-a-date"
                      "end", Data.Text "2026-01-10" ]
            )

        let parsed = Schema.parseRetainingInput rangeSchema raw

        test <@ not parsed.IsValid @>
        test <@ constructorCalls = 0 @>
        test <@ parsed.Errors = [ { Path = [ PathSegment.Name "start" ]; Error = SchemaError.InvalidFormat "date" } ] @>

    [<Fact>]
    let ``parse retains structured data for redisplay after a failed parse`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test <@ not parsed.IsValid @>
        test <@ Data.redisplayPath "email" parsed.Input = "ada@example.com" @>
        test <@ Data.redisplayPath "age" parsed.Input = "not-an-int" @>

    [<Fact>]
    let ``parse maps a check failure from a value constraint to a schema error`` () =
        let minLengthSchema =
            Schema.define<Signup>
            |> fieldWith (Schema.text |> Schema.constrain (Constraint.minLength 5)) "email" _.Email
            |> fieldWith Schema.int "age" _.Age
            |> construct (fun email age -> { Email = email; Age = age })

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ab"
                      "age", Data.Text "42" ]
            )

        let parsed = Schema.parseRetainingInput minLengthSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.InvalidLength(CheckLengthExpectation.MinimumLength 5, Some 2) ] @>

    [<Fact>]
    let ``schema errors are identical across differently named fields, only the diagnostics path differs`` () =
        let makeSchema fieldName =
            Schema.define<Signup>
            |> fieldWith (Schema.text |> Schema.constrain Constraint.required) fieldName _.Email
            |> fieldWith Schema.int "age" _.Age
            |> construct (fun email age -> { Email = email; Age = age })

        let rawFor fieldName =
            Data.objectOfMap (Map.ofList [ fieldName, Data.Null; "age", Data.Text "42" ])

        let shortNameParsed = Schema.parseRetainingInput (makeSchema "email") (rawFor "email")
        let longNameParsed = Schema.parseRetainingInput (makeSchema "emailAddress") (rawFor "emailAddress")

        test <@ (shortNameParsed.Errors |> List.map _.Error) = (longNameParsed.Errors |> List.map _.Error) @>
        test <@ shortNameParsed.Errors <> longNameParsed.Errors @>

    [<Fact>]
    let ``required reports blank non-text scalar as required`` () =
        let requiredAgeSchema =
            Schema.define<Signup>
            |> fieldWith Schema.text "email" _.Email
            |> fieldWith (Schema.int |> Schema.constrain Constraint.required) "age" _.Age
            |> construct (fun email age -> { Email = email; Age = age })

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "age", Data.Text "   " ]
            )

        let parsed = Schema.parseRetainingInput requiredAgeSchema raw

        test <@ not parsed.IsValid @>
        test <@ parsed.ErrorsFor "age" = [ SchemaError.Required ] @>
