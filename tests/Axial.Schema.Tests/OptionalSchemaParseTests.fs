namespace Axial.Tests

open Axial.ErrorHandling
open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit

/// <summary>
/// Covers parsing and validating optional fields declared with <c>Value.optionOf</c>: absent (or JSON null) input is
/// a legal <c>None</c>, present input parses through the payload schema into <c>Some</c>, and payload constraints run
/// only when a value is present.
/// </summary>
module OptionalSchemaParseTests =
    type private Profile =
        { Name: string
          Nickname: string option
          Age: int option }

    let private profileSchema () =
        Schema.recordFor<Profile, _> (fun name nickname age ->
            { Name = name
              Nickname = nickname
              Age = age })
        |> Schema.text "name" _.Name
        |> Schema.field
            "nickname"
            _.Nickname
            (Value.optionOf (Value.text |> Value.withConstraint (SchemaConstraint.minLength 2)))
        |> Schema.field "age" _.Age (Value.optionOf Value.int)
        |> Schema.build

    [<Fact>]
    let ``parse maps missing optional fields to None`` () =
        let raw = RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada" ])

        let parsed = Input.parse (profileSchema ()) raw

        test <@ parsed.Result = Ok { Name = "Ada"; Nickname = None; Age = None } @>

    [<Fact>]
    let ``parse maps json null optional fields to None`` () =
        let raw =
            JsonLikeValue.Object(
                Map.ofList
                    [ "name", JsonLikeValue.String "Ada"
                      "nickname", JsonLikeValue.Null
                      "age", JsonLikeValue.Null ]
            )
            |> RawInput.ofJsonLikeValue

        let parsed = Input.parse (profileSchema ()) raw

        test <@ parsed.Result = Ok { Name = "Ada"; Nickname = None; Age = None } @>

    [<Fact>]
    let ``parse wraps present optional payloads in Some`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "nickname", RawInput.Scalar "Lady A"
                      "age", RawInput.Scalar "36" ]
            )

        let parsed = Input.parse (profileSchema ()) raw

        test <@ parsed.Result = Ok { Name = "Ada"; Nickname = Some "Lady A"; Age = Some 36 } @>

    [<Fact>]
    let ``parse runs payload constraints on present optional values`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "nickname", RawInput.Scalar "A" ]
            )

        let parsed = Input.parse (profileSchema ()) raw

        test <@ not parsed.IsValid @>

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "nickname" ]
                                   Error = SchemaError.InvalidLength(CheckLengthExpectation.MinimumLength 2, Some 1) } ] @>

    [<Fact>]
    let ``validate accepts None optional values`` () =
        let schema = profileSchema ()
        let valid = { Name = "Ada"; Nickname = None; Age = None }

        let validation = Axial.Schema.Validation.validate schema valid

        test <@ Axial.Validation.Validation.toResult validation = Ok valid @>

    [<Fact>]
    let ``validate checks present optional payloads`` () =
        let schema = profileSchema ()
        let invalid = { Name = "Ada"; Nickname = Some "A"; Age = None }

        let validation = Axial.Schema.Validation.validate schema invalid

        test
            <@
                Axial.Validation.Validation.toResult validation =
                    Error
                        {
                            Errors = []
                            Children =
                                Map.ofList
                                    [ PathSegment.Name "nickname",
                                      Diagnostics.singleton (SchemaError.InvalidLength(CheckLengthExpectation.MinimumLength 2, Some 1)) ]
                        }
            @>
