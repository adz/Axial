namespace Axial.Tests

open Axial

open Axial.Check

open System
open Axial.Refined
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module RefinedCatalogSchemaTests =
    [<Fact>]
    let ``refinement constraints are exposed to metadata interpreters`` () =
        let schema = RefinedSchemas.nonEmptyList Schema.text
        let description = Inspect.schema schema
        let document = JsonSchema.generateValue schema

        test <@ description.Constraints |> List.map Constraint.code = [ "minLength" ] @>
        test <@ Schema.allConstraints schema |> List.map Constraint.code = [ "minLength" ] @>
        test <@ document.Contains "\"minItems\":1" @>

    [<Fact>]
    let ``refinement constraints execute once`` () =
        let mutable executions = 0
        let check value =
            executions <- executions + 1
            if String.IsNullOrWhiteSpace value then Error [ CheckFailure.Blank ] else Ok ()

        let refinement =
            Refinement.define
                (Axial.Check.Constraint.define "countedNonBlank" [] check)
                id
                id

        let parsed = Schema.parse (Schema.text |> Schema.refine refinement) (Data.Text "value")

        test <@ parsed = Ok "value" @>
        test <@ executions = 1 @>

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
        schema<Product> {
            field "name" _.Name {
                withSchema RefinedSchemas.nonBlankString
            }
            field "slug" _.Slug {
                withSchema RefinedSchemas.slug
            }
            field "quantity" _.Quantity {
                withSchema RefinedSchemas.positiveInt
            }
            construct (fun name slug quantity ->
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

        test <@ Refine.nonBlankString "   " |> Result.mapError (List.map SchemaError.ofCheckFailure) = Error [ SchemaError.Blank ] @>
        test <@ Refine.slug "Ada" |> Result.mapError (List.map SchemaError.ofCheckFailure) = Error [ SchemaError.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" ] @>
        test <@ Refine.positiveInt 0 |> Result.mapError (List.map SchemaError.ofCheckFailure) = Error [ SchemaError.OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") ] @>

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "name" ]; Error = SchemaError.Blank }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "quantity" ]; Error = SchemaError.OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "slug" ]; Error = SchemaError.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" } ] @>

    [<Fact>]
    let ``bounded string schema rejects a value refined under different bounds`` () =
        // BoundedString records its bounds per value, so a BoundedString built at 1..99 is still a BoundedString
        // when checked against a 2..80 schema. Refinement.create does not run for an already-refined value, so the
        // schema's retained constraints must run at the refined layer or the bounds go unenforced.
        let schema = RefinedSchemas.boundedString 2 80
        let tooShort = Refine.boundedString 1 99 "A" |> Result.defaultWith (fun error -> failwithf "%A" error)

        test <@ SchemaCheck.text schema tooShort = Error [ CheckFailure.InvalidLength(CheckLengthExpectation.LengthBetween(2, 80), Some 1) ] @>

    [<Fact>]
    let ``bounded string schema carries caller supplied bounds`` () =
        let schema = RefinedSchemas.boundedString 2 4

        test <@ Schema.allConstraints schema |> List.map Constraint.code = [ "present"; "lengthBetween" ] @>

        let check = SchemaCheck.text schema
        let value = Refine.boundedString 2 4 "Ada" |> Result.defaultWith (fun error -> failwithf "%A" error)

        test <@ check value = Ok () @>

    [<Fact>]
    let ``remaining scalar catalog schemas report the same failures as standalone refinement`` () =
        let schema =
            schema<Scalars> {
                field "command" _.Command {
                    withSchema RefinedSchemas.trimmedString
                }
                field "offset" _.Offset {
                    withSchema RefinedSchemas.nonZeroInt
                }
                construct (fun command offset -> { Command = command; Offset = offset })
            }

        let raw =
            Data.objectOfMap (Map.ofList [ "command", Data.Text " deploy "; "offset", Data.Text "0" ])

        let parsed = Schema.parseRetainingInput schema raw

        test
            <@ Refine.trimmedString " deploy " |> Result.mapError (List.map SchemaError.ofCheckFailure) =
                Error [ SchemaError.InvalidFormat "trimmed" ] @>

        test
            <@ Refine.nonZeroInt 0 |> Result.mapError (List.map SchemaError.ofCheckFailure) =
                Error [ SchemaError.Custom("notEqualTo:0", None) ] @>

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "command" ]; Error = SchemaError.InvalidFormat "trimmed" }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "offset" ]; Error = SchemaError.Custom("notEqualTo:0", None) } ] @>

    [<Fact>]
    let ``refined collection catalog schemas parse trusted values`` () =
        let schema =
            schema<Tagged> {
                field "tags" _.Tags {
                    withSchema (RefinedSchemas.nonEmptyList RefinedSchemas.slug)
                }
                field "codes" _.Codes {
                    withSchema (RefinedSchemas.distinctList Schema.text)
                }
                construct (fun tags codes -> { Tags = tags; Codes = codes })
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
            schema<Tagged> {
                field "tags" _.Tags {
                    withSchema (RefinedSchemas.nonEmptyList RefinedSchemas.slug)
                }
                field "codes" _.Codes {
                    withSchema (RefinedSchemas.distinctList Schema.text)
                }
                construct (fun tags codes -> { Tags = tags; Codes = codes })
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
                                   Error = SchemaError.InvalidLength(CheckLengthExpectation.MinimumLength 1, Some 0) } ] @>

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
                                     SchemaError.ConstructorFailed "failed custom check 'dateTimeOffsetRange'" } ] @>

    [<Fact>]
    let ``date only range schema parses trusted ranges`` () =
        let raw =
            Data.objectOfMap (Map.ofList [ "start", Data.Text "2026-01-01"; "end", Data.Text "2026-01-02" ])

        let parsed = Schema.parseRetainingInput RefinedSchemas.dateOnlyRange raw

        test
            <@ parsed.Result
               |> Result.map (fun range -> range.Start, range.End) =
                Ok(DateOnly(2026, 1, 1), DateOnly(2026, 1, 2)) @>
