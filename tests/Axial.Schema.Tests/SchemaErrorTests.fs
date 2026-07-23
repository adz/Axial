namespace Axial.Tests

open Axial

open Axial.ErrorHandling
open Axial.Refined
open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module SchemaErrorTests =
    type private Signup = { Email: string; Age: int }
    type private SignupError = Boundary of SchemaError
    type private Ticket = { Priority: int; HasAssignee: bool }

    [<Fact>]
    let ``parse errors lower into schema boundary errors`` () =
        test <@ SchemaError.ofParseError (ParseError.MissingValue "int") = SchemaError.Required @>
        test <@ SchemaError.ofParseError (ParseError.InvalidFormat("int", "nope")) = SchemaError.InvalidFormat "int" @>
        test <@ SchemaError.ofParseError (ParseError.OutOfRange("int", "999")) = SchemaError.ParseOutOfRange "int" @>

    [<Fact>]
    let ``check failures lower into schema boundary errors`` () =
        test <@ SchemaError.ofCheckFailure CheckFailure.Required = SchemaError.Required @>
        test <@ SchemaError.ofCheckFailure (CheckFailure.InvalidFormat "email") = SchemaError.InvalidFormat "email" @>

        let lengthError =
            SchemaError.ofCheckFailure (CheckFailure.InvalidLength(CheckLengthExpectation.MinimumLength 3, Some 1))

        test <@ lengthError = SchemaError.InvalidLength(CheckLengthExpectation.MinimumLength 3, Some 1) @>

    [<Fact>]
    let ``refinement errors lower into schema boundary errors`` () =
        let parseError = RefinementError.ParseFailed(ParseError.InvalidFormat("int", "nope"))
        let checkError = RefinementError.CheckFailed("Email", [ CheckFailure.Required; CheckFailure.InvalidFormat "email" ])
        let structureError = RefinementError.InvalidStructure("DateRange", "End must be on or after start.")

        test <@ SchemaError.ofRefinementError parseError = [ SchemaError.InvalidFormat "int" ] @>
        test <@ SchemaError.ofRefinementError checkError = [ SchemaError.Required; SchemaError.InvalidFormat "email" ] @>
        test <@ SchemaError.ofRefinementError structureError = [ SchemaError.Custom("DateRange", Some "End must be on or after start.") ] @>

    [<Fact>]
    let ``schema boundary errors render default English messages`` () =
        test <@ SchemaError.render SchemaError.Required = "This value is required." @>
        test <@ SchemaError.render (SchemaError.InvalidFormat "email") = "Expected email format." @>
        test <@ SchemaError.render (SchemaError.Custom("signup.blocked", Some "Signup is closed.")) = "Signup is closed." @>

    [<Fact>]
    let ``parsed input renders failed parse diagnostics with paths`` () =
        let schema =
            SchemaCE.schema<Signup> {
                SchemaCE.field "email" _.Email {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                SchemaCE.field "age" _.Age
                SchemaCE.construct (fun email age -> { Email = email; Age = age })
            }

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Null
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test
            <@ RetainedParseResult.renderErrors parsed = [ "age: Expected int format."; "email: This value is required." ] @>

    [<Fact>]
    let ``parsed input maps schema boundary errors to user owned errors with one function`` () =
        let schema =
            SchemaCE.schema<Signup> {
                SchemaCE.field "email" _.Email {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                SchemaCE.field "age" _.Age
                SchemaCE.construct (fun email age -> { Email = email; Age = age })
            }

        let parsed =
            Data.objectOfMap (Map.ofList [ "email", Data.Null; "age", Data.Text "42" ])
            |> Schema.parseRetainingInput schema
            |> RetainedParseResult.mapErrors Boundary

        test <@ parsed.ErrorsFor "email" = [ Boundary SchemaError.Required ] @>
