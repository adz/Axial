namespace Axial.Tests

open Axial
open Axial.Schema
open Axial.Schema.Syntax
open Swensen.Unquote
open Xunit

module RetainedParseResultTests =
    type private Signup = { Email: string }

    let private schema () =
        schema<Signup> {
            field "email" _.Email {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }

            construct (fun email -> { Email = email })
        }

    [<Fact>]
    let ``retained parse exposes its input and successful value`` () =
        let raw = Data.objectOfMap (Map.ofList [ "email", Data.Text "ada@example.com" ])
        let parsed = Schema.parseRetainingInput (schema ()) raw

        test <@ parsed.Input = raw @>
        test <@ parsed.Result = Ok { Email = "ada@example.com" } @>
        test <@ parsed.IsValid @>
        test <@ parsed.Value = { Email = "ada@example.com" } @>
        test <@ parsed.TryValue = Some { Email = "ada@example.com" } @>
        test <@ parsed.Errors = [] @>

    [<Fact>]
    let ``retained parse exposes schema issues and errors at a path`` () =
        let raw = Data.objectOfMap (Map.ofList [ "email", Data.Text "" ])
        let parsed = Schema.parseRetainingInput (schema ()) raw
        let emailPath = Path.key "email"

        test <@ parsed.Input = raw @>
        test <@ not parsed.IsValid @>
        test <@ parsed.TryValue = None @>
        raises<System.InvalidOperationException> <@ parsed.Value |> ignore @>
        test <@ parsed.Errors = [ { Path = emailPath; Error = SchemaError.Required } ] @>
        test <@ parsed.ErrorsFor emailPath = [ SchemaError.Required ] @>
        test <@ parsed.ErrorsFor "email" = [ SchemaError.Required ] @>
        test <@ parsed.ErrorsFor "name" = [] @>

    [<Fact>]
    let ``retained parse renders schema errors with paths`` () =
        let parsed =
            Data.objectOfMap (Map.ofList [ "email", Data.Text "" ])
            |> Schema.parseRetainingInput (schema ())

        test <@ RetainedParseResult.renderErrors parsed = [ "email: This value is required." ] @>
