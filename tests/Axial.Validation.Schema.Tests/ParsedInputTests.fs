namespace Axial.Tests

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
