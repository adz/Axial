namespace Axial.Tests

open System
open Axial.Refined
open Swensen.Unquote
open Xunit

module RefinedTests =
    type SampleEnum =
        | First = 1
        | Second = 2

    [<Fact>]
    let ``Parse covers primitive parser success and failure`` () =
        let guidText = "11111111-1111-1111-1111-111111111111"
        let parsedGuid = Guid.Parse guidText

        test <@ Parse.int "42" = Ok 42 @>
        test <@ Parse.int "nope" = Error () @>
        test <@ Parse.long "42000000000" = Ok 42000000000L @>
        test <@ Parse.long "nope" = Error () @>
        test <@ Parse.decimal "12.5" = Ok 12.5M @>
        test <@ Parse.decimal "nope" = Error () @>
        test <@ Parse.float "12.5" = Ok 12.5 @>
        test <@ Parse.float "nope" = Error () @>
        test <@ Parse.bool "true" = Ok true @>
        test <@ Parse.bool "nope" = Error () @>
        test <@ Parse.guid guidText = Ok parsedGuid @>
        test <@ Parse.guid "nope" = Error () @>
        test <@ Parse.dateTime "2026-06-28T12:30:00" |> Result.isOk @>
        test <@ Parse.dateTime "nope" = Error () @>
        test <@ Parse.dateTimeOffset "2026-06-28T12:30:00+09:30" |> Result.isOk @>
        test <@ Parse.dateTimeOffset "nope" = Error () @>
        test <@ Parse.dateOnly "2026-06-28" |> Result.isOk @>
        test <@ Parse.dateOnly "nope" = Error () @>
        test <@ Parse.timeOnly "12:30:00" |> Result.isOk @>
        test <@ Parse.timeOnly "nope" = Error () @>
        test <@ Parse.enum<SampleEnum> "Second" = Ok SampleEnum.Second @>
        test <@ Parse.enum<SampleEnum> "nope" = Error () @>

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
        test <@ Refine.nonBlankString "" = Error(MissingValue "NonBlankString") @>
        test <@ positive = Ok 42 @>
        test <@ Refine.positiveInt 0 = Error(OutOfRange("PositiveInt", "Expected a value greater than zero.")) @>
        test <@ nonEmpty = Ok [ 1; 2; 3 ] @>
        test <@ Refine.nonEmptyList [] = Error(InvalidStructure("NonEmptyList", "Expected at least one item.")) @>

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

        let parseFailure =
            refine {
                let! count = Parse.int "nope"
                return count
            }

        test <@ explicitBinding = Ok 42 @>
        test <@ annotatedRawBinding = Ok("Ada", 3, [ 1; 2 ]) @>
        test <@ parseFailure = Error(InvalidFormat("Parse", "The value could not be parsed.")) @>
