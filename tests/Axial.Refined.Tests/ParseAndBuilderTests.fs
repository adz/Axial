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
    let ``Parse option and default helpers cover missing invalid and valid input`` () =
        let guidText = "11111111-1111-1111-1111-111111111111"
        let parsedGuid = Guid.Parse guidText

        test <@ Parse.intOption None = None @>
        test <@ Parse.intOption (Some "nope") = None @>
        test <@ Parse.intOption (Some "42") = Some 42 @>
        test <@ Parse.boolOption None = None @>
        test <@ Parse.boolOption (Some "nope") = None @>
        test <@ Parse.boolOption (Some "true") = Some true @>
        test <@ Parse.decimalOption None = None @>
        test <@ Parse.decimalOption (Some "nope") = None @>
        test <@ Parse.decimalOption (Some "12.5") = Some 12.5M @>
        test <@ Parse.guidOption None = None @>
        test <@ Parse.guidOption (Some "nope") = None @>
        test <@ Parse.guidOption (Some guidText) = Some parsedGuid @>
        test <@ Parse.intOrDefault 5 "42" = 42 @>
        test <@ Parse.intOrDefault 5 "nope" = 5 @>
        test <@ Parse.boolOrDefault false "true" = true @>
        test <@ Parse.boolOrDefault true "nope" = true @>
        test <@ Parse.decimalOrDefault 5.5M "12.5" = 12.5M @>
        test <@ Parse.decimalOrDefault 5.5M "nope" = 5.5M @>

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
        test <@ Refine.nonBlankString "" = Error(CheckFailed("NonBlankString", [ Blank ])) @>
        test <@ positive = Ok 42 @>
        test <@ Refine.positiveInt 0 = Error(CheckFailed("PositiveInt", [ Positive(Some "0") ])) @>
        test <@ nonEmpty = Ok [ 1; 2; 3 ] @>
        test <@ Refine.nonEmptyList [] = Error(CheckFailed("NonEmptyList", [ NonEmpty(Some 0) ])) @>

    [<Fact>]
    let ``refine computation expression binds explicit results and annotated raw values`` () =
        let explicitBinding =
            refine {
                let! count = Parse.int "42"
                let! positive = Refine.positiveInt count
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
                let! name = "Ada"
                let! quantity = 3
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
