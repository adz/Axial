namespace Axial.Tests

open Axial.Refined
open Axial.Schema
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module RefinedCatalogSchemaTests =
    type private Product =
        {
            Name: NonBlankString
            Slug: Slug
            Quantity: PositiveInt
        }

    type private Tagged =
        {
            Tags: NonEmptyList<Slug>
            Codes: DistinctList<string>
        }

    let private productSchema () =
        Schema.recordFor<Product, _> (fun name slug quantity ->
            {
                Name = name
                Slug = slug
                Quantity = quantity
            })
        |> Schema.field "name" _.Name RefinedSchema.nonBlankString
        |> Schema.field "slug" _.Slug RefinedSchema.slug
        |> Schema.field "quantity" _.Quantity RefinedSchema.positiveInt
        |> Schema.build

    [<Fact>]
    let ``refined catalog schemas parse trusted scalar values`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "slug", RawInput.Scalar "ada-2026"
                      "quantity", RawInput.Scalar "3" ]
            )

        let parsed = Input.parse (productSchema ()) raw

        test
            <@ parsed.Result
               |> Result.map (fun product -> product.Name.Value, product.Slug.Value, product.Quantity.Value) =
                Ok("Ada", "ada-2026", 3) @>

    [<Fact>]
    let ``refined catalog schemas report the same failures as standalone refinement`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "   "
                      "slug", RawInput.Scalar "Ada"
                      "quantity", RawInput.Scalar "0" ]
            )

        let parsed = Input.parse (productSchema ()) raw

        test <@ Refine.nonBlankString "   " |> Result.mapError SchemaError.ofRefinementError = Error [ SchemaError.Required ] @>
        test <@ Refine.slug "Ada" |> Result.mapError SchemaError.ofRefinementError = Error [ SchemaError.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" ] @>
        test <@ Refine.positiveInt 0 |> Result.mapError SchemaError.ofRefinementError = Error [ SchemaError.RangeOutOfRange("greaterThan 0", Some "0") ] @>

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "name" ]; Error = SchemaError.Required }
                                 { Path = [ PathSegment.Name "quantity" ]; Error = SchemaError.RangeOutOfRange("greaterThan 0", Some "0") }
                                 { Path = [ PathSegment.Name "slug" ]; Error = SchemaError.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" } ] @>

    [<Fact>]
    let ``bounded string schema carries caller supplied bounds`` () =
        let schema = RefinedSchema.boundedString 2 4

        test <@ Value.allConstraints schema |> List.map SchemaConstraint.code = [ "required"; "lengthBetween" ] @>

        let check = ValueSchemaCheck.text schema
        let value = Refine.boundedString 2 4 "Ada" |> Result.defaultWith (fun error -> failwithf "%A" error)

        test <@ check value = Ok () @>

    [<Fact>]
    let ``refined collection catalog schemas parse trusted values`` () =
        let schema =
            Schema.recordFor<Tagged, _> (fun tags codes -> { Tags = tags; Codes = codes })
            |> Schema.field "tags" _.Tags (RefinedSchema.nonEmptyList RefinedSchema.slug)
            |> Schema.field "codes" _.Codes (RefinedSchema.distinctList Value.text)
            |> Schema.build

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "tags", RawInput.Many [ RawInput.Scalar "fsharp"; RawInput.Scalar "typed-errors" ]
                      "codes", RawInput.Many [ RawInput.Scalar "A"; RawInput.Scalar "B" ] ]
            )

        let parsed = Input.parse schema raw

        test
            <@ parsed.Result
               |> Result.map (fun value -> value.Tags.ToList() |> List.map _.Value, value.Codes.ToList()) =
                Ok([ "fsharp"; "typed-errors" ], [ "A"; "B" ]) @>

    [<Fact>]
    let ``refined collection catalog schemas report collection and item failures`` () =
        let schema =
            Schema.recordFor<Tagged, _> (fun tags codes -> { Tags = tags; Codes = codes })
            |> Schema.field "tags" _.Tags (RefinedSchema.nonEmptyList RefinedSchema.slug)
            |> Schema.field "codes" _.Codes (RefinedSchema.distinctList Value.text)
            |> Schema.build

        let raw =
            RawInput.Object(
                Map.ofList
                    [ "tags", RawInput.Many []
                      "codes", RawInput.Many [ RawInput.Scalar "A"; RawInput.Scalar "A" ] ]
            )

        let parsed = Input.parse schema raw

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "codes" ]
                                   Error = SchemaError.Custom("seq.distinct", None) }
                                 { Path = [ PathSegment.Name "tags" ]
                                   Error = SchemaError.CountOutOfRange("minCount 1", Some 0) } ] @>
