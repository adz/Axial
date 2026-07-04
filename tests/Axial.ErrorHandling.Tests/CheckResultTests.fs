namespace Axial.Tests

open System
open Microsoft.FSharp.Core
open Axial.ErrorHandling
open Swensen.Unquote
open Xunit

module CheckResultTests =
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
            test <@ Check.Number.positive 0 = Error [ Positive(Some "0") ] @>
            test <@ Check.Number.nonNegative 0 = Ok () @>
            test <@ Check.Number.nonNegative -1 = Error [ NonNegative(Some "-1") ] @>
            test <@ Check.Number.negative -1 = Ok () @>
            test <@ Check.Number.negative 0 = Error [ Negative(Some "0") ] @>
            test <@ Check.Number.nonPositive 0 = Ok () @>
            test <@ Check.Number.nonPositive 1 = Error [ NonPositive(Some "1") ] @>

        [<Fact>]
        let ``Check Seq behavior accumulates count and distinct failures`` () =
            let nullValues: seq<int> = null

            let seqCheck : Check<seq<int>> =
                Check.all
                    [
                        Check.Seq.minCount 2
                        Check.Seq.maxCount 3
                        Check.Seq.noDuplicates
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
            test <@ Check.Number.nonPositive 0.1m = Error [ NonPositive(Some "0.1") ] @>

        [<Fact>]
        let ``Check Seq exposes executable sequence value checks`` () =
            let nullValues: seq<int> = null

            test <@ Check.Seq.notEmpty [ 1 ] = Ok () @>
            test <@ Check.Seq.notEmpty [] = Error [ NonEmpty(Some 0) ] @>
            test <@ Check.Seq.notEmpty nullValues = Error [ NonEmpty None ] @>

            test <@ Check.Seq.empty [] = Ok () @>
            test <@ Check.Seq.empty [ 1 ] = Error [ Count(ExactCount 0, Some 1) ] @>
            test <@ Check.Seq.empty nullValues = Error [ Count(ExactCount 0, None) ] @>

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

            test <@ Check.Seq.noDuplicates [ 1; 2; 3 ] = Ok () @>
            test <@ Check.Seq.noDuplicates [ 1; 2; 1 ] = Error [ CustomCode "seq.distinct" ] @>
            test <@ Check.Seq.noDuplicates nullValues = Error [ Missing ] @>

            test <@ Check.Seq.contains 2 [ 1; 2 ] = Ok () @>
            test <@ Check.Seq.contains 3 [ 1; 2 ] = Error [ Equality(EqualTo "3", None) ] @>
            test <@ Check.Seq.contains 3 nullValues = Error [ Missing ] @>
            test <@ Check.Seq.single [ 1 ] = Ok () @>
            test <@ Check.Seq.single [ 1; 2 ] = Error [ Count(ExactCount 1, Some 2) ] @>
            test <@ Check.Seq.atMostOne [ 1; 2 ] = Error [ Count(MaximumCount 1, Some 2) ] @>
            test <@ Check.Seq.atLeastOne [] = Error [ NonEmpty(Some 0) ] @>
            test <@ Check.Seq.moreThanOne [ 1 ] = Error [ Count(MinimumCount 2, Some 1) ] @>

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
            test <@ Check.positive 0 = Error [ Positive(Some "0") ] @>
            test <@ Check.nonNegative 0 = Ok () @>
            test <@ Check.nonNegative -1 = Error [ NonNegative(Some "-1") ] @>
            test <@ Check.negative -1 = Ok () @>
            test <@ Check.negative 0 = Error [ Negative(Some "0") ] @>
            test <@ Check.nonPositive 0 = Ok () @>
            test <@ Check.nonPositive 1 = Error [ NonPositive(Some "1") ] @>

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
            test <@ Check.atLeastOne [] = Error [ NonEmpty(Some 0) ] @>
            test <@ Check.moreThanOne [ 1 ] = Error [ Count(MinimumCount 2, Some 1) ] @>

            test <@ Check.equalTo 3 3 = Ok () @>
            test <@ Check.equalTo 3 4 = Error [ Equality(EqualTo "3", Some "4") ] @>
            test <@ Check.notEqualTo 3 4 = Ok () @>
            test <@ Check.notEqualTo 3 3 = Error [ Equality(NotEqualTo "3", Some "3") ] @>

        [<Fact>]
        let ``Check top-level string facades match direct module behavior`` () =
            let nullString: string = null

            let assertSame (direct: Check<string>) (facade: Check<string>) samples =
                for sample in samples do
                    Assert.Equal<Result<unit, CheckFailure list>>(direct sample, facade sample)

            assertSame (Check.String.length 3) (Check.length 3) [ "Ada"; "Axial"; nullString ]
            assertSame (Check.String.minLength 3) (Check.minLength 3) [ "Ada"; "Al"; nullString ]
            assertSame (Check.String.maxLength 3) (Check.maxLength 3) [ "Ada"; "Axial"; nullString ]
            assertSame (Check.String.lengthBetween 2 4) (Check.lengthBetween 2 4) [ "Ada"; "A"; "Axial"; nullString ]
            assertSame Check.String.email Check.email [ "ada@example.com"; "Ada"; nullString ]
            assertSame (Check.String.matches "^[a-z]+$") (Check.matches "^[a-z]+$") [ "ada"; "Ada"; nullString ]
            assertSame (Check.String.oneOf [ "draft"; "published" ]) (Check.oneOf [ "draft"; "published" ]) [ "draft"; "archived"; nullString ]

        [<Fact>]
        let ``Check top-level numeric facades match direct module behavior`` () =
            let assertSame (direct: Check<int>) (facade: Check<int>) samples =
                for sample in samples do
                    Assert.Equal<Result<unit, CheckFailure list>>(direct sample, facade sample)

            assertSame (Check.Number.between 1 3) (Check.between 1 3) [ 0; 1; 3; 4 ]
            assertSame (Check.Number.greaterThan 1) (Check.greaterThan 1) [ 1; 2 ]
            assertSame (Check.Number.lessThan 3) (Check.lessThan 3) [ 2; 3 ]
            assertSame (Check.Number.atLeast 3) (Check.atLeast 3) [ 2; 3 ]
            assertSame (Check.Number.atMost 3) (Check.atMost 3) [ 3; 4 ]
            assertSame Check.Number.positive Check.positive [ 0; 1 ]
            assertSame Check.Number.nonNegative Check.nonNegative [ -1; 0 ]
            assertSame Check.Number.negative Check.negative [ -1; 0 ]
            assertSame Check.Number.nonPositive Check.nonPositive [ 0; 1 ]

        [<Fact>]
        let ``Check top-level sequence facades match direct module behavior`` () =
            let nullValues: seq<int> = null

            let assertSame (direct: Check<seq<int>>) (facade: Check<seq<int>>) samples =
                for sample in samples do
                    Assert.Equal<Result<unit, CheckFailure list>>(direct sample, facade sample)

            assertSame (Check.Seq.count 2) (Check.count 2) [ seq [ 1; 2 ]; seq [ 1 ]; nullValues ]
            assertSame (Check.Seq.minCount 2) (Check.minCount 2) [ seq [ 1; 2 ]; seq [ 1 ]; nullValues ]
            assertSame (Check.Seq.maxCount 2) (Check.maxCount 2) [ seq [ 1; 2 ]; seq [ 1; 2; 3 ]; nullValues ]
            assertSame (Check.Seq.countBetween 2 4) (Check.countBetween 2 4) [ seq [ 1; 2; 3 ]; seq [ 1 ]; nullValues ]
            assertSame Check.Seq.noDuplicates Check.distinct [ seq [ 1; 2; 3 ]; seq [ 1; 2; 1 ]; nullValues ]
            assertSame (Check.Seq.contains 2) (Check.contains 2) [ seq [ 1; 2 ]; seq [ 1; 3 ]; nullValues ]
            assertSame Check.Seq.single Check.single [ seq [ 1 ]; seq []; seq [ 1; 2 ]; nullValues ]
            assertSame Check.Seq.atMostOne Check.atMostOne [ seq []; seq [ 1 ]; seq [ 1; 2 ]; nullValues ]
            assertSame Check.Seq.atLeastOne Check.atLeastOne [ seq []; seq [ 1 ]; nullValues ]
            assertSame Check.Seq.moreThanOne Check.moreThanOne [ seq [ 1 ]; seq [ 1; 2 ]; nullValues ]

        [<Fact>]
        let ``Check top-level presence facade delegates to direct modules`` () =
            let nullString: string = null
            let emptyValues: int list = []
            let values: int list = [ 1 ]
            let nullValues: int array = null

            Assert.Equal<Result<unit, CheckFailure list>>(Check.String.present "Ada", Check.present "Ada")
            Assert.Equal<Result<unit, CheckFailure list>>(Check.String.present nullString, Check.present nullString)
            Assert.Equal<Result<unit, CheckFailure list>>(Check.String.present "", Check.present "")
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Option.present (Some 1), Check.present (Some 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Option.present (None: int option), Check.present (None: int option))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.ValueOption.present (ValueSome 1), Check.present (ValueSome 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.ValueOption.present (ValueNone: int voption), Check.present (ValueNone: int voption))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Nullable.present (System.Nullable 1), Check.present (System.Nullable 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Nullable.present (System.Nullable<int>()), Check.present (System.Nullable<int>()))

            Assert.Equal<Result<unit, CheckFailure list>>(Check.String.empty "", Check.empty "")
            Assert.Equal<Result<unit, CheckFailure list>>(Check.String.empty " ", Check.empty " ")
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Option.empty (None: int option), Check.empty (None: int option))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Option.empty (Some 1), Check.empty (Some 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.ValueOption.empty (ValueNone: int voption), Check.empty (ValueNone: int voption))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.ValueOption.empty (ValueSome 1), Check.empty (ValueSome 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Nullable.empty (System.Nullable<int>()), Check.empty (System.Nullable<int>()))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Nullable.empty (System.Nullable 1), Check.empty (System.Nullable 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Seq.empty emptyValues, Check.empty emptyValues)
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Seq.empty values, Check.empty values)
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Seq.empty nullValues, Check.empty nullValues)

            Assert.Equal<Result<unit, CheckFailure list>>(Check.String.notEmpty " ", Check.notEmpty " ")
            Assert.Equal<Result<unit, CheckFailure list>>(Check.String.notEmpty "", Check.notEmpty "")
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Option.notEmpty (Some 1), Check.notEmpty (Some 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Option.notEmpty (None: int option), Check.notEmpty (None: int option))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.ValueOption.notEmpty (ValueSome 1), Check.notEmpty (ValueSome 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.ValueOption.notEmpty (ValueNone: int voption), Check.notEmpty (ValueNone: int voption))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Nullable.notEmpty (System.Nullable 1), Check.notEmpty (System.Nullable 1))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Nullable.notEmpty (System.Nullable<int>()), Check.notEmpty (System.Nullable<int>()))
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Seq.notEmpty values, Check.notEmpty values)
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Seq.notEmpty emptyValues, Check.notEmpty emptyValues)
            Assert.Equal<Result<unit, CheckFailure list>>(Check.Seq.notEmpty nullValues, Check.notEmpty nullValues)

        [<Fact>]
        let ``Check composition accepts tightened top-level checks`` () =
            let requiredName =
                Check.all [ Check.present; Check.lengthBetween 2 40 ]

            test <@ requiredName "Ada" = Ok () @>
            test <@ requiredName "" = Error [ Blank; Length(LengthBetween(2, 40), Some 0) ] @>

            let nullString: string = null

            test <@ requiredName nullString = Error [ Missing; Length(LengthBetween(2, 40), None) ] @>

            let shortCode =
                Check.any [ Check.length 2; Check.length 3 ]

            test <@ shortCode "US" = Ok () @>
            test <@ shortCode "USA" = Ok () @>
            test <@ shortCode "United States" = Error [ Length(ExactLength 2, Some 13); Length(ExactLength 3, Some 13) ] @>

            let requiredDistinctIds =
                Check.all [ Check.notEmpty; Check.distinct; Check.maxCount 3 ]

            test <@ requiredDistinctIds [ 1; 2; 3 ] = Ok () @>
            test <@ requiredDistinctIds [] = Error [ NonEmpty(Some 0) ] @>
            test <@ requiredDistinctIds [ 1; 2; 1; 3 ] = Error [ CustomCode "seq.distinct"; Count(MaximumCount 3, Some 4) ] @>

        [<Fact>]
        let ``Check Option exposes executable option value checks`` () =
            test <@ Check.Option.some (Some 1) = Ok () @>
            test <@ Check.Option.some None = Error [ Missing ] @>

            test <@ Check.Option.none None = Ok () @>
            test <@ Check.Option.none (Some 1) = Error [ Equality(EqualTo "None", Some "Some") ] @>

            test <@ Check.Option.present (Some 1) = Ok () @>
            test <@ Check.Option.present None = Error [ Missing ] @>

            test <@ Check.Option.empty None = Ok () @>
            test <@ Check.Option.empty (Some 1) = Error [ Equality(EqualTo "None", Some "Some") ] @>

            test <@ Check.Option.notEmpty (Some 1) = Ok () @>
            test <@ Check.Option.notEmpty None = Error [ Missing ] @>

        [<Fact>]
        let ``Check ValueOption exposes executable value option checks`` () =
            test <@ Check.ValueOption.some (ValueSome 1) = Ok () @>
            test <@ Check.ValueOption.some ValueNone = Error [ Missing ] @>

            test <@ Check.ValueOption.none ValueNone = Ok () @>
            test <@ Check.ValueOption.none (ValueSome 1) = Error [ Equality(EqualTo "ValueNone", Some "ValueSome") ] @>

            test <@ Check.ValueOption.present (ValueSome 1) = Ok () @>
            test <@ Check.ValueOption.present ValueNone = Error [ Missing ] @>

            test <@ Check.ValueOption.empty ValueNone = Ok () @>
            test <@ Check.ValueOption.empty (ValueSome 1) = Error [ Equality(EqualTo "ValueNone", Some "ValueSome") ] @>

            test <@ Check.ValueOption.notEmpty (ValueSome 1) = Ok () @>
            test <@ Check.ValueOption.notEmpty ValueNone = Error [ Missing ] @>

        [<Fact>]
        let ``Check Nullable exposes executable nullable value checks`` () =
            test <@ Check.Nullable.hasValue (System.Nullable 1) = Ok () @>
            test <@ Check.Nullable.hasValue (System.Nullable<int>()) = Error [ Missing ] @>

            test <@ Check.Nullable.hasNoValue (System.Nullable<int>()) = Ok () @>
            test <@ Check.Nullable.hasNoValue (System.Nullable 1) = Error [ Equality(EqualTo "null", Some "value") ] @>

            test <@ Check.Nullable.present (System.Nullable 1) = Ok () @>
            test <@ Check.Nullable.present (System.Nullable<int>()) = Error [ Missing ] @>

            test <@ Check.Nullable.empty (System.Nullable<int>()) = Ok () @>
            test <@ Check.Nullable.empty (System.Nullable 1) = Error [ Equality(EqualTo "null", Some "value") ] @>

            test <@ Check.Nullable.notEmpty (System.Nullable 1) = Ok () @>
            test <@ Check.Nullable.notEmpty (System.Nullable<int>()) = Error [ Missing ] @>

        [<Fact>]
        let ``Check Result exposes executable result value checks`` () =
            test <@ Check.Result.ok (Ok 1) = Ok () @>
            test <@ Check.Result.ok (Error "missing") = Error [ Equality(EqualTo "Ok", Some "Error") ] @>

            test <@ Check.Result.error (Error "missing") = Ok () @>
            test <@ Check.Result.error (Ok 1) = Error [ Equality(EqualTo "Error", Some "Ok") ] @>

        [<Fact>]
        let ``Predicate exposes boolean helpers outside structured Check`` () =
            let nullString: string = null
            let nullValues: seq<int> = null

            test <@ Predicate.Option.isSome (Some 1) @>
            test <@ Predicate.Option.isNone (None: int option) @>
            test <@ Predicate.Option.present (Some 1) @>
            test <@ Predicate.Option.empty (None: int option) @>
            test <@ Predicate.Option.notEmpty (Some 1) @>

            test <@ Predicate.ValueOption.isSome (ValueSome 1) @>
            test <@ Predicate.ValueOption.isNone (ValueNone: int voption) @>
            test <@ Predicate.ValueOption.present (ValueSome 1) @>
            test <@ Predicate.ValueOption.empty (ValueNone: int voption) @>
            test <@ Predicate.ValueOption.notEmpty (ValueSome 1) @>

            test <@ Predicate.Nullable.hasValue (System.Nullable 1) @>
            test <@ Predicate.Nullable.hasNoValue (System.Nullable<int>()) @>
            test <@ Predicate.Nullable.present (System.Nullable 1) @>
            test <@ Predicate.Nullable.empty (System.Nullable<int>()) @>
            test <@ Predicate.Nullable.notEmpty (System.Nullable 1) @>

            test <@ Predicate.Result.isOk (Ok 1) @>
            test <@ Predicate.Result.isError (Error "missing") @>

            test <@ Predicate.Reference.isNull nullString @>
            test <@ Predicate.Reference.notNull "Ada" @>

            test <@ Predicate.String.empty "" @>
            test <@ not (Predicate.String.empty nullString) @>
            test <@ Predicate.String.notEmpty " " @>
            test <@ Predicate.String.blank "   " @>
            test <@ Predicate.String.blank nullString @>
            test <@ Predicate.String.notBlank "Ada" @>
            test <@ Predicate.String.minLength 3 "Ada" @>
            test <@ Predicate.String.maxLength 3 "Ada" @>
            test <@ Predicate.String.length 3 "Ada" @>
            test <@ Predicate.String.lengthBetween 2 4 "Ada" @>
            test <@ Predicate.String.matches "^[a-z]+$" "ada" @>
            test <@ Predicate.String.email "ada@example.com" @>
            test <@ Predicate.String.numeric "123" @>
            test <@ Predicate.String.alphaNumeric "Ada123" @>
            test <@ not (Predicate.String.alphaNumeric "Ada-123") @>

            test <@ Predicate.Seq.empty [] @>
            test <@ not (Predicate.Seq.empty nullValues) @>
            test <@ Predicate.Seq.notEmpty [ 1 ] @>
            test <@ Predicate.Seq.contains 2 [ 1; 2 ] @>
            test <@ Predicate.Seq.count 2 [ 1; 2 ] @>
            test <@ Predicate.Seq.minCount 2 [ 1; 2 ] @>
            test <@ Predicate.Seq.maxCount 2 [ 1; 2 ] @>
            test <@ Predicate.Seq.countBetween 1 3 [ 1; 2 ] @>
            test <@ Predicate.Seq.single [ 1 ] @>
            test <@ Predicate.Seq.atMostOne [] @>
            test <@ Predicate.Seq.atLeastOne [ 1 ] @>
            test <@ Predicate.Seq.moreThanOne [ 1; 2 ] @>
            test <@ Predicate.Seq.duplicates [ 1; 2; 1 ] @>
            test <@ Predicate.Seq.distinct [ 1; 2; 3 ] @>
            test <@ not (Predicate.Seq.distinct [ 1; 2; 1 ]) @>
            test <@ not (Predicate.Seq.distinct nullValues) @>

            test <@ Predicate.Number.greaterThan 3 4 @>
            test <@ Predicate.Number.lessThan 3 2 @>
            test <@ Predicate.Number.atLeast 3 3 @>
            test <@ Predicate.Number.atMost 3 3 @>
            test <@ Predicate.Number.between 1 3 2 @>
            test <@ Predicate.Number.positive 1 @>
            test <@ Predicate.Number.nonNegative 0 @>
            test <@ Predicate.Number.negative -1 @>
            test <@ Predicate.Number.nonPositive 0 @>

        [<Fact>]
        let ``Check top-level facade exposes structured checks`` () =
            let nullString: string = null

            Assert.Equal<Result<unit, CheckFailure list>>(Ok (), Check.present "Ada")
            Assert.Equal<Result<unit, CheckFailure list>>(Error [ Missing ], Check.present nullString)
            Assert.Equal<Result<unit, CheckFailure list>>(Ok (), Check.empty "")
            Assert.Equal<Result<unit, CheckFailure list>>(Ok (), Check.notEmpty "  ")
            test <@ Check.length 3 "abc" = Ok () @>
            test <@ Check.email "ada@example.com" = Ok () @>
            test <@ Check.matches "^[a-z]+$" "abc" = Ok () @>
            test <@ Check.count 2 [ 1; 2 ] = Ok () @>
            test <@ Check.distinct [ 1; 2; 3 ] = Ok () @>
            test <@ Check.single [ 5 ] = Ok () @>

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
            test <@ ("Ada" |> Result.keepIf (String.IsNullOrWhiteSpace >> not) "required") = Ok "Ada" @>
            test <@ ("" |> Result.keepIf (String.IsNullOrWhiteSpace >> not) "required") = Error "required" @>
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
            test <@ ("Ada" |> Result.keepIf (String.IsNullOrWhiteSpace >> not) "required") = Ok "Ada" @>
            test <@ ("Ada" |> Result.keepIf (fun value -> not (obj.ReferenceEquals(value, null))) "required") = Ok "Ada" @>
            test <@ ([ 1; 2 ] |> Result.keepIf (Seq.isEmpty >> not) "required") = Ok [ 1; 2 ] @>
            test <@ ([ 1; 2 ] |> Result.keepIf (Seq.contains 2) "missing") = Ok [ 1; 2 ] @>
            test <@ ([ 1; 2; 3 ] |> Result.keepIf (fun values -> (Check.distinct values).IsOk) "duplicate") = Ok [ 1; 2; 3 ] @>
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
            test <@ ([] |> Result.atLeastOne) = Error [ NonEmpty(Some 0) ] @>
            test <@ ([ 5 ] |> Result.moreThanOne) = Error [ Count(MinimumCount 2, Some 1) ] @>
            test <@ Collection.traverseResult (fun value -> if value < 3 then Ok(value * 2) else Error value) [ 1; 2 ] = Ok [ 2; 4 ] @>
            test <@ Collection.sequenceResult [ Ok 1; Error "missing"; Ok 3 ] = Error "missing" @>
            test <@ workflow = Ok 10 @>
