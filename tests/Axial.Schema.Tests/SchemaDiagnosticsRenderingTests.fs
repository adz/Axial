namespace Axial.Tests

open Axial

open Axial.ErrorHandling

open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module SchemaDiagnosticsRenderingTests =
    type private Signup = { Email: string; Age: int }

    let private schema =
        SchemaCE.schema<Signup> {
            SchemaCE.field "email" _.Email {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "age" _.Age
            SchemaCE.construct (fun email age -> { Email = email; Age = age })
        }

    [<Fact>]
    let ``toString renders a field name path for a single failing field`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "email", Data.Null; "age", Data.Text "42" ])

        let parsed = Schema.parseRetainingInput schema raw

        let text =
            match parsed.Result with
            | Error diagnostics -> Diagnostics.toString diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        test <@ text.Contains("email:") @>
        test <@ text.Contains($"- {string SchemaError.Required}") @>
        test <@ text.Contains("Errors:") |> not @>

    [<Fact>]
    let ``toString renders sibling field paths for every failing field`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Null
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        let text =
            match parsed.Result with
            | Error diagnostics -> Diagnostics.toString diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        test <@ text.Contains("email:") @>
        test <@ text.Contains("age:") @>
        let invalidAge = string (SchemaError.InvalidFormat "int")
        test <@ text.Contains($"- {string SchemaError.Required}") @>
        test <@ text.Contains($"- {invalidAge}") @>

    [<Fact>]
    let ``toString renders a root path error without a nested branch when input is not an object`` () =
        let raw = Data.Text "not-an-object"
        let parsed = Schema.parseRetainingInput schema raw

        let text =
            match parsed.Result with
            | Error diagnostics -> Diagnostics.toString diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        let expectedObject = string SchemaError.ExpectedObject
        test <@ text.Contains($"- {expectedObject}") @>
        test <@ text.Contains(":") |> not @>

    [<Fact>]
    let ``flatten preserves one diagnostic per failing field with its own path`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Null
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        let flattened =
            match parsed.Result with
            | Error diagnostics -> Diagnostics.flatten diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        let expected =
            [ { Path = [ PathSegment.Name "age" ]; Error = SchemaError.InvalidFormat "int" }
              { Path = [ PathSegment.Name "email" ]; Error = SchemaError.Required } ]

        test <@ flattened = expected @>
        test <@ parsed.Errors = expected @>

    [<Fact>]
    let ``flatten reports an empty path for a root-level schema error`` () =
        let raw = Data.List [ Data.Text "1" ]
        let parsed = Schema.parseRetainingInput schema raw

        let flattened =
            match parsed.Result with
            | Error diagnostics -> Diagnostics.flatten diagnostics
            | Ok _ -> failwith "Expected a failed parse."

        test <@ flattened = [ { Path = []; Error = SchemaError.ExpectedObject } ] @>

    [<Fact>]
    let ``flatten and toString agree on which fields failed`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "   "
                      "age", Data.Text "not-an-int" ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        match parsed.Result with
        | Error diagnostics ->
            let flattened = Diagnostics.flatten diagnostics
            let text = Diagnostics.toString diagnostics

            let flattenedFieldNames =
                flattened
                |> List.collect (fun diagnostic -> diagnostic.Path)
                |> List.choose (function
                    | PathSegment.Name name -> Some name
                    | _ -> None)
                |> List.distinct
                |> List.sort

            test <@ flattenedFieldNames = [ "age"; "email" ] @>
            test <@ text.Contains("email:") @>
            test <@ text.Contains("age:") @>
        | Ok _ -> failwith "Expected a failed parse."
