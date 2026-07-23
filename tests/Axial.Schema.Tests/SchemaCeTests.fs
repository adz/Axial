namespace Axial.Tests

open Axial
open Axial.Refined
open Axial.Schema
open Axial.Schema.Json
open Swensen.Unquote
open Xunit

type CeEmail =
    private
    | CeEmail of string

    member this.Value =
        let (CeEmail value) = this
        value

    static member Refinement(_: string, _: CeEmail) =
        Refinement.define
            (fun (raw: string) ->
                if raw.Contains "@" then
                    Ok(CeEmail raw)
                else
                    Error(RefinementError.InvalidStructure("email", "Expected an email address.")))
            _.Value

[<RequireQualifiedAccess>]
module private CeEmail =
    let create (raw: string) =
        if raw.Contains "@" then
            Ok(CeEmail raw)
        else
            Error(RefinementError.InvalidStructure("email", "Expected an email address."))

type CeSignup =
    {
        Email: CeEmail
        Age: int
    }

[<RequireQualifiedAccess>]
module private CeSignup =
    let create email age =
        { Email = email; Age = age }

    let createChecked email age =
        if age >= 18 then
            Ok(create email age)
        else
            Error "Expected an adult signup."

module SchemaCeTests =
    let private validateCompanyEmail (email: CeEmail) =
        if email.Value.EndsWith("@example.com") then
            Ok()
        else
            Error(SchemaError.Custom("company-email", Some "Expected an example.com address."))

    let private signupSchema =
        SchemaCE.schema<CeSignup> {
            SchemaCE.field "email" _.Email {
                withSchema Schema.text
                constrain (Syntax.minLength 3)
                refine
                validate validateCompanyEmail
            }

            SchemaCE.field "age" _.Age
            SchemaCE.construct CeSignup.create
        }

    let private checkedSignupSchema =
        SchemaCE.schema<CeSignup> {
            SchemaCE.field "email" _.Email {
                withSchema Schema.text
                refine
            }

            SchemaCE.field "age" _.Age {
                withSchema Schema.int
                constrain (Syntax.atLeast 0)
            }

            SchemaCE.constructResult CeSignup.createChecked
        }

    let private input age =
        Data.Object
            [ "email", Data.Text "ada@example.com"
              "age", Data.Number(string age) ]

    let private inputWithEmail email age =
        Data.Object
            [ "email", Data.Text email
              "age", Data.Number(string age) ]

    [<Fact>]
    let ``schema CE parses fields with default explicit and refined schemas`` () =
        let parsed = Schema.parse signupSchema (input 42)

        test <@ parsed |> Result.map (fun value -> value.Email.Value, value.Age) = Ok("ada@example.com", 42) @>

    [<Fact>]
    let ``schema CE retains the typed compiled JSON plan`` () =
        let signup = CeSignup.create (CeEmail "ada@example.com") 42
        let codec = Json.compile signupSchema
        let encoded = Json.serialize codec signup
        let decoded = Json.deserialize codec encoded

        test <@ encoded = """{"email":"ada@example.com","age":42}""" @>
        test <@ decoded = signup @>

    [<Fact>]
    let ``schema CE supports checked constructors on the same field chain`` () =
        let accepted = Schema.parse checkedSignupSchema (input 20)
        let rejected = Schema.parse checkedSignupSchema (input 17)

        test <@ accepted |> Result.map _.Age = Ok 20 @>
        test <@ rejected |> Result.isError @>

    [<Fact>]
    let ``field validation runs after refinement during parse and check`` () =
        let parseResult = Schema.parse signupSchema (inputWithEmail "ada@other.test" 42)
        let checkResult = Schema.check signupSchema (CeSignup.create (CeEmail "ada@other.test") 42)

        let parseErrors =
            parseResult
            |> Result.mapError (Axial.Validation.Diagnostics.flatten >> List.map (fun issue -> issue.Path, issue.Error))

        let checkErrors =
            checkResult
            |> Result.mapError (Axial.Validation.Diagnostics.flatten >> List.map (fun issue -> issue.Path, issue.Error))

        let parseIssues =
            match parseErrors with
            | Error issues -> issues
            | Ok _ -> []

        let checkIssues =
            match checkErrors with
            | Error issues -> issues
            | Ok _ -> []

        let expected =
            [ [ Axial.Validation.PathSegment.Name "email" ],
              SchemaError.Custom("company-email", Some "Expected an example.com address.") ]

        test <@ parseIssues = expected @>
        test <@ checkIssues = expected @>
