namespace Axial.Refined.Tests

open System
open Axial.ErrorHandling
open Axial.Refined
open Swensen.Unquote
open Xunit

module CatalogTests =
    type Contact =
        | Name of NonBlankString
        | Id of PositiveInt

    type Email =
        private
        | Email of string

        member this.Value =
            let (Email value) = this
            value

    module Email =
        let create value =
            Refine.withChecks "Email" [ Check.String.present; Check.String.email ] Email value

    let unwrap result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "%A" error

    [<Fact>]
    let ``Numeric refinements cover low-risk integer invariants`` () =
        test <@ Refine.nonNegativeInt 0 |> Result.map _.Value = Ok 0 @>
        test <@ Refine.nonNegativeInt -1 = Error(CheckFailed("NonNegativeInt", [ CheckFailure.OutOfRange(AtLeast "0", Some "-1") ])) @>
        test <@ Refine.nonZeroInt -1 |> Result.map _.Value = Ok -1 @>
        test <@ Refine.nonZeroInt 0 = Error(CheckFailed("NonZeroInt", [ CheckFailure.Custom "notEqualTo:0" ])) @>
        test <@ Refine.negativeInt -1 |> Result.map _.Value = Ok -1 @>
        test <@ Refine.negativeInt 0 = Error(CheckFailed("NegativeInt", [ CheckFailure.OutOfRange(LessThan "0", Some "0") ])) @>
        test <@ Refine.nonPositiveInt 0 |> Result.map _.Value = Ok 0 @>
        test <@ Refine.nonPositiveInt 1 = Error(CheckFailed("NonPositiveInt", [ CheckFailure.OutOfRange(AtMost "0", Some "1") ])) @>

    [<Fact>]
    let ``Text refinements preserve normalize and bound intentionally`` () =
        test <@ Refine.nonBlankString " Ada " |> Result.map _.Value = Ok " Ada " @>
        test <@ Refine.trimmedString "Ada" |> Result.map _.Value = Ok "Ada" @>
        test <@ Refine.trimmedString " Ada " = Error(CheckFailed("TrimmedString", [ CheckFailure.InvalidFormat "trimmed" ])) @>
        test <@ Refine.boundedString 2 4 "Ada" |> Result.map (fun value -> value.Value, value.MinLength, value.MaxLength) = Ok("Ada", 2, 4) @>
        test <@ Refine.boundedString 2 4 "A" = Error(CheckFailed("BoundedString", [ CheckFailure.InvalidLength(LengthBetween(2, 4), Some 1) ])) @>
        test <@ Refine.boundedString 5 4 "Ada" = Error(InvalidStructure("BoundedString", "Expected minimum length to be less than or equal to maximum length.")) @>
        test <@ Refine.slug "ada-2026" |> Result.map _.Value = Ok "ada-2026" @>
        test <@ Refine.slug "Ada" = Error(CheckFailed("Slug", [ CheckFailure.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" ])) @>
        test <@ Refine.slug "-ada" = Error(CheckFailed("Slug", [ CheckFailure.InvalidFormat "^[a-z0-9]+(-[a-z0-9]+)*$" ])) @>

    [<Fact>]
    let ``Refined constructors can use Check programs directly`` () =
        test <@ Email.create "ada@example.com" |> Result.map _.Value = Ok "ada@example.com" @>
        test <@ Email.create "" = Error(CheckFailed("Email", [ Required; CheckFailure.InvalidFormat "email" ])) @>

    [<Fact>]
    let ``Collection refinements cover non-empty distinct and bounded shapes`` () =
        test <@ Refine.nonEmptyArray [ 1; 2 ] |> Result.map (fun values -> values.Head, values.Tail, values.ToArray()) = Ok(1, [| 2 |], [| 1; 2 |]) @>
        test <@ Refine.nonEmptyArray [] = Error(CheckFailed("NonEmptyArray", [ CheckFailure.InvalidCount(MinimumCount 1, Some 0) ])) @>
        test <@ Refine.distinctList [ 1; 2; 3 ] |> Result.map _.ToList() = Ok [ 1; 2; 3 ] @>
        test <@ Refine.distinctList [ 1; 2; 1 ] = Error(CheckFailed("DistinctList", [ CheckFailure.Duplicate ])) @>
        test <@ Refine.boundedList 2 3 [ 1; 2 ] |> Result.map (fun values -> values.ToList(), values.MinCount, values.MaxCount) = Ok([ 1; 2 ], 2, 3) @>
        test <@ Refine.boundedList 2 3 [ 1 ] = Error(CheckFailed("BoundedList", [ CheckFailure.InvalidCount(CountBetween(2, 3), Some 1) ])) @>
        test <@ Refine.boundedArray 1 2 [ 1; 2 ] |> Result.map (fun values -> values.ToArray(), values.MinCount, values.MaxCount) = Ok([| 1; 2 |], 1, 2) @>
        test <@ Refine.boundedArray 1 2 [ 1; 2; 3 ] = Error(CheckFailed("BoundedArray", [ CheckFailure.InvalidCount(CountBetween(1, 2), Some 3) ])) @>

    [<Fact>]
    let ``Re-certifying helpers preserve or re-check invariants`` () =
        let positive = Refine.positiveInt 2 |> unwrap
        let name = Refine.nonBlankString "Ada" |> unwrap
        let items = Refine.nonEmptyList [ 1; 2; 3 ] |> unwrap

        test <@ PositiveInt.map ((+) 1) positive |> Result.map _.Value = Ok 3 @>
        test <@ PositiveInt.replace 0 positive = Error(CheckFailed("PositiveInt", [ CheckFailure.OutOfRange(GreaterThan "0", Some "0") ])) @>
        test <@ NonBlankString.map _.ToUpperInvariant() name |> Result.map _.Value = Ok "ADA" @>
        test <@ NonBlankString.map (fun _ -> "") name = Error(CheckFailed("NonBlankString", [ Required ])) @>
        test <@ NonEmptyList.map ((*) 2) items |> NonEmptyList.toList = [ 2; 4; 6 ] @>
        test <@ NonEmptyList.filter (fun value -> value > 2) items = [ 3 ] @>
        test <@ NonEmptyList.tryFilter (fun value -> value > 3) items = Error(CheckFailed("NonEmptyList", [ CheckFailure.InvalidCount(MinimumCount 1, Some 0) ])) @>

    [<Fact>]
    let ``Temporal character and choice helpers cover first-wave support`` () =
        let start = DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero)
        let finish = start.AddHours 1.0

        let parseName value =
            Refine.nonBlankString value
            |> Result.map Name

        let parsePositiveId value =
            Parse.int value
            |> Result.mapError RefinementError.ParseFailed
            |> Result.bind Refine.positiveInt

        let parseId value =
            parsePositiveId value
            |> Result.map Id

        let parseContact =
            [
                parseId
                parseName
            ]
            |> Choice.tryAny (RefinementError.CheckFailed("Contact", [ CheckFailure.InvalidFormat "positive integer id or non-blank name" ]))

        test <@ Refine.dateTimeOffsetRange start finish |> Result.map (fun range -> range.Start, range.End) = Ok(start, finish) @>
        test <@ Refine.dateTimeOffsetRange finish start = Error(InvalidStructure("DateTimeOffsetRange", "Expected Start to be less than or equal to End.")) @>
        test <@ Character.isAsciiDigit '7' @>
        test <@ Character.isAsciiHexDigit 'f' @>
        test <@ Character.isLowercase 'a' @>
        test <@ Character.isUppercase 'A' @>
        test <@ Character.isWhitespace ' ' @>
        test <@ Character.isControl (char 1) @>
        test <@ Character.isNumeric '9' @>
        test <@ parseContact "42" |> Result.map (function Id value -> value.Value | _ -> -1) = Ok 42 @>
        test <@ parseContact "Ada" |> Result.map (function Name value -> value.Value | _ -> "") = Ok "Ada" @>
        test <@ Choice.orElse Name Refine.nonBlankString Id parsePositiveId (RefinementError.CheckFailed("Either", [ Required ])) "Ada" |> Result.map (function Name value -> value.Value | _ -> "") = Ok "Ada" @>
