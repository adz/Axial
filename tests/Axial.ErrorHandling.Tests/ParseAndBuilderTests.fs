namespace Axial.Refined.Tests

open System
open Axial.ErrorHandling
open Axial.Refined
open Swensen.Unquote
open Xunit

module ParseAndBuilderTests =
    type SampleEnum =
        | First = 1
        | Second = 2

    type CustomerId =
        private
        | CustomerId of PositiveInt

        member this.Value =
            let (CustomerId value) = this
            value.Value

    module CustomerId =
        let create raw : Result<CustomerId, RefinementError> =
            refine {
                let! (parsed: int) = raw
                let! (positive: PositiveInt) = parsed
                return CustomerId positive
            }

        let value (CustomerId value) =
            value.Value.ToString()

        let refinement =
            Refinement.define create value

    type CustomerId with
        static member Refinement(_: string, _: CustomerId) =
            CustomerId.refinement

    [<Fact>]
    let ``Refine from supports built-in refinements and refinements defined on application types`` () =
        let parsed: Result<int, RefinementError> = Refine.from "42"
        let positive: Result<PositiveInt, RefinementError> = Refine.from 3
        let customerId: Result<CustomerId, RefinementError> = Refine.from "43"
        let invalidInt: Result<int, RefinementError> = Refine.from "not-an-int"
        let invalidCustomerId: Result<CustomerId, RefinementError> = Refine.from "0"

        test <@ parsed = Ok 42 @>
        test <@ positive |> Result.map _.Value = Ok 3 @>
        test <@ customerId |> Result.map _.Value = Ok 43 @>
        test <@ invalidInt = Error(ParseFailed(ParseError.InvalidFormat("int", "not-an-int"))) @>
        test <@ invalidCustomerId |> Result.isError @>

    [<Fact>]
    let ``Refinement exposes the same construction and inspection used by type-directed refinement`` () =
        let created = Refinement.create CustomerId.refinement "45"

        let inspected =
            created
            |> Result.map (Refinement.inspect CustomerId.refinement)

        test <@ created |> Result.map _.Value = Ok 45 @>
        test <@ inspected = Ok "45" @>

    [<Fact>]
    let ``refine computation expression binds refinements defined on application types`` () =
        let actual =
            refine {
                let! (customerId: CustomerId) = "44"
                return customerId.Value
            }

        test <@ actual = Ok 44 @>

    [<Fact>]
    let ``Parse covers primitive parser success and failure`` () =
        let guidText = "11111111-1111-1111-1111-111111111111"
        let parsedGuid = Guid.Parse guidText

        test <@ Parse.int "42" = Ok 42 @>
        test <@ Parse.int "nope" = Error(ParseError.InvalidFormat("int", "nope")) @>
        test <@ Parse.long "42000000000" = Ok 42000000000L @>
        test <@ Parse.long "nope" = Error(ParseError.InvalidFormat("int64", "nope")) @>
        test <@ Parse.decimal "12.5" = Ok 12.5M @>
        test <@ Parse.decimal "nope" = Error(ParseError.InvalidFormat("decimal", "nope")) @>
        test <@ Parse.float "12.5" = Ok 12.5 @>
        test <@ Parse.float "nope" = Error(ParseError.InvalidFormat("float", "nope")) @>
        test <@ Parse.bool "true" = Ok true @>
        test <@ Parse.bool "nope" = Error(ParseError.InvalidFormat("bool", "nope")) @>
        test <@ Parse.guid guidText = Ok parsedGuid @>
        test <@ Parse.guid "nope" = Error(ParseError.InvalidFormat("Guid", "nope")) @>
        test <@ Parse.dateTime "2026-06-28T12:30:00" |> Result.isOk @>
        test <@ Parse.dateTime "nope" = Error(ParseError.InvalidFormat("DateTime", "nope")) @>
        test <@ Parse.dateTimeOffset "2026-06-28T12:30:00+09:30" |> Result.isOk @>
        test <@ Parse.dateTimeOffset "nope" = Error(ParseError.InvalidFormat("DateTimeOffset", "nope")) @>
        test <@ Parse.dateOnly "2026-06-28" |> Result.isOk @>
        test <@ Parse.dateOnly "nope" = Error(ParseError.InvalidFormat("DateOnly", "nope")) @>
        test <@ Parse.timeOnly "12:30:00" |> Result.isOk @>
        test <@ Parse.timeOnly "nope" = Error(ParseError.InvalidFormat("TimeOnly", "nope")) @>
        test <@ Parse.enum<SampleEnum> "Second" = Ok SampleEnum.Second @>
        test <@ Parse.enum<SampleEnum> "nope" = Error(ParseError.InvalidFormat("SampleEnum", "nope")) @>
        test <@ Parse.int "" = Error(ParseError.MissingValue "int") @>
        test <@ Parse.int "999999999999999999999999" = Error(ParseError.OutOfRange("int", "999999999999999999999999")) @>

    [<Fact>]
    let ``Parse optional helpers distinguish absence from malformed present input`` () =
        let guidText = "11111111-1111-1111-1111-111111111111"
        let parsedGuid = Guid.Parse guidText

        test <@ Parse.optional Parse.int None = Ok None @>
        test <@ Parse.optional Parse.int (Some "42") = Ok(Some 42) @>
        test <@ Parse.optional Parse.int (Some "nope") = Error(ParseError.InvalidFormat("int", "nope")) @>
        test <@ Parse.optionalOr 5 Parse.int None = Ok 5 @>
        test <@ Parse.optionalOr 5 Parse.int (Some "42") = Ok 42 @>
        test <@ Parse.optionalOr 5 Parse.int (Some "nope") = Error(ParseError.InvalidFormat("int", "nope")) @>

        test <@ Parse.intOption None = Ok None @>
        test <@ Parse.intOption (Some "nope") = Error(ParseError.InvalidFormat("int", "nope")) @>
        test <@ Parse.intOption (Some "42") = Ok(Some 42) @>
        test <@ Parse.boolOption None = Ok None @>
        test <@ Parse.boolOption (Some "nope") = Error(ParseError.InvalidFormat("bool", "nope")) @>
        test <@ Parse.boolOption (Some "true") = Ok(Some true) @>
        test <@ Parse.decimalOption None = Ok None @>
        test <@ Parse.decimalOption (Some "nope") = Error(ParseError.InvalidFormat("decimal", "nope")) @>
        test <@ Parse.decimalOption (Some "12.5") = Ok(Some 12.5M) @>
        test <@ Parse.guidOption None = Ok None @>
        test <@ Parse.guidOption (Some "nope") = Error(ParseError.InvalidFormat("Guid", "nope")) @>
        test <@ Parse.guidOption (Some guidText) = Ok(Some parsedGuid) @>

        test <@ Parse.intOrDefault 5 None = Ok 5 @>
        test <@ Parse.intOrDefault 5 (Some "42") = Ok 42 @>
        test <@ Parse.intOrDefault 5 (Some "nope") = Error(ParseError.InvalidFormat("int", "nope")) @>
        test <@ Parse.boolOrDefault false None = Ok false @>
        test <@ Parse.boolOrDefault true (Some "true") = Ok true @>
        test <@ Parse.boolOrDefault true (Some "nope") = Error(ParseError.InvalidFormat("bool", "nope")) @>
        test <@ Parse.decimalOrDefault 5.5M None = Ok 5.5M @>
        test <@ Parse.decimalOrDefault 5.5M (Some "12.5") = Ok 12.5M @>
        test <@ Parse.decimalOrDefault 5.5M (Some "nope") = Error(ParseError.InvalidFormat("decimal", "nope")) @>

    [<Fact>]
    let ``Refine builds initial structural refined values`` () =
        let nonBlank =
            Refine.nonBlankString "Ada"
            |> Result.map _.Value

        let positive =
            Refine.positiveInt 42
            |> Result.map _.Value

        let nonEmpty =
            Refine.nonEmptyList [ 1; 2; 3 ]
            |> Result.map _.ToList()

        test <@ nonBlank = Ok "Ada" @>
        test <@ Refine.nonBlankString "" = Error(CheckFailed("NonBlankString", [ Required ])) @>
        test <@ positive = Ok 42 @>
        test <@ Refine.positiveInt 0 = Error(CheckFailed("PositiveInt", [ CheckFailure.OutOfRange(GreaterThan "0", Some "0") ])) @>
        test <@ nonEmpty = Ok [ 1; 2; 3 ] @>
        test <@ Refine.nonEmptyList [] = Error(CheckFailed("NonEmptyList", [ CheckFailure.InvalidCount(MinimumCount 1, Some 0) ])) @>

    [<Fact>]
    let ``refine computation expression binds explicit results and annotated raw values`` () =
        let explicitBinding =
            refine {
                let! count = Parse.int "42"
                let! (positive: PositiveInt) = Refine.positiveInt count
                return positive.Value
            }

        let annotatedRawBinding =
            refine {
                let! (name: NonBlankString) = "Ada"
                let! (quantity: PositiveInt) = 3
                let! (items: NonEmptyList<int>) = [ 1; 2 ]
                return name.Value, quantity.Value, items.ToList()
            }

        let inferredRawBinding : Result<NonBlankString * PositiveInt, RefinementError> =
            refine {
                let! (name: NonBlankString) = "Ada"
                let! (quantity: PositiveInt) = 3
                return name, quantity
            }

        let parseFailure =
            refine {
                let! count = Parse.int "nope"
                return count
            }

        let parseReturnFrom =
            refine {
                return! Parse.int "42"
            }

        test <@ explicitBinding = Ok 42 @>
        test <@ annotatedRawBinding = Ok("Ada", 3, [ 1; 2 ]) @>
        test <@ (inferredRawBinding |> Result.map (fun (name, quantity) -> name.Value, quantity.Value)) = Ok("Ada", 3) @>
        test <@ parseFailure = Error(ParseFailed(ParseError.InvalidFormat("int", "nope"))) @>
        test <@ parseReturnFrom = Ok 42 @>

    [<Fact>]
    let ``refine computation expression selects non-zero integer refinement from the bound type`` () =
        let actual =
            refine {
                let! (value: NonZeroInt) = 42
                return value.Value
            }

        test <@ actual = Ok 42 @>

    [<Fact>]
    let ``refine computation expression selects string refinements from the bound type`` () =
        let actual =
            refine {
                let! (trimmed: TrimmedString) = "Ada"
                let! (slug: Slug) = "ada-lovelace"
                let! (bounded: BoundedString) = ("Axial", 3, 10)
                return trimmed.Value, slug.Value, bounded.Value
            }

        test <@ actual = Ok("Ada", "ada-lovelace", "Axial") @>

    [<Fact>]
    let ``refine computation expression selects every integer refinement from the bound type`` () =
        let actual =
            refine {
                let! (positive: PositiveInt) = 1
                let! (nonNegative: NonNegativeInt) = 0
                let! (nonZero: NonZeroInt) = -1
                let! (negative: NegativeInt) = -2
                let! (nonPositive: NonPositiveInt) = 0
                return positive.Value, nonNegative.Value, nonZero.Value, negative.Value, nonPositive.Value
            }

        test <@ actual = Ok(1, 0, -1, -2, 0) @>

    [<Fact>]
    let ``refine computation expression selects collection and range refinements from the bound type`` () =
        let start = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        let finish = start.AddDays 1

        let actual =
            refine {
                let! (nonEmptyList: NonEmptyList<int>) = [ 1; 2 ]
                let! (nonEmptyArray: NonEmptyArray<int>) = [ 1; 2 ]
                let! (distinct: DistinctList<int>) = [ 1; 2 ]
                let! (boundedList: BoundedList<int>) = ([ 1; 2 ], 1, 3)
                let! (boundedArray: BoundedArray<int>) = ([| 1; 2 |], 1, 3)
                let! (range: DateTimeOffsetRange) = (start, finish)
                let! (dateRange: DateOnlyRange) = (DateOnly(2026, 1, 1), DateOnly(2026, 1, 2))

                return
                    nonEmptyList.ToList(),
                    nonEmptyArray.ToArray(),
                    distinct.ToList(),
                    boundedList.ToList(),
                    boundedArray.ToArray(),
                    range.End,
                    dateRange.End
            }

        let expected =
            Ok([ 1; 2 ], [| 1; 2 |], [ 1; 2 ], [ 1; 2 ], [| 1; 2 |], finish, DateOnly(2026, 1, 2))

        test <@ actual = expected @>

    [<Fact>]
    let ``refine computation expression selects primitive parsers from the bound type`` () =
        let expectedGuid = Guid.Parse "6f9619ff-8b86-d011-b42d-00cf4fc964ff"

        let actual =
            refine {
                let! (intValue: int) = "42"
                let! (longValue: int64) = "43"
                let! (decimalValue: decimal) = "44.5"
                let! (floatValue: float) = "45.5"
                let! (boolValue: bool) = "true"
                let! (guidValue: Guid) = expectedGuid.ToString()
                let! (dateTimeValue: DateTime) = "2026-01-02T03:04:05"
                let! (offsetValue: DateTimeOffset) = "2026-01-02T03:04:05+00:00"
                let! (dateValue: DateOnly) = "2026-01-02"
                let! (timeValue: TimeOnly) = "03:04:05"

                return
                    intValue,
                    longValue,
                    decimalValue,
                    floatValue,
                    boolValue,
                    guidValue,
                    dateTimeValue.Year,
                    offsetValue.Year,
                    dateValue,
                    timeValue
            }

        let expected =
            Ok(42, 43L, 44.5M, 45.5, true, expectedGuid, 2026, 2026, DateOnly(2026, 1, 2), TimeOnly(3, 4, 5))

        test <@ actual = expected @>

    [<Fact>]
    let ``automatic refine bindings preserve parse and refinement failures`` () =
        let parseFailure =
            refine {
                let! (_: int) = "not-an-int"
                return ()
            }

        let refinementFailure =
            refine {
                let! (_: NonZeroInt) = 0
                return ()
            }

        test <@ parseFailure = Error(ParseFailed(ParseError.InvalidFormat("int", "not-an-int"))) @>

        let expectedRefinementFailure : Result<unit, RefinementError> =
            Error(CheckFailed("NonZeroInt", [ CheckFailure.Custom "notEqualTo:0" ]))

        test <@ refinementFailure = expectedRefinementFailure @>
