namespace Axial.Tests

open Axial

open Axial.ErrorHandling

open System
open Axial.Refined
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

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
        SchemaCE.schema<Product> {
            SchemaCE.field "name" _.Name {
                withSchema RefinedSchemas.nonBlankString
            }
            SchemaCE.field "slug" _.Slug {
                withSchema RefinedSchemas.slug
            }
            SchemaCE.field "quantity" _.Quantity {
                withSchema RefinedSchemas.positiveInt
            }
            SchemaCE.construct (fun name slug quantity ->
                { Name = name
                  Slug = slug
                  Quantity = quantity })
        }

    [<Fact>]
    let ``refined catalog schemas parse trusted scalar values`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "slug", Data.Text "ada-2026"
                      "quantity", Data.Text "3" ]
            )

        let parsed = Schema.parseRetainingInput (productSchema ()) raw

        test
            <@ parsed.Result
               |> Result.map (fun product -> product.Name.Value, product.Slug.Value, product.Quantity.Value) =
                Ok("Ada", "ada-2026", 3) @>

    [<Fact>]
    let ``refined catalog schemas report the same failures as standalone refinement`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "   "
                      "slug", Data.Text "Ada"
                      "quantity", Data.Text "0" ]
            )

        let parsed = Schema.parseRetainingInput (productSchema ()) raw

        test <@ Refine.nonBlankString "   " |> Result.mapError SchemaError.ofRefinementError = Error [ SchemaError.Required ] @>
        test <@ Refine.slug "Ada" |> Result.mapError SchemaError.ofRefinementError = Error [ SchemaError.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" ] @>
        test <@ Refine.positiveInt 0 |> Result.mapError SchemaError.ofRefinementError = Error [ SchemaError.OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") ] @>

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "name" ]; Error = SchemaError.Required }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "quantity" ]; Error = SchemaError.OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "slug" ]; Error = SchemaError.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" } ] @>

    [<Fact>]
    let ``bounded string schema carries caller supplied bounds`` () =
        let schema = RefinedSchemas.boundedString 2 4

        test <@ Schema.allConstraints schema |> List.map Constraint.code = [ "required"; "lengthBetween" ] @>

        let check = SchemaCheck.text schema
        let value = Refine.boundedString 2 4 "Ada" |> Result.defaultWith (fun error -> failwithf "%A" error)

        test <@ check value = Ok value @>

    [<Fact>]
    let ``remaining scalar catalog schemas report the same failures as standalone refinement`` () =
        let schema =
            SchemaCE.schema<Scalars> {
                SchemaCE.field "command" _.Command {
                    withSchema RefinedSchemas.trimmedString
                }
                SchemaCE.field "offset" _.Offset {
                    withSchema RefinedSchemas.nonZeroInt
                }
                SchemaCE.construct (fun command offset -> { Command = command; Offset = offset })
            }

        let raw =
            Data.objectOfMap (Map.ofList [ "command", Data.Text " deploy "; "offset", Data.Text "0" ])

        let parsed = Schema.parseRetainingInput schema raw

        test
            <@ Refine.trimmedString " deploy " |> Result.mapError SchemaError.ofRefinementError =
                Error [ SchemaError.InvalidFormat "trimmed" ] @>

        test
            <@ Refine.nonZeroInt 0 |> Result.mapError SchemaError.ofRefinementError =
                Error [ SchemaError.Custom("notEqualTo:0", None) ] @>

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "command" ]; Error = SchemaError.InvalidFormat "trimmed" }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "offset" ]; Error = SchemaError.Custom("notEqualTo:0", None) } ] @>

    [<Fact>]
    let ``refined collection catalog schemas parse trusted values`` () =
        let schema =
            SchemaCE.schema<Tagged> {
                SchemaCE.field "tags" _.Tags {
                    withSchema (RefinedSchemas.nonEmptyList RefinedSchemas.slug)
                }
                SchemaCE.field "codes" _.Codes {
                    withSchema (RefinedSchemas.distinctList Schema.text)
                }
                SchemaCE.construct (fun tags codes -> { Tags = tags; Codes = codes })
            }

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "tags", Data.List [ Data.Text "fsharp"; Data.Text "typed-errors" ]
                      "codes", Data.List [ Data.Text "A"; Data.Text "B" ] ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test
            <@ parsed.Result
               |> Result.map (fun value -> value.Tags.ToList() |> List.map _.Value, value.Codes.ToList()) =
                Ok([ "fsharp"; "typed-errors" ], [ "A"; "B" ]) @>

    [<Fact>]
    let ``refined collection catalog schemas report collection and item failures`` () =
        let schema =
            SchemaCE.schema<Tagged> {
                SchemaCE.field "tags" _.Tags {
                    withSchema (RefinedSchemas.nonEmptyList RefinedSchemas.slug)
                }
                SchemaCE.field "codes" _.Codes {
                    withSchema (RefinedSchemas.distinctList Schema.text)
                }
                SchemaCE.construct (fun tags codes -> { Tags = tags; Codes = codes })
            }

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "tags", Data.List []
                      "codes", Data.List [ Data.Text "A"; Data.Text "A" ] ]
            )

        let parsed = Schema.parseRetainingInput schema raw

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "codes" ]
                                   Error = SchemaError.Duplicate }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "tags" ]
                                   Error = SchemaError.InvalidCount(CheckCountExpectation.MinimumCount 1, Some 0) } ] @>

    [<Fact>]
    let ``date time range schema parses trusted ranges`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "2026-01-01T00:00:00+00:00"
                      "end", Data.Text "2026-01-02T00:00:00+00:00" ]
            )

        let parsed = Schema.parseRetainingInput RefinedSchemas.dateTimeOffsetRange raw

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
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "2026-01-02T00:00:00+00:00"
                      "end", Data.Text "2026-01-01T00:00:00+00:00" ]
            )

        let parsed = Schema.parseRetainingInput RefinedSchemas.dateTimeOffsetRange raw

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy []
                                   Error =
                                     SchemaError.ConstructorFailed
                                         "DateTimeOffsetRange: Expected Start to be less than or equal to End." } ] @>

    [<Fact>]
    let ``date only range schema parses trusted ranges`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "start", Data.Text "2026-01-01"; "end", Data.Text "2026-01-02" ])

        let parsed = Schema.parseRetainingInput RefinedSchemas.dateOnlyRange raw

        test
            <@ parsed.Result
               |> Result.map (fun range -> range.Start, range.End) =
                Ok(DateOnly(2026, 1, 1), DateOnly(2026, 1, 2)) @>
