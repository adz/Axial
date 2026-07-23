namespace Axial.Tests

open Axial

open Axial.ErrorHandling
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>
/// Covers parsing and validating optional fields declared with <c>Schema.option</c>: absent (or JSON null) input is
/// a legal <c>None</c>, present input parses through the payload schema into <c>Some</c>, and payload constraints run
/// only when a value is present.
/// </summary>
module OptionalSchemaParseTests =
    type private Profile =
        { Name: string
          Nickname: string option
          Age: int option }

    let private profileSchema () =
        schema<Profile> {
            field "name" _.Name
            field "nickname" _.Nickname {
                withSchema (Schema.option (Schema.text |> Schema.constrain (Constraint.minLength 2)))
            }
            field "age" _.Age {
                withSchema (Schema.option Schema.int)
            }
            construct (fun name nickname age ->
                { Name = name
                  Nickname = nickname
                  Age = age })
        }

    [<Fact>]
    let ``parse maps missing optional fields to None`` () =
        let raw = Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada" ])

        let parsed = Schema.parseRetainingInput (profileSchema ()) raw

        test <@ parsed.Result = Ok { Name = "Ada"; Nickname = None; Age = None } @>

    [<Fact>]
    let ``parse maps json null optional fields to None`` () =
        let raw =
            Data.Object
                [ "name", Data.Text "Ada"
                  "nickname", Data.Null
                  "age", Data.Null ]
        let parsed = Schema.parseRetainingInput (profileSchema ()) raw

        test <@ parsed.Result = Ok { Name = "Ada"; Nickname = None; Age = None } @>

    [<Fact>]
    let ``parse wraps present optional payloads in Some`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "nickname", Data.Text "Lady A"
                      "age", Data.Text "36" ]
            )

        let parsed = Schema.parseRetainingInput (profileSchema ()) raw

        test <@ parsed.Result = Ok { Name = "Ada"; Nickname = Some "Lady A"; Age = Some 36 } @>

    [<Fact>]
    let ``parse runs payload constraints on present optional values`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "nickname", Data.Text "A" ]
            )

        let parsed = Schema.parseRetainingInput (profileSchema ()) raw

        test <@ not parsed.IsValid @>

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "nickname" ]
                                   Error = SchemaError.InvalidLength(CheckLengthExpectation.MinimumLength 2, Some 1) } ] @>

    [<Fact>]
    let ``validate accepts None optional values`` () =
        let schema = profileSchema ()
        let valid = { Name = "Ada"; Nickname = None; Age = None }

        let validation = Schema.check schema valid

        test <@ validation = Ok valid @>

    [<Fact>]
    let ``validate checks present optional payloads`` () =
        let schema = profileSchema ()
        let invalid = { Name = "Ada"; Nickname = Some "A"; Age = None }

        let validation = Schema.check schema invalid

        let issues = validation |> Result.mapError SchemaErrors.toList

        test
            <@
                issues =
                    Error
                        [ { Path = Path.key "nickname"
                            Error = SchemaError.InvalidLength(CheckLengthExpectation.MinimumLength 2, Some 1) } ]
            @>
