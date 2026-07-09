namespace Axial.Tests

open Axial.ErrorHandling

open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module ParsedInputTests =
    type private Signup = { Email: string }

    [<Fact>]
    let ``parsed input retains raw input with successful model result`` () =
        let raw =
            RawInput.Object(Map.ofList [ "email", RawInput.Scalar "ada@example.com" ])

        let parsed: ParsedInput<Signup, string> =
            {
                Input = raw
                Result = Ok { Email = "ada@example.com" }
            }

        test <@ parsed.Input = raw @>
        test <@ parsed.Result = Ok { Email = "ada@example.com" } @>
        test <@ parsed.IsValid @>
        test <@ parsed.Model = { Email = "ada@example.com" } @>
        test <@ parsed.TryModel = Some { Email = "ada@example.com" } @>
        test <@ parsed.Errors = [] @>
        test <@ parsed.ErrorsFor "email" = [] @>

    [<Fact>]
    let ``parsed input retains raw input with path aware diagnostics`` () =
        let raw =
            RawInput.Object(Map.ofList [ "email", RawInput.Scalar "" ])

        let diagnostics =
            Validation.fail (Diagnostics.singleton "Required")
            |> Validation.name "email"
            |> Validation.toResult
            |> function
                | Error diagnostics -> diagnostics
                | Ok _ -> failwith "Expected diagnostics."

        let parsed: ParsedInput<Signup, string> =
            {
                Input = raw
                Result = Error diagnostics
            }

        test <@ parsed.Input = raw @>
        test <@ parsed.Result = Error diagnostics @>
        test <@ Diagnostics.flatten diagnostics = [ { Path = [ PathSegment.Name "email" ]; Error = "Required" } ] @>
        test <@ not parsed.IsValid @>
        test <@ parsed.TryModel = None @>
        raises<System.InvalidOperationException> <@ parsed.Model |> ignore @>

        let expectedErrors =
            [ { Path = [ PathSegment.Name "email" ]; Error = "Required" } ]

        test <@ parsed.Errors = expectedErrors @>
        test <@ parsed.ErrorsFor [ PathSegment.Name "email" ] = [ "Required" ] @>
        test <@ parsed.ErrorsFor "email" = [ "Required" ] @>
        test <@ parsed.ErrorsFor "name" = [] @>

    type private SignupError =
        | MissingField of string

    [<Fact>]
    let ``mapErrors translates a failed parse's errors while preserving input and paths`` () =
        let raw =
            RawInput.Object(Map.ofList [ "email", RawInput.Scalar "" ])

        let diagnostics =
            Validation.fail (Diagnostics.singleton "Required")
            |> Validation.name "email"
            |> Validation.toResult
            |> function
                | Error diagnostics -> diagnostics
                | Ok _ -> failwith "Expected diagnostics."

        let parsed: ParsedInput<Signup, string> = { Input = raw; Result = Error diagnostics }

        let domainParsed = parsed |> ParsedInput.mapErrors MissingField

        test <@ domainParsed.Input = raw @>
        test <@ not domainParsed.IsValid @>

        let expectedErrors =
            [ { Path = [ PathSegment.Name "email" ]; Error = MissingField "Required" } ]

        test <@ domainParsed.Errors = expectedErrors @>

    [<Fact>]
    let ``mapErrors leaves a successful parse's model unchanged`` () =
        let raw =
            RawInput.Object(Map.ofList [ "email", RawInput.Scalar "ada@example.com" ])

        let parsed: ParsedInput<Signup, string> =
            {
                Input = raw
                Result = Ok { Email = "ada@example.com" }
            }

        let domainParsed = parsed |> ParsedInput.mapErrors MissingField

        test <@ domainParsed.IsValid @>
        test <@ domainParsed.Model = { Email = "ada@example.com" } @>
        test <@ domainParsed.Errors = [] @>
