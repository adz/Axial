namespace Axial.Tests

open Axial.Constraint
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

[<RequireQualifiedAccess>]
module private CeEmail =
    let refinement = Refinement.define (Constraint.pattern ".+@.+") CeEmail _.Value

type CeEmail with
    static member Refinement(_: string, _: CeEmail) = CeEmail.refinement

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
        schema<CeSignup> {
            field "email" _.Email {
                withSchema Schema.text
                constrain (Constraint.minLength 3)
                refine CeEmail.refinement
                validate validateCompanyEmail
            }

            field "age" _.Age
            construct CeSignup.create
        }

    let private checkedSignupSchema =
        schema<CeSignup> {
            field "email" _.Email {
                withSchema Schema.text
                refine
            }

            field "age" _.Age {
                withSchema Schema.int
                constrain (Constraint.atLeast 0)
            }

            constructResult CeSignup.createChecked
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
    let ``defaultValue composes after an explicit field schema`` () =
        let schema =
            schema<CeSignup> {
                field "email" _.Email {
                    withSchema Schema.text
                    refine
                }

                field "age" _.Age {
                    withSchema Schema.int
                    defaultValue 18
                }

                construct CeSignup.create
            }

        let input = Data.Object [ "email", Data.Text "ada@example.com" ]

        test <@ Schema.parse schema input |> Result.map _.Age = Ok 18 @>

    [<Fact>]
    let ``field metadata operations compose with inferred and explicit schemas`` () =
        let schema =
            schema<CeSignup> {
                field "email" _.Email {
                    withSchema Schema.text
                    describe "Primary contact email."
                    format SchemaFormat.email
                    refine
                }

                field "age" _.Age {
                    describe "Age in years."
                    defaultValue 18
                }

                construct CeSignup.create
            }

        let fields = (Inspect.model schema).Fields
        let email = fields |> List.find (fun field -> field.Name = "email")
        let age = fields |> List.find (fun field -> field.Name = "age")

        let emailRaw =
            match email.Schema.Shape with
            | SchemaShape.Refined raw -> raw
            | shape -> failwithf "Expected refined email schema; got %A" shape

        test <@ emailRaw.Description = Some "Primary contact email." @>
        test <@ emailRaw.Format = Some SchemaFormat.email @>
        test <@ age.Schema.Description = Some "Age in years." @>
        test <@ age.Schema.Default = Some(box 18) @>

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
            |> Result.mapError (SchemaErrors.toList >> List.map (fun issue -> issue.Path, issue.Error))

        let checkErrors =
            checkResult
            |> Result.mapError (SchemaErrors.toList >> List.map (fun issue -> issue.Path, issue.Error))

        let parseIssues =
            match parseErrors with
            | Error issues -> issues
            | Ok _ -> []

        let checkIssues =
            match checkErrors with
            | Error issues -> issues
            | Ok _ -> []

        let expected =
            [ Path.key "email",
              SchemaError.Custom("company-email", Some "Expected an example.com address.") ]

        test <@ parseIssues = expected @>
        test <@ checkIssues = expected @>
