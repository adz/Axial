namespace Axial.Tests

open Axial

open Axial.ErrorHandling

open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module SchemaDiagnosticsRenderingTests =
    type private Signup = { Email: string; Age: int }

    let private signupSchema =
        schema<Signup> {
            field "email" _.Email {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            field "age" _.Age
            construct (fun email age -> { Email = email; Age = age })
        }

    [<Fact>]
    let ``toString renders a field name path for a single failing field`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "email", Data.Null; "age", Data.Text "42" ])

        let parsed = Schema.parseRetainingInput signupSchema raw

        let text =
            match parsed.Result with
            | Error diagnostics -> SchemaErrors.toString diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        test <@ text = "email: This value is required." @>

    [<Fact>]
    let ``toString renders sibling field paths for every failing field`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Null
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput signupSchema raw

        let text =
            match parsed.Result with
            | Error diagnostics -> SchemaErrors.toString diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        test <@ text.Contains("email: This value is required.") @>
        test <@ text.Contains("age: Expected int format.") @>

    [<Fact>]
    let ``toString renders a root path error without a nested branch when input is not an object`` () =
        let raw = Data.Text "not-an-object"
        let parsed = Schema.parseRetainingInput signupSchema raw

        let text =
            match parsed.Result with
            | Error diagnostics -> SchemaErrors.toString diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        test <@ text = "Expected an object." @>

    [<Fact>]
    let ``flatten preserves one diagnostic per failing field with its own path`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Null
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput signupSchema raw

        let flattened =
            match parsed.Result with
            | Error diagnostics -> SchemaErrors.toList diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        let expected =
            [ { Path = Path.key "age"; Error = SchemaError.InvalidFormat "int" }
              { Path = Path.key "email"; Error = SchemaError.Required } ]

        test <@ flattened = expected @>
        test <@ parsed.Errors = expected @>

    [<Fact>]
    let ``flatten reports an empty path for a root-level schema error`` () =
        let raw = Data.List [ Data.Text "1" ]
        let parsed = Schema.parseRetainingInput signupSchema raw

        let flattened =
            match parsed.Result with
            | Error diagnostics -> SchemaErrors.toList diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        test <@ flattened = [ { Path = Path.root; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``flatten and toString agree on which fields failed`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "   "
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput signupSchema raw

        match parsed.Result with
        | Error diagnostics ->
            let flattened = SchemaErrors.toList diagnostics
            let text = SchemaErrors.toString diagnostics

            let flattenedFieldNames =
                flattened |> List.map (fun issue -> Path.format issue.Path) |> List.distinct |> List.sort

            test <@ flattenedFieldNames = [ "age"; "email" ] @>
            test <@ text.Contains("email:") @>
            test <@ text.Contains("age:") @>
        | Ok _ -> failwith "Expected a failed parse."
