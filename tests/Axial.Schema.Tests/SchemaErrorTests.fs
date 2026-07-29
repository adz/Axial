namespace Axial.Tests

open Axial.Parse

open Axial

open Axial.Check
open Axial.Refined
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module SchemaErrorTests =
    type private Signup = { Email: string; Age: int }
    type private Ticket = { Priority: int; HasAssignee: bool }

    [<Fact>]
    let ``parse errors lower into schema boundary errors`` () =
        test <@ SchemaError.ofParseError (ParseError.MissingValue "int") = SchemaError.Blank @>
        test <@ SchemaError.ofParseError (ParseError.InvalidFormat("int", "nope")) = SchemaError.InvalidFormat "int" @>
        test <@ SchemaError.ofParseError (ParseError.OutOfRange("int", "999")) = SchemaError.ParseOutOfRange "int" @>

    [<Fact>]
    let ``check failures lower into schema boundary errors`` () =
        test <@ SchemaError.ofCheckFailure CheckFailure.Blank = SchemaError.Blank @>
        test <@ SchemaError.ofCheckFailure (CheckFailure.InvalidFormat "email") = SchemaError.InvalidFormat "email" @>

        let lengthError =
            SchemaError.ofCheckFailure (CheckFailure.InvalidLength(CheckLengthExpectation.MinimumLength 3, Some 1))

        test <@ lengthError = SchemaError.InvalidLength(CheckLengthExpectation.MinimumLength 3, Some 1) @>

    [<Fact>]
    let ``schema boundary errors render default English messages`` () =
        test <@ SchemaError.render SchemaError.Blank = "This value must be present." @>
        test <@ SchemaError.render (SchemaError.InvalidFormat "email") = "Expected email format." @>
        test <@ SchemaError.render (SchemaError.Custom("signup.blocked", Some "Signup is closed.")) = "Signup is closed." @>

    [<Fact>]
    let ``parsed input renders failed parse diagnostics with paths`` () =
        let schema =
            schema<Signup> {
                field "email" _.Email {
                    withSchema (Schema.text |> Schema.constrain Constraint.present)
                }
                field "age" _.Age
                construct (fun email age -> { Email = email; Age = age })
            }

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Null
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test
            <@ RetainedParseResult.renderErrors parsed = [ "age: Expected int format."; "email: This value must be present." ] @>

    [<Fact>]
    let ``schema issues can be mapped into application errors after parsing`` () =
        let schema =
            schema<Signup> {
                field "email" _.Email {
                    withSchema (Schema.text |> Schema.constrain Constraint.present)
                }
                field "age" _.Age
                construct (fun email age -> { Email = email; Age = age })
            }

        let result =
            Data.objectOfMap (Map.ofList [ "email", Data.Null; "age", Data.Text "42" ])
            |> Schema.parse schema

        let applicationErrors =
            match result with
            | Ok _ -> []
            | Error errors -> errors |> SchemaErrors.toList |> List.map (fun issue -> issue.Error)

        test <@ applicationErrors = [ SchemaError.Blank ] @>
