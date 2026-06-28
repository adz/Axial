namespace Axial.Refined.Tests

open System
open Axial.Refined
open Swensen.Unquote
open Xunit

module CatalogTests =
    type Contact =
        | Name of NonBlankString
        | Id of PositiveInt

    let unwrap result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "%A" error

    [<Fact>]
    let ``Numeric refinements cover low-risk integer invariants`` () =
        test <@ Refine.nonNegativeInt 0 |> Result.map _.Value = Ok 0 @>
        test <@ Refine.nonNegativeInt -1 = Error(OutOfRange("NonNegativeInt", "Expected a value greater than or equal to zero.")) @>
        test <@ Refine.nonZeroInt -1 |> Result.map _.Value = Ok -1 @>
        test <@ Refine.nonZeroInt 0 = Error(OutOfRange("NonZeroInt", "Expected a non-zero value.")) @>
        test <@ Refine.negativeInt -1 |> Result.map _.Value = Ok -1 @>
        test <@ Refine.negativeInt 0 = Error(OutOfRange("NegativeInt", "Expected a value less than zero.")) @>
        test <@ Refine.nonPositiveInt 0 |> Result.map _.Value = Ok 0 @>
        test <@ Refine.nonPositiveInt 1 = Error(OutOfRange("NonPositiveInt", "Expected a value less than or equal to zero.")) @>

    [<Fact>]
    let ``Text refinements preserve normalize and bound intentionally`` () =
        test <@ Refine.nonBlankString " Ada " |> Result.map _.Value = Ok " Ada " @>
        test <@ Refine.trimmedString "Ada" |> Result.map _.Value = Ok "Ada" @>
        test <@ Refine.trimmedString " Ada " = Error(InvalidFormat("TrimmedString", "Expected no leading or trailing whitespace.")) @>
        test <@ Refine.boundedString 2 4 "Ada" |> Result.map (fun value -> value.Value, value.MinLength, value.MaxLength) = Ok("Ada", 2, 4) @>
        test <@ Refine.boundedString 2 4 "A" = Error(OutOfRange("BoundedString", "Expected length between 2 and 4.")) @>
        test <@ Refine.boundedString 5 4 "Ada" = Error(InvalidStructure("BoundedString", "Expected minimum length to be less than or equal to maximum length.")) @>
        test <@ Refine.slug "ada-2026" |> Result.map _.Value = Ok "ada-2026" @>
        test <@ Refine.slug "Ada" = Error(InvalidFormat("Slug", "Expected only ASCII lowercase letters, digits, and hyphens.")) @>
        test <@ Refine.slug "-ada" = Error(InvalidFormat("Slug", "Expected no leading, trailing, or repeated hyphen.")) @>

    [<Fact>]
    let ``Collection refinements cover non-empty distinct and bounded shapes`` () =
        test <@ Refine.nonEmptyArray [ 1; 2 ] |> Result.map (fun values -> values.Head, values.Tail, values.ToArray()) = Ok(1, [| 2 |], [| 1; 2 |]) @>
        test <@ Refine.nonEmptyArray [] = Error(InvalidStructure("NonEmptyArray", "Expected at least one item.")) @>
        test <@ Refine.distinctList [ 1; 2; 3 ] |> Result.map _.ToList() = Ok [ 1; 2; 3 ] @>
        test <@ Refine.distinctList [ 1; 2; 1 ] = Error(InvalidStructure("DistinctList", "Expected no duplicate items.")) @>
        test <@ Refine.boundedList 2 3 [ 1; 2 ] |> Result.map (fun values -> values.ToList(), values.MinLength, values.MaxLength) = Ok([ 1; 2 ], 2, 3) @>
        test <@ Refine.boundedList 2 3 [ 1 ] = Error(OutOfRange("BoundedList", "Expected length between 2 and 3.")) @>
        test <@ Refine.boundedArray 1 2 [ 1; 2 ] |> Result.map (fun values -> values.ToArray(), values.MinLength, values.MaxLength) = Ok([| 1; 2 |], 1, 2) @>
        test <@ Refine.boundedArray 1 2 [ 1; 2; 3 ] = Error(OutOfRange("BoundedArray", "Expected length between 1 and 2.")) @>

    [<Fact>]
    let ``Re-certifying helpers preserve or re-check invariants`` () =
        let positive = Refine.positiveInt 2 |> unwrap
        let name = Refine.nonBlankString "Ada" |> unwrap
        let items = Refine.nonEmptyList [ 1; 2; 3 ] |> unwrap

        test <@ PositiveInt.map ((+) 1) positive |> Result.map _.Value = Ok 3 @>
        test <@ PositiveInt.replace 0 positive = Error(OutOfRange("PositiveInt", "Expected a value greater than zero.")) @>
        test <@ NonBlankString.map _.ToUpperInvariant() name |> Result.map _.Value = Ok "ADA" @>
        test <@ NonBlankString.map (fun _ -> "") name = Error(MissingValue "NonBlankString") @>
        test <@ NonEmptyList.map ((*) 2) items |> NonEmptyList.toList = [ 2; 4; 6 ] @>
        test <@ NonEmptyList.filter (fun value -> value > 2) items = [ 3 ] @>
        test <@ NonEmptyList.tryFilter (fun value -> value > 3) items = Error(InvalidStructure("NonEmptyList", "Expected at least one item.")) @>

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
            |> Choice.tryAny (InvalidFormat("Contact", "Expected a positive integer id or non-blank name."))

        test <@ Refine.dateTimeOffsetRange start finish |> Result.map (fun range -> range.Start, range.End) = Ok(start, finish) @>
        test <@ Refine.dateTimeOffsetRange finish start = Error(InvalidStructure("DateTimeOffsetRange", "Expected Start to be less than or equal to End.")) @>
        test <@ Character.isAsciiDigit '7' @>
        test <@ Character.isAsciiHexDigit 'f' @>
        test <@ Character.isLowercase 'a' @>
        test <@ Character.isUppercase 'A' @>
        test <@ Character.isWhitespace ' ' @>
        test <@ Character.isControl '\u0001' @>
        test <@ Character.isNumeric '9' @>
        test <@ parseContact "42" |> Result.map (function Id value -> value.Value | _ -> -1) = Ok 42 @>
        test <@ parseContact "Ada" |> Result.map (function Name value -> value.Value | _ -> "") = Ok "Ada" @>
        test <@ Choice.orElse Name Refine.nonBlankString Id parsePositiveId (MissingValue "Either") "Ada" |> Result.map (function Name value -> value.Value | _ -> "") = Ok "Ada" @>
