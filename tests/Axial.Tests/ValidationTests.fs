namespace Axial.Tests

open Microsoft.FSharp.Core
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module ValidationTests =
        type PolicyTestEnv = { EnforceLimit: bool; Limit: int }

        type PolicyTestError =
            | Required
            | TooLarge
            | PolicyDisabled

        [<Fact>]
        let ``Check is a typed value program that returns accumulated check failures`` () =
            let check : Check<string> =
                fun value ->
                    if value = "valid" then Ok ()
                    else Error []

            test <@ check "valid" = Ok () @>
            test <@ check "invalid" = Error [] @>

        [<Fact>]
        let ``CheckFailure exposes structured value constraint cases`` () =
            let failures =
                [ Missing
                  Blank
                  InvalidFormat "email"
                  Length(MinimumLength 3, Some 2)
                  Range(Between("1", "10"), Some "12")
                  Count(CountBetween(1, 3), Some 0)
                  Equality(EqualTo "expected", Some "actual")
                  CustomCode "domain.rule" ]

            test
                <@
                    failures =
                        [ Missing
                          Blank
                          InvalidFormat "email"
                          Length(MinimumLength 3, Some 2)
                          Range(Between("1", "10"), Some "12")
                          Count(CountBetween(1, 3), Some 0)
                          Equality(EqualTo "expected", Some "actual")
                          CustomCode "domain.rule" ]
                @>

        [<Fact>]
        let ``Check composition accumulates alternatives and maps failures`` () =
            let missingWhenEmpty : Check<string> =
                fun value -> if value = "" then Error [ Missing ] else Ok ()

            let blankWhenWhitespace : Check<string> =
                fun value -> if value.Trim() = "" then Error [ Blank ] else Ok ()

            let invalidWhenNotEmail : Check<string> =
                fun value ->
                    if value.Contains("@") then Ok ()
                    else Error [ InvalidFormat "email" ]

            let invalidWhenNotPhone : Check<string> =
                fun value ->
                    if value.StartsWith("+") then Ok ()
                    else Error [ InvalidFormat "phone" ]

            test <@ Check.all [ missingWhenEmpty; blankWhenWhitespace ] "" = Error [ Missing; Blank ] @>
            test <@ Check.all [ missingWhenEmpty; blankWhenWhitespace ] "Ada" = Ok () @>
            test <@ Check.all [] "Ada" = Ok () @>
            test <@ Check.any [ invalidWhenNotEmail; invalidWhenNotPhone ] "ada@example.com" = Ok () @>
            test <@ Check.any [ invalidWhenNotEmail; invalidWhenNotPhone ] "Ada" = Error [ InvalidFormat "email"; InvalidFormat "phone" ] @>
            test <@ Check.any [] "Ada" = Error [] @>
            test <@ Check.not invalidWhenNotEmail "Ada" = Ok () @>
            test <@ Check.not invalidWhenNotEmail "ada@example.com" = Error [ CustomCode "check.not" ] @>

            test
                <@
                    Check.mapFailure (function
                        | InvalidFormat expected -> CustomCode $"format.{expected}"
                        | failure -> failure) invalidWhenNotEmail "Ada" = Error [ CustomCode "format.email" ]
                @>

        [<Fact>]
        let ``Check all evaluates every check and preserves accumulated failure order`` () =
            let calls = ResizeArray<string>()

            let failWith name failure : Check<string> =
                fun _ ->
                    calls.Add name
                    Error [ failure ]

            let passWith name : Check<string> =
                fun _ ->
                    calls.Add name
                    Ok ()

            let check =
                Check.all
                    [
                        failWith "first" Missing
                        passWith "second"
                        failWith "third" Blank
                        failWith "fourth" (InvalidFormat "email")
                    ]

            test <@ check "" = Error [ Missing; Blank; InvalidFormat "email" ] @>
            test <@ calls |> Seq.toList = [ "first"; "second"; "third"; "fourth" ] @>

        [<Fact>]
        let ``Check any accumulates failed alternatives and short-circuits after success`` () =
            let calls = ResizeArray<string>()

            let failWith name failure : Check<string> =
                fun _ ->
                    calls.Add name
                    Error [ failure ]

            let passWith name : Check<string> =
                fun _ ->
                    calls.Add name
                    Ok ()

            let firstSuccess =
                Check.any
                    [
                        failWith "email" (InvalidFormat "email")
                        failWith "phone" (InvalidFormat "phone")
                        passWith "username"
                        failWith "later" (CustomCode "unreachable")
                    ]

            test <@ firstSuccess "ada" = Ok () @>
            test <@ calls |> Seq.toList = [ "email"; "phone"; "username" ] @>

            calls.Clear()

            let allFail =
                Check.any
                    [
                        failWith "email" (InvalidFormat "email")
                        failWith "phone" (InvalidFormat "phone")
                    ]

            test <@ allFail "ada" = Error [ InvalidFormat "email"; InvalidFormat "phone" ] @>
            test <@ calls |> Seq.toList = [ "email"; "phone" ] @>

        [<Fact>]
        let ``Check String behavior distinguishes null blank format and length failures`` () =
            let nullString: string = null

            let requiredEmail =
                Check.all
                    [
                        Check.String.present
                        Check.String.email
                        Check.String.lengthBetween 5 20
                    ]

            test <@ requiredEmail nullString = Error [ Missing; InvalidFormat "email"; Length(LengthBetween(5, 20), None) ] @>
            test <@ requiredEmail "" = Error [ Blank; InvalidFormat "email"; Length(LengthBetween(5, 20), Some 0) ] @>
            test <@ requiredEmail "   " = Error [ Blank; InvalidFormat "email"; Length(LengthBetween(5, 20), Some 3) ] @>
            test <@ requiredEmail "ada" = Error [ InvalidFormat "email"; Length(LengthBetween(5, 20), Some 3) ] @>
            test <@ requiredEmail "ada@example.com" = Ok () @>

        [<Fact>]
        let ``Check Number behavior keeps inclusive and exclusive range boundaries distinct`` () =
            test <@ Check.Number.between 1 3 1 = Ok () @>
            test <@ Check.Number.between 1 3 3 = Ok () @>
            test <@ Check.Number.between 1 3 0 = Error [ Range(Between("1", "3"), Some "0") ] @>
            test <@ Check.Number.between 1 3 4 = Error [ Range(Between("1", "3"), Some "4") ] @>

            test <@ Check.Number.greaterThan 1 1 = Error [ Range(GreaterThan "1", Some "1") ] @>
            test <@ Check.Number.greaterThan 1 2 = Ok () @>
            test <@ Check.Number.lessThan 3 3 = Error [ Range(LessThan "3", Some "3") ] @>
            test <@ Check.Number.lessThan 3 2 = Ok () @>
            test <@ Check.Number.atLeast 1 1 = Ok () @>
            test <@ Check.Number.atMost 3 3 = Ok () @>
            test <@ Check.Number.positive 1 = Ok () @>
            test <@ Check.Number.positive 0 = Error [ Range(GreaterThan "0", Some "0") ] @>
            test <@ Check.Number.nonNegative 0 = Ok () @>
            test <@ Check.Number.nonNegative -1 = Error [ Range(AtLeast "0", Some "-1") ] @>
            test <@ Check.Number.negative -1 = Ok () @>
            test <@ Check.Number.negative 0 = Error [ Range(LessThan "0", Some "0") ] @>
            test <@ Check.Number.nonPositive 0 = Ok () @>
            test <@ Check.Number.nonPositive 1 = Error [ Range(AtMost "0", Some "1") ] @>

        [<Fact>]
        let ``Check Seq behavior accumulates count and distinct failures`` () =
            let nullValues: seq<int> = null

            let seqCheck : Check<seq<int>> =
                Check.all
                    [
                        Check.Seq.minCount 2
                        Check.Seq.maxCount 3
                        Check.Seq.distinct
                    ]

            test <@ seqCheck [ 1; 2; 3 ] = Ok () @>
            test <@ seqCheck [] = Error [ Count(MinimumCount 2, Some 0) ] @>
            test <@ seqCheck [ 1; 2; 1; 3 ] = Error [ Count(MaximumCount 3, Some 4); CustomCode "seq.distinct" ] @>
            test <@ seqCheck nullValues = Error [ Count(MinimumCount 2, None); Count(MaximumCount 3, None); Missing ] @>

        [<Fact>]
        let ``Check Option and Result behavior composes with all and any`` () =
            test <@ Check.all [ Check.Option.some; Check.not Check.Option.none ] (Some 1) = Ok () @>
            test <@ Check.all [ Check.Option.some; Check.not Check.Option.none ] None = Error [ Missing; CustomCode "check.not" ] @>
            test <@ Check.any [ Check.Option.none; Check.Option.some ] (Some 1) = Ok () @>
            test <@ Check.any [ Check.Option.none; Check.Option.some ] None = Ok () @>

            test <@ Check.all [ Check.Result.ok; Check.not Check.Result.error ] (Ok 1) = Ok () @>
            test
                <@
                    Check.all [ Check.Result.ok; Check.not Check.Result.error ] (Error "missing") =
                        Error [ Equality(EqualTo "Ok", Some "Error"); CustomCode "check.not" ]
                @>
            test <@ Check.any [ Check.Result.error; Check.Result.ok ] (Error "missing") = Ok () @>
            test <@ Check.any [ Check.Result.error; Check.Result.ok ] (Ok 1) = Ok () @>

        [<Fact>]
        let ``Check String exposes executable string value checks`` () =
            let nullString: string = null

            test <@ Check.String.present "Ada" = Ok () @>
            test <@ Check.String.present nullString = Error [ Missing ] @>
            test <@ Check.String.present "" = Error [ Blank ] @>
            test <@ Check.String.present "   " = Error [ Blank ] @>

            test <@ Check.String.empty "" = Ok () @>
            test <@ Check.String.empty " " = Error [ Length(ExactLength 0, Some 1) ] @>
            test <@ Check.String.empty nullString = Error [ Missing ] @>

            test <@ Check.String.notEmpty " " = Ok () @>
            test <@ Check.String.notEmpty "" = Error [ Length(MinimumLength 1, Some 0) ] @>
            test <@ Check.String.notEmpty nullString = Error [ Missing ] @>

            test <@ Check.String.minLength 3 "Ada" = Ok () @>
            test <@ Check.String.minLength 3 "Al" = Error [ Length(MinimumLength 3, Some 2) ] @>
            test <@ Check.String.minLength 3 nullString = Error [ Length(MinimumLength 3, None) ] @>

            test <@ Check.String.maxLength 3 "Ada" = Ok () @>
            test <@ Check.String.maxLength 3 "Axial" = Error [ Length(MaximumLength 3, Some 5) ] @>
            test <@ Check.String.maxLength 3 nullString = Error [ Length(MaximumLength 3, None) ] @>

            test <@ Check.String.lengthBetween 2 4 "Ada" = Ok () @>
            test <@ Check.String.lengthBetween 2 4 "A" = Error [ Length(LengthBetween(2, 4), Some 1) ] @>
            test <@ Check.String.lengthBetween 2 4 "Axial" = Error [ Length(LengthBetween(2, 4), Some 5) ] @>
            test <@ Check.String.lengthBetween 2 4 nullString = Error [ Length(LengthBetween(2, 4), None) ] @>

            test <@ Check.String.length 3 "Ada" = Ok () @>
            test <@ Check.String.length 3 "Axial" = Error [ Length(ExactLength 3, Some 5) ] @>
            test <@ Check.String.length 3 nullString = Error [ Length(ExactLength 3, None) ] @>
            test <@ Check.String.exactLength 3 "Ada" = Ok () @>

            test <@ Check.String.email "ada@example.com" = Ok () @>
            test <@ Check.String.email "Ada" = Error [ InvalidFormat "email" ] @>
            test <@ Check.String.email nullString = Error [ InvalidFormat "email" ] @>

            test <@ Check.String.matches "^[a-z]+$" "ada" = Ok () @>
            test <@ Check.String.matches "^[a-z]+$" "Ada" = Error [ InvalidFormat "^[a-z]+$" ] @>
            test <@ Check.String.matches "^[a-z]+$" nullString = Error [ InvalidFormat "^[a-z]+$" ] @>

            test <@ Check.String.numeric "12345" = Ok () @>
            test <@ Check.String.numeric "12a45" = Error [ InvalidFormat "numeric" ] @>
            test <@ Check.String.numeric "" = Error [ InvalidFormat "numeric" ] @>
            test <@ Check.String.numeric nullString = Error [ InvalidFormat "numeric" ] @>

            test <@ Check.String.alphaNumeric "Ada123" = Ok () @>
            test <@ Check.String.alphaNumeric "Ada-123" = Error [ InvalidFormat "alphaNumeric" ] @>
            test <@ Check.String.alphaNumeric "" = Error [ InvalidFormat "alphaNumeric" ] @>
            test <@ Check.String.alphaNumeric nullString = Error [ InvalidFormat "alphaNumeric" ] @>

            test <@ Check.String.oneOf [ "draft"; "published" ] "draft" = Ok () @>
            test <@ Check.String.oneOf [ "draft"; "published" ] "archived" = Error [ Equality(EqualTo "draft|published", Some "archived") ] @>
            test <@ Check.String.oneOf [ "draft"; "published" ] nullString = Error [ Equality(EqualTo "draft|published", None) ] @>

        [<Fact>]
        let ``Check Number exposes executable range checks`` () =
            test <@ Check.Number.between 1 10 5 = Ok () @>
            test <@ Check.Number.between 1 10 0 = Error [ Range(Between("1", "10"), Some "0") ] @>
            test <@ Check.Number.between 1 10 11 = Error [ Range(Between("1", "10"), Some "11") ] @>

            test <@ Check.Number.greaterThan 3 4 = Ok () @>
            test <@ Check.Number.greaterThan 3 3 = Error [ Range(GreaterThan "3", Some "3") ] @>

            test <@ Check.Number.lessThan 3 2 = Ok () @>
            test <@ Check.Number.lessThan 3 3 = Error [ Range(LessThan "3", Some "3") ] @>

            test <@ Check.Number.atLeast 3 3 = Ok () @>
            test <@ Check.Number.atLeast 3 2 = Error [ Range(AtLeast "3", Some "2") ] @>

            test <@ Check.Number.atMost 3 3 = Ok () @>
            test <@ Check.Number.atMost 3 4 = Error [ Range(AtMost "3", Some "4") ] @>

            test <@ Check.Number.between 1.5m 2.5m 2.0m = Ok () @>
            test <@ Check.Number.atLeast 1.5m 1.0m = Error [ Range(AtLeast "1.5", Some "1.0") ] @>
            test <@ Check.Number.positive 0.1m = Ok () @>
            test <@ Check.Number.nonPositive 0.1m = Error [ Range(AtMost "0", Some "0.1") ] @>

        [<Fact>]
        let ``Check Seq exposes executable sequence value checks`` () =
            let nullValues: seq<int> = null

            test <@ Check.Seq.notEmpty [ 1 ] = Ok () @>
            test <@ Check.Seq.notEmpty [] = Error [ Count(MinimumCount 1, Some 0) ] @>
            test <@ Check.Seq.notEmpty nullValues = Error [ Count(MinimumCount 1, None) ] @>

            test <@ Check.Seq.count 2 [ 1; 2 ] = Ok () @>
            test <@ Check.Seq.count 2 [ 1 ] = Error [ Count(ExactCount 2, Some 1) ] @>
            test <@ Check.Seq.count 2 nullValues = Error [ Count(ExactCount 2, None) ] @>

            test <@ Check.Seq.minCount 2 [ 1; 2 ] = Ok () @>
            test <@ Check.Seq.minCount 2 [ 1 ] = Error [ Count(MinimumCount 2, Some 1) ] @>
            test <@ Check.Seq.minCount 2 nullValues = Error [ Count(MinimumCount 2, None) ] @>

            test <@ Check.Seq.maxCount 2 [ 1; 2 ] = Ok () @>
            test <@ Check.Seq.maxCount 2 [ 1; 2; 3 ] = Error [ Count(MaximumCount 2, Some 3) ] @>
            test <@ Check.Seq.maxCount 2 nullValues = Error [ Count(MaximumCount 2, None) ] @>

            test <@ Check.Seq.countBetween 2 4 [ 1; 2; 3 ] = Ok () @>
            test <@ Check.Seq.countBetween 2 4 [ 1 ] = Error [ Count(CountBetween(2, 4), Some 1) ] @>
            test <@ Check.Seq.countBetween 2 4 [ 1; 2; 3; 4; 5 ] = Error [ Count(CountBetween(2, 4), Some 5) ] @>
            test <@ Check.Seq.countBetween 2 4 nullValues = Error [ Count(CountBetween(2, 4), None) ] @>

            test <@ Check.Seq.distinct [ 1; 2; 3 ] = Ok () @>
            test <@ Check.Seq.distinct [ 1; 2; 1 ] = Error [ CustomCode "seq.distinct" ] @>
            test <@ Check.Seq.distinct nullValues = Error [ Missing ] @>

        [<Fact>]
        let ``Check exposes top-level concrete structured checks`` () =
            let nullString: string = null
            let nullValues: seq<int> = null

            test <@ Check.length 3 "Ada" = Ok () @>
            test <@ Check.length 3 "Axial" = Error [ Length(ExactLength 3, Some 5) ] @>
            test <@ Check.length 3 nullString = Error [ Length(ExactLength 3, None) ] @>
            test <@ Check.minLength 3 "Ada" = Ok () @>
            test <@ Check.maxLength 3 "Axial" = Error [ Length(MaximumLength 3, Some 5) ] @>
            test <@ Check.lengthBetween 2 4 "Ada" = Ok () @>
            test <@ Check.email "ada@example.com" = Ok () @>
            test <@ Check.matches "^[a-z]+$" "Ada" = Error [ InvalidFormat "^[a-z]+$" ] @>
            test <@ Check.oneOf [ "draft"; "published" ] "archived" = Error [ Equality(EqualTo "draft|published", Some "archived") ] @>

            test <@ Check.between 1 10 5 = Ok () @>
            test <@ Check.greaterThan 3 3 = Error [ Range(GreaterThan "3", Some "3") ] @>
            test <@ Check.lessThan 3 3 = Error [ Range(LessThan "3", Some "3") ] @>
            test <@ Check.atLeast 3 2 = Error [ Range(AtLeast "3", Some "2") ] @>
            test <@ Check.atMost 3 4 = Error [ Range(AtMost "3", Some "4") ] @>
            test <@ Check.positive 1 = Ok () @>
            test <@ Check.positive 0 = Error [ Range(GreaterThan "0", Some "0") ] @>
            test <@ Check.nonNegative 0 = Ok () @>
            test <@ Check.nonNegative -1 = Error [ Range(AtLeast "0", Some "-1") ] @>
            test <@ Check.negative -1 = Ok () @>
            test <@ Check.negative 0 = Error [ Range(LessThan "0", Some "0") ] @>
            test <@ Check.nonPositive 0 = Ok () @>
            test <@ Check.nonPositive 1 = Error [ Range(AtMost "0", Some "1") ] @>

            test <@ Check.count 2 [ 1; 2 ] = Ok () @>
            test <@ Check.count 2 [ 1 ] = Error [ Count(ExactCount 2, Some 1) ] @>
            test <@ Check.count 2 nullValues = Error [ Count(ExactCount 2, None) ] @>
            test <@ Check.minCount 2 [ 1 ] = Error [ Count(MinimumCount 2, Some 1) ] @>
            test <@ Check.maxCount 2 [ 1; 2; 3 ] = Error [ Count(MaximumCount 2, Some 3) ] @>
            test <@ Check.countBetween 2 4 [ 1; 2; 3 ] = Ok () @>
            test <@ Check.distinct [ 1; 2; 3 ] = Ok () @>
            test <@ Check.contains 2 [ 1; 2 ] = Ok () @>
            test <@ Check.contains 3 [ 1; 2 ] = Error [ Equality(EqualTo "3", None) ] @>
            test <@ Check.contains 3 nullValues = Error [ Missing ] @>
            test <@ Check.single [ 1 ] = Ok () @>
            test <@ Check.single [ 1; 2 ] = Error [ Count(ExactCount 1, Some 2) ] @>
            test <@ Check.atMostOne [ 1; 2 ] = Error [ Count(MaximumCount 1, Some 2) ] @>
            test <@ Check.atLeastOne [] = Error [ Count(MinimumCount 1, Some 0) ] @>
            test <@ Check.moreThanOne [ 1 ] = Error [ Count(MinimumCount 2, Some 1) ] @>

            test <@ Check.equalTo 3 3 = Ok () @>
            test <@ Check.equalTo 3 4 = Error [ Equality(EqualTo "3", Some "4") ] @>
            test <@ Check.notEqualTo 3 4 = Ok () @>
            test <@ Check.notEqualTo 3 3 = Error [ Equality(NotEqualTo "3", Some "3") ] @>

        [<Fact>]
        let ``Check Option exposes executable option value checks`` () =
            test <@ Check.Option.some (Some 1) = Ok () @>
            test <@ Check.Option.some None = Error [ Missing ] @>

            test <@ Check.Option.none None = Ok () @>
            test <@ Check.Option.none (Some 1) = Error [ Equality(EqualTo "None", Some "Some") ] @>

        [<Fact>]
        let ``Check ValueOption exposes executable value option checks`` () =
            test <@ Check.ValueOption.some (ValueSome 1) = Ok () @>
            test <@ Check.ValueOption.some ValueNone = Error [ Missing ] @>

            test <@ Check.ValueOption.none ValueNone = Ok () @>
            test <@ Check.ValueOption.none (ValueSome 1) = Error [ Equality(EqualTo "ValueNone", Some "ValueSome") ] @>

        [<Fact>]
        let ``Check Nullable exposes executable nullable value checks`` () =
            test <@ Check.Nullable.hasValue (System.Nullable 1) = Ok () @>
            test <@ Check.Nullable.hasValue (System.Nullable<int>()) = Error [ Missing ] @>

            test <@ Check.Nullable.hasNoValue (System.Nullable<int>()) = Ok () @>
            test <@ Check.Nullable.hasNoValue (System.Nullable 1) = Error [ Equality(EqualTo "null", Some "value") ] @>

        [<Fact>]
        let ``Check Result exposes executable result value checks`` () =
            test <@ Check.Result.ok (Ok 1) = Ok () @>
            test <@ Check.Result.ok (Error "missing") = Error [ Equality(EqualTo "Ok", Some "Error") ] @>

            test <@ Check.Result.error (Error "missing") = Ok () @>
            test <@ Check.Result.error (Ok 1) = Error [ Equality(EqualTo "Error", Some "Ok") ] @>

        [<Fact>]
        let ``Check exposes pure predicates`` () =
            let nullString: string = null

            test <@ Check.isSome (Some 10) @>
            test <@ Check.isNone None @>
            test <@ Check.isValueSome (ValueSome 11) @>
            test <@ Check.isValueNone ValueNone @>
            test <@ Check.hasValue (System.Nullable 12) @>
            test <@ Check.hasNoValue (System.Nullable<int>()) @>
            test <@ Check.notNull "axial" @>
            test <@ Check.isNull nullString @>
            test <@ Check.isOk (Ok 3) @>
            test <@ Check.isError (Error "missing") @>
            test <@ Check.notBlank "  x  " @>
            test <@ Check.blank nullString @>
            test <@ Check.hasMinLength 3 "abcd" @>
            test <@ Check.hasMaxLength 3 nullString @>
            test <@ Check.hasExactLength 3 "abc" @>
            test <@ Check.matchesRegex "^[a-z]+$" "abc" @>
            test <@ Check.isEmail "ada@example.com" @>
            test <@ Check.isNumeric "12345" @>
            test <@ Check.isAlphaNumeric "abc123" @>
            test <@ Check.notEmpty [ 1; 2 ] @>
            test <@ Check.isEmpty Seq.empty<int> @>
            test <@ Check.hasCount 2 [ 1; 2 ] @>
            test <@ Check.hasDuplicates [ 1; 2; 1 ] @>
            test <@ Check.hasNoDuplicates [ 1; 2; 3 ] @>
            test <@ Check.isSingle [ 5 ] @>
            test <@ Check.negate Check.notBlank "" @>

        [<Fact>]
        let ``Result covers fail-fast helpers and the result computation expression`` () =
            let workflow =
                result {
                    let! value = Ok 20
                    let! divisor = Ok 2
                    do! Ok ()
                    return value / divisor
                }

            test <@ (Ok 10 |> Result.map ((+) 1)) = Ok 11 @>
            test <@ (Ok 7 |> Result.bind (fun value -> Ok(value + 5))) = Ok 12 @>
            test <@ (Error 42 |> Result.mapError string) = Error "42" @>
            test <@ ("Ada" |> Result.require Check.String.present) = Ok () @>
            test <@ ("" |> Result.require Check.String.present) = Error [ Blank ] @>
            test <@ ("Ada" |> Result.guard Check.String.present) = Ok "Ada" @>
            test <@ ("" |> Result.guard Check.String.present) = Error [ Blank ] @>
            test <@ ("Ada" |> Result.notBlank) = Ok "Ada" @>
            test <@ ("" |> Result.notBlank) = Error [ Blank ] @>
            test <@ ("Ada" |> Result.keepIf Check.notBlank "required") = Ok "Ada" @>
            test <@ ("" |> Result.keepIf Check.notBlank "required") = Error "required" @>
            test <@ (true |> Result.checkOr "invalid") = Ok () @>
            test <@ (false |> Result.checkOr "invalid") = Error "invalid" @>
            test <@ (Error () |> Result.withError "typed") = Error "typed" @>
            test <@ ((true, 42) |> Result.fromTry) = Ok 42 @>
            test <@ ((false, 42) |> Result.fromTry) = Error () @>
            test <@ (Choice1Of2 42 |> Result.fromChoice) = Ok 42 @>
            test <@ (Choice2Of2 "missing" |> Result.fromChoice) = Error "missing" @>
            test <@ (Ok 10 |> Result.toOption) = Some 10 @>
            test <@ (Error "missing" |> Result.toValueOption) = ValueNone @>
            test <@ (Error "missing" |> Result.defaultValue 5) = 5 @>
            test <@ (Some 7 |> Result.someOr "missing") = Ok 7 @>
            test <@ (None |> Result.noneOr "unexpected") = Ok () @>
            test <@ (ValueSome 8 |> Result.valueSomeOr "missing") = Ok 8 @>
            test <@ (ValueNone |> Result.valueNoneOr "unexpected") = Ok () @>
            test <@ (System.Nullable 12 |> Result.nullableOr "missing") = Ok 12 @>
            test <@ ("Ada" |> Result.notNullOr "required") = Ok "Ada" @>
            test <@ (Ok 3 |> Result.okOr "missing") = Ok 3 @>
            test <@ (Error "failed" |> Result.errorOr "missing") = Ok "failed" @>
            test <@ ([ 1; 2 ] |> Result.headOr "missing") = Ok 1 @>
            test <@ ("Ada" |> Result.keepIf Check.notBlank "required") = Ok "Ada" @>
            test <@ ("Ada" |> Result.keepIf Check.notNull "required") = Ok "Ada" @>
            test <@ ([ 1; 2 ] |> Result.keepIf Check.notEmpty "required") = Ok [ 1; 2 ] @>
            test <@ ([ 1; 2 ] |> Result.keepIf (Seq.contains 2) "missing") = Ok [ 1; 2 ] @>
            test <@ ([ 1; 2; 3 ] |> Result.keepIf Check.hasNoDuplicates "duplicate") = Ok [ 1; 2; 3 ] @>
            test <@ ("ab" |> Result.minLength 3) = Error [ Length(MinimumLength 3, Some 2) ] @>
            test <@ ("abcd" |> Result.maxLength 3) = Error [ Length(MaximumLength 3, Some 4) ] @>
            test <@ ("ab" |> Result.exactLength 3) = Error [ Length(ExactLength 3, Some 2) ] @>
            test <@ (6 |> Result.range 3 5) = Error [ Range(Between("3", "5"), Some "6") ] @>
            test <@ (3 |> Result.greaterThan 3) = Error [ Range(GreaterThan "3", Some "3") ] @>
            test <@ (3 |> Result.lessThan 3) = Error [ Range(LessThan "3", Some "3") ] @>
            test <@ (2 |> Result.atLeast 3) = Error [ Range(AtLeast "3", Some "2") ] @>
            test <@ (4 |> Result.atMost 3) = Error [ Range(AtMost "3", Some "4") ] @>
            test <@ ([ 5 ] |> Result.single) = Ok 5 @>
            test <@ ([] |> Result.single) = Error(ExpectedSingle 0) @>
            test <@ ([ 5 ] |> Result.atMostOne) = Ok(Some 5) @>
            test <@ ([] |> Result.atLeastOne) = Error [ Count(MinimumCount 1, Some 0) ] @>
            test <@ ([ 5 ] |> Result.moreThanOne) = Error [ Count(MinimumCount 2, Some 1) ] @>
            test <@ Collection.traverseResult (fun value -> if value < 3 then Ok(value * 2) else Error value) [ 1; 2 ] = Ok [ 2; 4 ] @>
            test <@ Collection.sequenceResult [ Ok 1; Error "missing"; Ok 3 ] = Error "missing" @>
            test <@ workflow = Ok 10 @>

        [<Fact>]
        let ``Policy adapts result functions to flow verification`` () =
            let requireNonBlank value =
                value |> Result.keepIf Check.notBlank ()

            let requireName =
                Policy.withError requireNonBlank Required

            let withinLimit =
                Policy.context
                    (fun env value -> if value <= env.Limit then Ok value else Error ())
                    (fun _ -> TooLarge)

            let optionalLimit =
                Policy.optional (fun env -> env.EnforceLimit) withinLimit

            let workflow name count =
                flow {
                    let! checkedName = name |> Flow.verify requireName
                    let! _ = count |> Flow.verify optionalLimit
                    return checkedName
                }

            test <@ workflow "Ada" 3 |> Flow.runSync { EnforceLimit = true; Limit = 5 } = Exit.Success "Ada" @>
            test <@ workflow "" 3 |> Flow.runSync { EnforceLimit = true; Limit = 5 } = Exit.Failure(Cause.Fail Required) @>
            test <@ workflow "Ada" 6 |> Flow.runSync { EnforceLimit = true; Limit = 5 } = Exit.Failure(Cause.Fail TooLarge) @>
            test <@ workflow "Ada" 6 |> Flow.runSync { EnforceLimit = false; Limit = 5 } = Exit.Success "Ada" @>
            test <@ Policy.pass { EnforceLimit = true; Limit = 5 } "value" = Ok "value" @>
            test <@ Policy.compose requireName (Policy.withError (fun value -> Ok(value.Length)) PolicyDisabled) { EnforceLimit = true; Limit = 5 } "Ada" = Ok 3 @>

        [<Fact>]
        let ``validation graph names are explicit`` () =
            let graph : Diagnostics<string> =
                {
                    Errors = [ "missing" ]
                    Children = Map.empty
                }

            test <@ typeof<PathSegment>.Name = "PathSegment" @>
            test <@ typeof<Diagnostic<string>>.Name = "Diagnostic`1" @>
            test <@ typeof<Diagnostics<string>>.Name = "Diagnostics`1" @>
            test <@ graph.Errors = [ "missing" ] @>
            test <@ graph.Children.IsEmpty @>
            test <@ Diagnostics.flatten graph = [ { Path = []; Error = "missing" } ] @>
            test <@ (Diagnostics.toString graph |> fun text -> text.Contains("missing")) @>
            test <@ (Diagnostics.toString graph |> fun text -> text.Contains("Errors:") |> not) @>

        [<Fact>]
        let ``diagnostics toString renders root errors and nested child branches`` () =
            let graph : Diagnostics<string> =
                {
                    Errors = [ "RequestId required" ]
                    Children =
                        Map.ofList
                            [
                                PathSegment.Key "customer",
                                {
                                    Errors = []
                                    Children =
                                        Map.ofList
                                            [
                                                PathSegment.Key "address",
                                                {
                                                    Errors = []
                                                    Children =
                                                        Map.ofList
                                                            [
                                                                PathSegment.Key "city",
                                                                Diagnostics.singleton "City required"
                                                            ]
                                                }

                                                PathSegment.Key "lines",
                                                {
                                                    Errors = []
                                                    Children =
                                                        Map.ofList
                                                            [
                                                                PathSegment.Index 0,
                                                                {
                                                                    Errors = []
                                                                    Children =
                                                                        Map.ofList
                                                                            [
                                                                                PathSegment.Key "name",
                                                                                Diagnostics.singleton "Line 0 name required"
                                                                            ]
                                                                }
                                                            ]
                                                }
                                            ]
                                }
                            ]
                }

            let text = Diagnostics.toString graph

            test <@ text.Contains("- RequestId required") @>
            test <@ text.Contains("customer:") @>
            test <@ text.Contains("address:") @>
            test <@ text.Contains("lines:") @>
            test <@ text.Contains("city:") @>
            test <@ text.Contains("name:") @>
            test <@ text.Contains("Errors:") |> not @>

        [<Fact>]
        let ``diagnostics merge recursively combines shared branches and flattens paths`` () =
            let left =
                {
                    Errors = [ "left-root" ]
                    Children =
                        Map.ofList
                            [
                                PathSegment.Key "user",
                                {
                                    Errors = [ "left-user" ]
                                    Children =
                                        Map.ofList
                                            [
                                                PathSegment.Key "address",
                                                Diagnostics.singleton "left-address"
                                            ]
                                }
                            ]
                }

            let right =
                {
                    Errors = [ "right-root" ]
                    Children =
                        Map.ofList
                            [
                                PathSegment.Key "user",
                                {
                                    Errors = [ "right-user" ]
                                    Children =
                                        Map.ofList
                                            [
                                                PathSegment.Key "address",
                                                Diagnostics.singleton "right-address"
                                            ]
                                }
                            ]
                }

            let merged = Diagnostics.merge left right

            test <@ merged.Errors = [ "left-root"; "right-root" ] @>

            match merged.Children |> Map.tryFind (PathSegment.Key "user") with
            | Some userBranch ->
                test <@ userBranch.Errors = [ "left-user"; "right-user" ] @>

                match userBranch.Children |> Map.tryFind (PathSegment.Key "address") with
                | Some addressBranch ->
                    let expectedAddress =
                        [
                            { Path = []; Error = "left-address" }
                            { Path = []; Error = "right-address" }
                        ]

                    test <@ Diagnostics.flatten addressBranch = expectedAddress @>
                | None -> failwith "expected merged address branch"
            | None -> failwith "expected merged user branch"

            let expectedMerged =
                [
                    { Path = []; Error = "left-root" }
                    { Path = []; Error = "right-root" }
                    { Path = [ PathSegment.Key "user" ]; Error = "left-user" }
                    { Path = [ PathSegment.Key "user" ]; Error = "right-user" }
                    { Path = [ PathSegment.Key "user"; PathSegment.Key "address" ]; Error = "left-address" }
                    { Path = [ PathSegment.Key "user"; PathSegment.Key "address" ]; Error = "right-address" }
                ]

            test <@ Diagnostics.flatten merged = expectedMerged @>
            test <@ (Diagnostics.toString merged |> fun text -> text.Contains("user:")) @>
            test <@ (Diagnostics.toString merged |> fun text -> text.Contains("Errors:") |> not) @>

        [<Fact>]
        let ``scoped validation prefixes nested field and list paths`` () =
            let validateAddress address =
                validate.key "address" {
                    let! city =
                        validate.name "City" {
                            return! address.City |> Result.keepIf Check.notBlank "City required"
                        }

                    return { address with City = city }
                }

            let validateCustomer customer =
                validate.key "customer" {
                    let! name =
                        validate.name "Name" {
                            return! customer.Name |> Result.keepIf Check.notBlank "Name required"
                        }

                    and! address = validateAddress customer.Address

                    and! lines =
                        Validation.key "lines" (
                            customer.Lines
                            |> Validation.traverseIndexed (fun index line ->
                                validate.name "Name" {
                                    return! line |> Result.keepIf Check.notBlank $"Line {index} name required"
                                }
                            )
                        )

                    return
                        { customer with
                            Name = name
                            Address = address
                            Lines = lines }
                }

            let result =
                validateCustomer
                    { Name = ""
                      Address = { City = "" }
                      Lines = [ "" ] }
                |> Validation.toResult

            match result with
            | Ok _ -> failwith "expected scoped validation to fail"
            | Error diagnostics ->
                let flattened = Diagnostics.flatten diagnostics
                let expected =
                    [
                        { Path =
                            [
                                PathSegment.Key "customer"
                                PathSegment.Key "address"
                                PathSegment.Name "City"
                            ]
                          Error = "City required" }
                        { Path =
                            [
                                PathSegment.Key "customer"
                                PathSegment.Key "lines"
                                PathSegment.Index 0
                                PathSegment.Name "Name"
                            ]
                          Error = "Line 0 name required" }
                        { Path = [ PathSegment.Key "customer"; PathSegment.Name "Name" ]
                          Error = "Name required" }
                    ]

                Assert.Equal<Diagnostic<string> list>(expected, flattened)

        [<Fact>]
        let ``validate computation expression accumulates sibling failures and short-circuits sequentially`` () =
            let makeDiagnostic error =
                {
                    Path = []
                    Error = error
                }

            let leftRuns = ref 0
            let rightRuns = ref 0
            let sequentialRuns = ref 0

            let mergedWorkflow : Validation<int, string> =
                validate {
                    let! left =
                        (leftRuns.Value <- leftRuns.Value + 1
                         Validation.fail (Diagnostics.singleton "left"))

                    and! right =
                        (rightRuns.Value <- rightRuns.Value + 1
                         Validation.fail (Diagnostics.singleton "right"))

                    return left + right
                }

            let sequentialWorkflow : Validation<int, string> =
                validate {
                    let! _ = Validation.fail (Diagnostics.singleton "parse")
                    let! _ =
                        (sequentialRuns.Value <- sequentialRuns.Value + 1
                         Validation.fail (Diagnostics.singleton "should-not-run"))
                    return 0
                }

            let liftedResultWorkflow : Validation<int, string> =
                validate {
                    let! value = Ok 21
                    return value + 1
                }

            let mergedResult = Validation.toResult mergedWorkflow
            let sequentialResult = Validation.toResult sequentialWorkflow
            let liftedResult = Validation.toResult liftedResultWorkflow
            let expectedSequential = Error (Diagnostics.singleton "parse")

            test <@ typeof<Validation<int, string>>.Name = "Validation`2" @>
            match mergedResult with
            | Ok _ -> failwith "expected merged workflow to fail"
            | Error diagnostics ->
                let flattened = Diagnostics.flatten diagnostics
                if flattened <> [ makeDiagnostic "left"; makeDiagnostic "right" ] then
                    failwith "expected merged workflow diagnostics to flatten in order"

            test <@ leftRuns.Value = 1 @>
            test <@ rightRuns.Value = 1 @>
            test <@ sequentialResult = expectedSequential @>
            test <@ sequentialRuns.Value = 0 @>
            test <@ liftedResult = Ok 22 @>

        [<Fact>]
        let ``validation normalizes constructors, applicative helpers, and fallbacks`` () =
            let okValue = Validation.ok 41
            let okAlias = Validation.succeed 41
            let errorValue = Validation.error (Diagnostics.singleton "missing")
            let errorAlias = Validation.fail (Diagnostics.singleton "missing")

            let mapped = Validation.(<!>) ((+) 1) okValue
            let applied = Validation.(<*>) (Validation.ok ((+) 1)) okValue
            let mapped3 =
                Validation.map3 (fun left middle right -> left + middle + right) (Validation.ok 1) (Validation.ok 2) (Validation.ok 3)

            let ignored = Validation.ignore okValue

            let fallbackRuns = ref 0
            let recovered =
                Validation.orElse (Validation.ok 99) errorValue

            let recoveredWith =
                Validation.orElseWith
                    (fun diagnostics ->
                        fallbackRuns.Value <- fallbackRuns.Value + 1
                        Validation.ok diagnostics.Errors.Length)
                    errorValue

            let lazyFallback =
                Validation.orElseWith
                    (fun _ ->
                        fallbackRuns.Value <- fallbackRuns.Value + 1
                        Validation.ok 0)
                    okValue

            test <@ okValue = okAlias @>
            test <@ errorValue = errorAlias @>
            test <@ mapped = Validation.ok 42 @>
            test <@ applied = Validation.ok 42 @>
            test <@ mapped3 = Validation.ok 6 @>
            test <@ ignored = Validation.ok () @>
            test <@ recovered = Validation.ok 99 @>
            test <@ recoveredWith = Validation.ok 1 @>
            test <@ lazyFallback = okValue @>
            test <@ fallbackRuns.Value = 1 @>
