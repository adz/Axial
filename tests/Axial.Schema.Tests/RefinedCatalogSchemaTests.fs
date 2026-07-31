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

    // Concepts removed from the catalogue are expressed as constraints over a primitive.
    let private slugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$"

    let private slugSchema () =
        Schema.text
        |> Schema.constrainAll [ Axial.Schema.Constraint.present; Axial.Schema.Constraint.pattern slugPattern ]

    let private trimmedSchema () =
        Schema.text |> Schema.constrain Axial.Schema.Constraint.trimmed

    let private boundedSchema minLength maxLength =
        Schema.text
        |> Schema.constrainAll
            [ Axial.Schema.Constraint.present; Axial.Schema.Constraint.lengthBetween minLength maxLength ]

    type private Product =
        {
            Name: NonBlankString
            Slug: string
            Quantity: int
        }

    type private Scalars =
        {
            Command: string
            Offset: int
        }

    type private Tagged =
        {
            Tags: NonEmptyList<NonBlankString>
            Codes: DistinctList<string>
        }

    let private productSchema () =
        schema<Product> {
            field "name" _.Name {
                withSchema RefinedSchemas.nonBlankString
            }
            field "slug" _.Slug {
                withSchema (slugSchema ())
            }
            field "quantity" _.Quantity {
                withSchema (Schema.int |> Schema.constrain (Axial.Schema.Constraint.greaterThan 0))
            }
            construct (fun name slug quantity ->
                { Name = name
                  Slug = slug
                  Quantity = quantity })
        }

    [<Fact>]
    let ``removing a refined type leaves the emitted metadata unchanged`` () =
        // The proof that dropping Slug, TrimmedString, and BoundedString costs nothing at
        // the boundary: the constraints they were built from still reach interpreters.
        let slug = slugSchema ()
        test <@ Schema.allConstraints slug |> List.map Constraint.code = [ "present"; "pattern" ] @>
        test <@ JsonSchema.generateValue slug |> _.Contains(slugPattern) @>

        let trimmed = trimmedSchema ()
        test <@ Schema.allConstraints trimmed |> List.map Constraint.code = [ "trimmed" ] @>

        let bounded = boundedSchema 2 4
        test <@ Schema.allConstraints bounded |> List.map Constraint.code = [ "present"; "lengthBetween" ] @>
        test <@ JsonSchema.generateValue bounded |> _.Contains("\"maxLength\":4") @>

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
               |> Result.map (fun product -> product.Name.Value, product.Slug, product.Quantity) =
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

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "name" ]; Error = SchemaError.Blank }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "quantity" ]; Error = SchemaError.OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "slug" ]; Error = SchemaError.InvalidFormat slugPattern } ] @>

    [<Fact>]
    let ``length bounds are enforced by the schema rather than recorded per value`` () =
        // BoundedString carried its own bounds, so a value refined at 1..99 satisfied a
        // 2..80 schema. Expressing the bounds as a constraint removes that whole class of
        // mismatch: there is only ever one set of bounds, the schema's.
        let check = SchemaCheck.text (boundedSchema 2 80)

        test <@ check "A" = Error [ CheckFailure.InvalidLength(CheckLengthExpectation.LengthBetween(2, 80), Some 1) ] @>
        test <@ check "Ada" = Ok() @>

    [<Fact>]
    let ``remaining scalar catalog schemas report the same failures as standalone refinement`` () =
        let schema =
            schema<Scalars> {
                field "command" _.Command {
                    withSchema (trimmedSchema ())
                }
                field "offset" _.Offset {
                    withSchema (Schema.int |> Schema.constrain (Axial.Schema.Constraint.notEqualTo 0))
                }
                construct (fun command offset -> { Command = command; Offset = offset })
            }

        let raw =
            Data.objectOfMap (Map.ofList [ "command", Data.Text " deploy "; "offset", Data.Text "0" ])

        let parsed = Schema.parseRetainingInput schema raw

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy [ PathSegment.Name "command" ]; Error = SchemaError.InvalidFormat "trimmed" }
                                 { Path = TestPath.fromLegacy [ PathSegment.Name "offset" ]; Error = SchemaError.Custom("notEqualTo:0", None) } ] @>

    [<Fact>]
    let ``refined collection catalog schemas parse trusted values`` () =
        let schema =
            schema<Tagged> {
                field "tags" _.Tags {
                    withSchema (RefinedSchemas.nonEmptyList RefinedSchemas.nonBlankString)
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
                    withSchema (RefinedSchemas.nonEmptyList RefinedSchemas.nonBlankString)
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
    let ``the generic interval schema parses trusted ranges`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "lower", Data.Text "2026-01-01T00:00:00+00:00"
                      "upper", Data.Text "2026-01-02T00:00:00+00:00" ]
            )

        let parsed = Schema.parseRetainingInput (RefinedSchemas.interval Schema.dateTime) raw

        test
            <@ parsed.Result |> Result.map (fun range -> range.Lower, range.Upper) =
                Ok(
                    DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
                ) @>

    [<Fact>]
    let ``the interval schema reports constructor failures after fields parse`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "lower", Data.Text "2026-01-02T00:00:00+00:00"
                      "upper", Data.Text "2026-01-01T00:00:00+00:00" ]
            )

        let parsed = Schema.parseRetainingInput (RefinedSchemas.interval Schema.dateTime) raw

        test
            <@ parsed.Errors = [ { Path = TestPath.fromLegacy []
                                   Error = SchemaError.ConstructorFailed "failed custom check 'interval'" } ] @>

    [<Fact>]
    let ``one generic interval schema replaces the per-type range schemas`` () =
        // dateTimeOffsetRange and dateOnlyRange were two hand-rolled types with duplicate
        // operations. One generic schema now covers both, and integers besides.
        let raw = Data.objectOfMap (Map.ofList [ "lower", Data.Text "1"; "upper", Data.Text "5" ])
        let parsed = Schema.parseRetainingInput (RefinedSchemas.interval Schema.int) raw

        test <@ parsed.Result |> Result.map Interval.toPair = Ok(1, 5) @>
        test <@ parsed.Result |> Result.map (Interval.contains 3) = Ok true @>


    [<Fact>]
    let ``int64 and float schemas describe themselves as numbers`` () =
        test <@ JsonSchema.generateValue Schema.int64 |> _.Contains("\"type\":\"integer\"") @>
        test <@ JsonSchema.generateValue Schema.float |> _.Contains("\"type\":\"number\"") @>

    [<Fact>]
    let ``the finite constraint is inspectable metadata rather than a custom code`` () =
        let schema = RefinedSchemas.finiteFloat

        test <@ Schema.allConstraints schema |> List.map Constraint.code = [ "finite" ] @>
        test
            <@ Schema.allConstraints schema
               |> List.map Constraint.metadata
                = [ ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Finite ] @>

    [<Fact>]
    let ``a finite schema admits real numbers and rejects NaN at the boundary`` () =
        test <@ Schema.parse RefinedSchemas.finiteFloat (Data.Text "1.5") |> Result.map FiniteFloat.value = Ok 1.5 @>
        test <@ Schema.parse RefinedSchemas.finiteFloat (Data.Text "NaN") |> Result.isError @>

    [<Fact>]
    let ``dateRange keeps the start and end wire vocabulary without a second type`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "2026-01-01T00:00:00+00:00"
                      "end", Data.Text "2026-01-02T00:00:00+00:00" ]
            )

        match Schema.parse RefinedSchemas.dateRange raw with
        | Ok range ->
            test <@ range.Lower = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) @>
            test <@ range.Upper = DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero) @>
            // The same type as Interval, so every Interval operation applies unchanged.
            test <@ Interval.contains (DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)) range @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``dateRange reports an inverted pair rather than reordering it`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "2026-01-02T00:00:00+00:00"
                      "end", Data.Text "2026-01-01T00:00:00+00:00" ]
            )

        // between would repair this; at a boundary an inverted pair is a caller error.
        test <@ Schema.parse RefinedSchemas.dateRange raw |> Result.isError @>
        test <@ Interval.between (DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)) (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) |> Interval.lower
                    = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) @>
