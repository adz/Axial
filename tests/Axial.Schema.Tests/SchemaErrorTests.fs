namespace Axial.Tests

open Axial.Parse

open Axial

open Axial.Constraint
open Axial.Refined
open Axial.Schema
open Xunit
open Axial.Schema.Syntax
open Axial.Constraint.ConstraintDSL
open Swensen.Unquote

module SchemaErrorTests =
    type private Signup = { Email: string; Age: int }
    type private Ticket = { Priority: int; HasAssignee: bool }

    [<Fact>]
    let ``parse errors lower into schema boundary errors`` () =
        test <@ SchemaError.ofParseError (ParseError.MissingValue "int") = SchemaError.Blank @>
        test <@ SchemaError.ofParseError (ParseError.InvalidFormat("int", "nope")) = SchemaError.InvalidFormat "int" @>
        test <@ SchemaError.ofParseError (ParseError.OutOfRange("int", "999")) = SchemaError.ParseOutOfRange "int" @>

    [<Fact>]
    let ``a constraint failure is carried whole rather than lowered into a parse-shaped case`` () =
        // Lowering would discard the atom and force consumers to reconstruct constraint identity from strings,
        // which is what the unified violation exists to remove.
        let violation = Atomic(Expected(CardinalityAtom(Cardinality.Minimum 3), Some(ConstraintValue.Integer 1L)))
        let error = SchemaError.Violation violation

        test <@ error = SchemaError.Violation violation @>
        test <@ SchemaError.render error = "Expected a size of at least 3, but was 1." @>

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
