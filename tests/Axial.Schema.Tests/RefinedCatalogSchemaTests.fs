namespace Axial.Tests

open Axial.ErrorHandling

open System
open Axial.Refined
open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit

module RefinedCatalogSchemaTests =
    type private Product =
        {
            Name: NonBlankString
            Slug: Slug
            Quantity: PositiveInt
        }

    type private Scalars =
        {
            Command: TrimmedString
            Offset: NonZeroInt
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
        test <@ Refine.positiveInt 0 |> Result.mapError SchemaError.ofRefinementError = Error [ SchemaError.OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") ] @>

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "name" ]; Error = SchemaError.Required }
                                 { Path = [ PathSegment.Name "quantity" ]; Error = SchemaError.OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") }
                                 { Path = [ PathSegment.Name "slug" ]; Error = SchemaError.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" } ] @>

    [<Fact>]
    let ``bounded string schema carries caller supplied bounds`` () =
        let schema = RefinedSchema.boundedString 2 4

        test <@ Value.allConstraints schema |> List.map SchemaConstraint.code = [ "required"; "lengthBetween" ] @>

        let check = ValueSchemaCheck.text schema
        let value = Refine.boundedString 2 4 "Ada" |> Result.defaultWith (fun error -> failwithf "%A" error)

        test <@ check value = Ok value @>

    [<Fact>]
    let ``remaining scalar catalog schemas report the same failures as standalone refinement`` () =
        let schema =
            Schema.recordFor<Scalars, _> (fun command offset -> { Command = command; Offset = offset })
            |> Schema.field "command" _.Command RefinedSchema.trimmedString
            |> Schema.field "offset" _.Offset RefinedSchema.nonZeroInt
            |> Schema.build

        let raw =
            RawInput.Object(Map.ofList [ "command", RawInput.Scalar " deploy "; "offset", RawInput.Scalar "0" ])

        let parsed = Input.parse schema raw

        test
            <@ Refine.trimmedString " deploy " |> Result.mapError SchemaError.ofRefinementError =
                Error [ SchemaError.InvalidFormat "trimmed" ] @>

        test
            <@ Refine.nonZeroInt 0 |> Result.mapError SchemaError.ofRefinementError =
                Error [ SchemaError.Custom("notEqualTo:0", None) ] @>

        test
            <@ parsed.Errors = [ { Path = [ PathSegment.Name "command" ]; Error = SchemaError.InvalidFormat "trimmed" }
                                 { Path = [ PathSegment.Name "offset" ]; Error = SchemaError.Custom("notEqualTo:0", None) } ] @>

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
                                   Error = SchemaError.Duplicate }
                                 { Path = [ PathSegment.Name "tags" ]
                                   Error = SchemaError.InvalidCount(CheckCountExpectation.MinimumCount 1, Some 0) } ] @>

    [<Fact>]
    let ``date time range schema parses trusted ranges`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "start", RawInput.Scalar "2026-01-01T00:00:00+00:00"
                      "end", RawInput.Scalar "2026-01-02T00:00:00+00:00" ]
            )

        let parsed = Input.parse RefinedSchema.dateTimeOffsetRange raw

        test
            <@ parsed.Result
               |> Result.map (fun range -> range.Start, range.End) =
                Ok(
                    DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
                ) @>

    [<Fact>]
    let ``date time range schema reports constructor failures after fields parse`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "start", RawInput.Scalar "2026-01-02T00:00:00+00:00"
                      "end", RawInput.Scalar "2026-01-01T00:00:00+00:00" ]
            )

        let parsed = Input.parse RefinedSchema.dateTimeOffsetRange raw

        test
            <@ parsed.Errors = [ { Path = []
                                   Error =
                                     SchemaError.ConstructorFailed
                                         "DateTimeOffsetRange: Expected Start to be less than or equal to End." } ] @>

    [<Fact>]
    let ``date only range schema parses trusted ranges`` () =
        let raw =
            RawInput.Object(Map.ofList [ "start", RawInput.Scalar "2026-01-01"; "end", RawInput.Scalar "2026-01-02" ])

        let parsed = Input.parse RefinedSchema.dateOnlyRange raw

        test
            <@ parsed.Result
               |> Result.map (fun range -> range.Start, range.End) =
                Ok(DateOnly(2026, 1, 1), DateOnly(2026, 1, 2)) @>
