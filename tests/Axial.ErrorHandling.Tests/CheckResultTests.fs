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
                    if value = "valid" then Ok value
                    else Error []

            test <@ check "valid" = Ok "valid" @>
            test <@ check "invalid" = Error [] @>

        [<Fact>]
        let ``CheckFailure exposes structured value constraint cases`` () =
            let failures =
                [ Required
                  Required
                  InvalidFormat "email"
                  InvalidLength(MinimumLength 3, Some 2)
                  OutOfRange(Between("1", "10"), Some "12")
                  InvalidCount(CountBetween(1, 3), Some 0)
                  NotOneOf "expected"
                  Custom "domain.rule" ]

            test
                <@
                    failures =
                        [ Required
                          Required
                          InvalidFormat "email"
                          InvalidLength(MinimumLength 3, Some 2)
                          OutOfRange(Between("1", "10"), Some "12")
                          InvalidCount(CountBetween(1, 3), Some 0)
                          NotOneOf "expected"
                          Custom "domain.rule" ]
                @>

        [<Fact>]
        let ``Check composition accumulates alternatives and maps failures`` () =
            let missingWhenEmpty : Check<string> =
                fun value -> if value = "" then Error [ Required ] else Ok value

            let blankWhenWhitespace : Check<string> =
                fun value -> if value.Trim() = "" then Error [ Required ] else Ok value

            let invalidWhenNotEmail : Check<string> =
                fun value ->
                    if value.Contains("@") then Ok value
                    else Error [ InvalidFormat "email" ]

            let invalidWhenNotPhone : Check<string> =
                fun value ->
                    if value.StartsWith("+") then Ok value
                    else Error [ InvalidFormat "phone" ]

            test <@ Check.all [ missingWhenEmpty; blankWhenWhitespace ] "" = Error [ Required; Required ] @>
            test <@ Check.all [ missingWhenEmpty; blankWhenWhitespace ] "Ada" = Ok "Ada" @>
            test <@ Check.all [] "Ada" = Ok "Ada" @>
            test <@ Check.any [ invalidWhenNotEmail; invalidWhenNotPhone ] "ada@example.com" = Ok "ada@example.com" @>
            test <@ Check.any [ invalidWhenNotEmail; invalidWhenNotPhone ] "Ada" = Error [ InvalidFormat "email"; InvalidFormat "phone" ] @>
            test <@ Check.any [] "Ada" = Error [] @>
            test <@ Check.not invalidWhenNotEmail "Ada" = Ok "Ada" @>
            test <@ Check.not invalidWhenNotEmail "ada@example.com" = Error [ Custom "check.not" ] @>

            test
                <@
                    Check.mapFailure (function
                        | InvalidFormat expected -> Custom $"format.{expected}"
                        | failure -> failure) invalidWhenNotEmail "Ada" = Error [ Custom "format.email" ]
                @>

        [<Fact>]
        let ``Check all evaluates every check and preserves accumulated failure order`` () =
            let calls = ResizeArray<string>()

            let failWith name failure : Check<string> =
                fun _ ->
                    calls.Add name
                    Error [ failure ]

            let passWith name : Check<string> =
                fun value ->
                    calls.Add name
                    Ok value

            let check =
                Check.all
                    [
                        failWith "first" Required
                        passWith "second"
                        failWith "third" Required
                        failWith "fourth" (InvalidFormat "email")
                    ]

            test <@ check "" = Error [ Required; Required; InvalidFormat "email" ] @>
            test <@ calls |> Seq.toList = [ "first"; "second"; "third"; "fourth" ] @>

        [<Fact>]
        let ``Check any accumulates failed alternatives and short-circuits after success`` () =
            let calls = ResizeArray<string>()

            let failWith name failure : Check<string> =
                fun _ ->
                    calls.Add name
                    Error [ failure ]

            let passWith name : Check<string> =
                fun value ->
                    calls.Add name
                    Ok value

            let firstSuccess =
                Check.any
                    [
                        failWith "email" (InvalidFormat "email")
                        failWith "phone" (InvalidFormat "phone")
                        passWith "username"
                        failWith "later" (Custom "unreachable")
                    ]

            test <@ firstSuccess "ada" = Ok "ada" @>
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

            test <@ requiredEmail nullString = Error [ Required; InvalidFormat "email"; InvalidLength(LengthBetween(5, 20), None) ] @>
            test <@ requiredEmail "" = Error [ Required; InvalidFormat "email"; InvalidLength(LengthBetween(5, 20), Some 0) ] @>
            test <@ requiredEmail "   " = Error [ Required; InvalidFormat "email"; InvalidLength(LengthBetween(5, 20), Some 3) ] @>
            test <@ requiredEmail "ada" = Error [ InvalidFormat "email"; InvalidLength(LengthBetween(5, 20), Some 3) ] @>
            test <@ requiredEmail "ada@example.com" = Ok "ada@example.com" @>

        [<Fact>]
        let ``Check Number behavior keeps inclusive and exclusive range boundaries distinct`` () =
            test <@ Check.Number.between 1 3 1 = Ok 1 @>
            test <@ Check.Number.between 1 3 3 = Ok 3 @>
            test <@ Check.Number.between 1 3 0 = Error [ OutOfRange(Between("1", "3"), Some "0") ] @>
            test <@ Check.Number.between 1 3 4 = Error [ OutOfRange(Between("1", "3"), Some "4") ] @>

            test <@ Check.Number.greaterThan 1 1 = Error [ OutOfRange(GreaterThan "1", Some "1") ] @>
            test <@ Check.Number.greaterThan 1 2 = Ok 2 @>
            test <@ Check.Number.lessThan 3 3 = Error [ OutOfRange(LessThan "3", Some "3") ] @>
            test <@ Check.Number.lessThan 3 2 = Ok 2 @>
            test <@ Check.Number.atLeast 1 1 = Ok 1 @>
            test <@ Check.Number.atMost 3 3 = Ok 3 @>
            test <@ Check.Number.positive 1 = Ok 1 @>
            test <@ Check.Number.positive 0 = Error [ OutOfRange(GreaterThan "0", Some "0") ] @>
            test <@ Check.Number.nonNegative 0 = Ok 0 @>
            test <@ Check.Number.nonNegative -1 = Error [ OutOfRange(AtLeast "0", Some "-1") ] @>
            test <@ Check.Number.negative -1 = Ok -1 @>
            test <@ Check.Number.negative 0 = Error [ OutOfRange(LessThan "0", Some "0") ] @>
            test <@ Check.Number.nonPositive 0 = Ok 0 @>
            test <@ Check.Number.nonPositive 1 = Error [ OutOfRange(AtMost "0", Some "1") ] @>

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

            test <@ seqCheck [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>
            test <@ seqCheck [] = Error [ InvalidCount(MinimumCount 2, Some 0) ] @>
            test <@ seqCheck [ 1; 2; 1; 3 ] = Error [ InvalidCount(MaximumCount 3, Some 4); Duplicate ] @>
            test <@ seqCheck nullValues = Error [ InvalidCount(MinimumCount 2, None); InvalidCount(MaximumCount 3, None); Required ] @>

        [<Fact>]
        let ``Check Option and Result behavior composes with all and any`` () =
            test <@ Check.all [ Check.Option.some; Check.not Check.Option.none ] (Some 1) = Ok(Some 1) @>
            test <@ Check.all [ Check.Option.some; Check.not Check.Option.none ] None = Error [ Required; Custom "check.not" ] @>
            test <@ Check.any [ Check.Option.none; Check.Option.some ] (Some 1) = Ok(Some 1) @>
            test <@ Check.any [ Check.Option.none; Check.Option.some ] (None: int option) = Ok None @>

            test <@ Check.all [ Check.Result.ok; Check.not Check.Result.error ] (Ok 1) = Ok(Ok 1) @>
            test
                <@
                    Check.all [ Check.Result.ok; Check.not Check.Result.error ] (Error "missing") =
                        Error [ NotOneOf "Ok"; Custom "check.not" ]
                @>
            test <@ Check.any [ Check.Result.error; Check.Result.ok ] (Error "missing") = Ok(Error "missing") @>
            test <@ Check.any [ Check.Result.error; Check.Result.ok ] (Ok 1) = Ok(Ok 1) @>

        [<Fact>]
        let ``Check String exposes executable string value checks`` () =
            let nullString: string = null

            test <@ Check.String.present "Ada" = Ok "Ada" @>
            test <@ Check.String.present nullString = Error [ Required ] @>
            test <@ Check.String.present "" = Error [ Required ] @>
            test <@ Check.String.present "   " = Error [ Required ] @>

            test <@ Check.String.empty "" = Ok "" @>
            test <@ Check.String.empty " " = Error [ InvalidLength(ExactLength 0, Some 1) ] @>
            test <@ Check.String.empty nullString = Error [ Required ] @>

            test <@ Check.String.notEmpty " " = Ok " " @>
            test <@ Check.String.notEmpty "" = Error [ InvalidLength(MinimumLength 1, Some 0) ] @>
            test <@ Check.String.notEmpty nullString = Error [ Required ] @>

            test <@ Check.String.minLength 3 "Ada" = Ok "Ada" @>
            test <@ Check.String.minLength 3 "Al" = Error [ InvalidLength(MinimumLength 3, Some 2) ] @>
            test <@ Check.String.minLength 3 nullString = Error [ InvalidLength(MinimumLength 3, None) ] @>

            test <@ Check.String.maxLength 3 "Ada" = Ok "Ada" @>
            test <@ Check.String.maxLength 3 "Axial" = Error [ InvalidLength(MaximumLength 3, Some 5) ] @>
            test <@ Check.String.maxLength 3 nullString = Error [ InvalidLength(MaximumLength 3, None) ] @>

            test <@ Check.String.lengthBetween 2 4 "Ada" = Ok "Ada" @>
            test <@ Check.String.lengthBetween 2 4 "A" = Error [ InvalidLength(LengthBetween(2, 4), Some 1) ] @>
            test <@ Check.String.lengthBetween 2 4 "Axial" = Error [ InvalidLength(LengthBetween(2, 4), Some 5) ] @>
            test <@ Check.String.lengthBetween 2 4 nullString = Error [ InvalidLength(LengthBetween(2, 4), None) ] @>

            test <@ Check.String.length 3 "Ada" = Ok "Ada" @>
            test <@ Check.String.length 3 "Axial" = Error [ InvalidLength(ExactLength 3, Some 5) ] @>
            test <@ Check.String.length 3 nullString = Error [ InvalidLength(ExactLength 3, None) ] @>
            test <@ Check.String.exactLength 3 "Ada" = Ok "Ada" @>

            test <@ Check.String.email "ada@example.com" = Ok "ada@example.com" @>
            test <@ Check.String.email "Ada" = Error [ InvalidFormat "email" ] @>
            test <@ Check.String.email nullString = Error [ InvalidFormat "email" ] @>

            test <@ Check.String.matches "^[a-z]+$" "ada" = Ok "ada" @>
            test <@ Check.String.matches "^[a-z]+$" "Ada" = Error [ InvalidFormat "^[a-z]+$" ] @>
            test <@ Check.String.matches "^[a-z]+$" nullString = Error [ InvalidFormat "^[a-z]+$" ] @>

            test <@ Check.String.numeric "12345" = Ok "12345" @>
            test <@ Check.String.numeric "12a45" = Error [ InvalidFormat "numeric" ] @>
            test <@ Check.String.numeric "" = Error [ InvalidFormat "numeric" ] @>
            test <@ Check.String.numeric nullString = Error [ InvalidFormat "numeric" ] @>

            test <@ Check.String.alphaNumeric "Ada123" = Ok "Ada123" @>
            test <@ Check.String.alphaNumeric "Ada-123" = Error [ InvalidFormat "alphaNumeric" ] @>
            test <@ Check.String.alphaNumeric "" = Error [ InvalidFormat "alphaNumeric" ] @>
            test <@ Check.String.alphaNumeric nullString = Error [ InvalidFormat "alphaNumeric" ] @>

            test <@ Check.String.oneOf [ "draft"; "published" ] "draft" = Ok "draft" @>
            test <@ Check.String.oneOf [ "draft"; "published" ] "archived" = Error [ NotOneOf "draft|published" ] @>
            test <@ Check.String.oneOf [ "draft"; "published" ] nullString = Error [ NotOneOf "draft|published" ] @>

        [<Fact>]
        let ``Check Number exposes executable range checks`` () =
            test <@ Check.Number.between 1 10 5 = Ok 5 @>
            test <@ Check.Number.between 1 10 0 = Error [ OutOfRange(Between("1", "10"), Some "0") ] @>
            test <@ Check.Number.between 1 10 11 = Error [ OutOfRange(Between("1", "10"), Some "11") ] @>

            test <@ Check.Number.greaterThan 3 4 = Ok 4 @>
            test <@ Check.Number.greaterThan 3 3 = Error [ OutOfRange(GreaterThan "3", Some "3") ] @>

            test <@ Check.Number.lessThan 3 2 = Ok 2 @>
            test <@ Check.Number.lessThan 3 3 = Error [ OutOfRange(LessThan "3", Some "3") ] @>

            test <@ Check.Number.atLeast 3 3 = Ok 3 @>
            test <@ Check.Number.atLeast 3 2 = Error [ OutOfRange(AtLeast "3", Some "2") ] @>

            test <@ Check.Number.atMost 3 3 = Ok 3 @>
            test <@ Check.Number.atMost 3 4 = Error [ OutOfRange(AtMost "3", Some "4") ] @>

            test <@ Check.Number.between 1.5m 2.5m 2.0m = Ok 2.0m @>
            test <@ Check.Number.atLeast 1.5m 1.0m = Error [ OutOfRange(AtLeast "1.5", Some "1.0") ] @>
            test <@ Check.Number.positive 0.1m = Ok 0.1m @>
            test <@ Check.Number.nonPositive 0.1m = Error [ OutOfRange(AtMost "0", Some "0.1") ] @>

        [<Fact>]
        let ``Check Seq exposes executable sequence value checks`` () =
            let nullValues: seq<int> = null

            test <@ Check.Seq.notEmpty [ 1 ] = Ok [ 1 ] @>
            test <@ Check.Seq.notEmpty [] = Error [ InvalidCount(MinimumCount 1, Some 0) ] @>
            test <@ Check.Seq.notEmpty nullValues = Error [ InvalidCount(MinimumCount 1, None) ] @>

            test <@ Check.Seq.empty ([]: int list) = Ok([]: int list) @>
            test <@ Check.Seq.empty [ 1 ] = Error [ InvalidCount(ExactCount 0, Some 1) ] @>
            test <@ Check.Seq.empty nullValues = Error [ InvalidCount(ExactCount 0, None) ] @>

            test <@ Check.Seq.count 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.Seq.count 2 [ 1 ] = Error [ InvalidCount(ExactCount 2, Some 1) ] @>
            test <@ Check.Seq.count 2 nullValues = Error [ InvalidCount(ExactCount 2, None) ] @>

            test <@ Check.Seq.minCount 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.Seq.minCount 2 [ 1 ] = Error [ InvalidCount(MinimumCount 2, Some 1) ] @>
            test <@ Check.Seq.minCount 2 nullValues = Error [ InvalidCount(MinimumCount 2, None) ] @>

            test <@ Check.Seq.maxCount 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.Seq.maxCount 2 [ 1; 2; 3 ] = Error [ InvalidCount(MaximumCount 2, Some 3) ] @>
            test <@ Check.Seq.maxCount 2 nullValues = Error [ InvalidCount(MaximumCount 2, None) ] @>

            test <@ Check.Seq.countBetween 2 4 [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>
            test <@ Check.Seq.countBetween 2 4 [ 1 ] = Error [ InvalidCount(CountBetween(2, 4), Some 1) ] @>
            test <@ Check.Seq.countBetween 2 4 [ 1; 2; 3; 4; 5 ] = Error [ InvalidCount(CountBetween(2, 4), Some 5) ] @>
            test <@ Check.Seq.countBetween 2 4 nullValues = Error [ InvalidCount(CountBetween(2, 4), None) ] @>

            test <@ Check.Seq.noDuplicates [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>
            test <@ Check.Seq.noDuplicates [ 1; 2; 1 ] = Error [ Duplicate ] @>
            test <@ Check.Seq.noDuplicates nullValues = Error [ Required ] @>

            test <@ Check.Seq.contains 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.Seq.contains 3 [ 1; 2 ] = Error [ NotOneOf "3" ] @>
            test <@ Check.Seq.contains 3 nullValues = Error [ Required ] @>
            test <@ Check.Seq.single [ 1 ] = Ok [ 1 ] @>
            test <@ Check.Seq.single [ 1; 2 ] = Error [ InvalidCount(ExactCount 1, Some 2) ] @>
            test <@ Check.Seq.atMostOne [ 1; 2 ] = Error [ InvalidCount(MaximumCount 1, Some 2) ] @>
            test <@ Check.Seq.atLeastOne [] = Error [ InvalidCount(MinimumCount 1, Some 0) ] @>
            test <@ Check.Seq.moreThanOne [ 1 ] = Error [ InvalidCount(MinimumCount 2, Some 1) ] @>

        [<Fact>]
        let ``Check exposes top-level concrete structured checks`` () =
            let nullString: string = null
            let nullValues: seq<int> = null

            test <@ Check.length 3 "Ada" = Ok "Ada" @>
            test <@ Check.length 3 "Axial" = Error [ InvalidLength(ExactLength 3, Some 5) ] @>
            test <@ Check.length 3 nullString = Error [ InvalidLength(ExactLength 3, None) ] @>
            test <@ Check.minLength 3 "Ada" = Ok "Ada" @>
            test <@ Check.maxLength 3 "Axial" = Error [ InvalidLength(MaximumLength 3, Some 5) ] @>
            test <@ Check.lengthBetween 2 4 "Ada" = Ok "Ada" @>
            test <@ Check.email "ada@example.com" = Ok "ada@example.com" @>
            test <@ Check.matches "^[a-z]+$" "Ada" = Error [ InvalidFormat "^[a-z]+$" ] @>
            test <@ Check.oneOf [ "draft"; "published" ] "archived" = Error [ NotOneOf "draft|published" ] @>

            test <@ Check.between 1 10 5 = Ok 5 @>
            test <@ Check.greaterThan 3 3 = Error [ OutOfRange(GreaterThan "3", Some "3") ] @>
            test <@ Check.lessThan 3 3 = Error [ OutOfRange(LessThan "3", Some "3") ] @>
            test <@ Check.atLeast 3 2 = Error [ OutOfRange(AtLeast "3", Some "2") ] @>
            test <@ Check.atMost 3 4 = Error [ OutOfRange(AtMost "3", Some "4") ] @>
            test <@ Check.positive 1 = Ok 1 @>
            test <@ Check.positive 0 = Error [ OutOfRange(GreaterThan "0", Some "0") ] @>
            test <@ Check.nonNegative 0 = Ok 0 @>
            test <@ Check.nonNegative -1 = Error [ OutOfRange(AtLeast "0", Some "-1") ] @>
            test <@ Check.negative -1 = Ok -1 @>
            test <@ Check.negative 0 = Error [ OutOfRange(LessThan "0", Some "0") ] @>
            test <@ Check.nonPositive 0 = Ok 0 @>
            test <@ Check.nonPositive 1 = Error [ OutOfRange(AtMost "0", Some "1") ] @>

            test <@ Check.count 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.count 2 [ 1 ] = Error [ InvalidCount(ExactCount 2, Some 1) ] @>
            test <@ Check.count 2 nullValues = Error [ InvalidCount(ExactCount 2, None) ] @>
            test <@ Check.minCount 2 [ 1 ] = Error [ InvalidCount(MinimumCount 2, Some 1) ] @>
            test <@ Check.maxCount 2 [ 1; 2; 3 ] = Error [ InvalidCount(MaximumCount 2, Some 3) ] @>
            test <@ Check.countBetween 2 4 [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>
            test <@ Check.distinct [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>
            test <@ Check.contains 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.contains 3 [ 1; 2 ] = Error [ NotOneOf "3" ] @>
            test <@ Check.contains 3 nullValues = Error [ Required ] @>
            test <@ Check.single [ 1 ] = Ok [ 1 ] @>
            test <@ Check.single [ 1; 2 ] = Error [ InvalidCount(ExactCount 1, Some 2) ] @>
            test <@ Check.atMostOne [ 1; 2 ] = Error [ InvalidCount(MaximumCount 1, Some 2) ] @>
            test <@ Check.atLeastOne [] = Error [ InvalidCount(MinimumCount 1, Some 0) ] @>
            test <@ Check.moreThanOne [ 1 ] = Error [ InvalidCount(MinimumCount 2, Some 1) ] @>

            test <@ Check.equalTo 3 3 = Ok 3 @>
            test <@ Check.equalTo 3 4 = Error [ NotOneOf "3" ] @>
            test <@ Check.notEqualTo 3 4 = Ok 4 @>
            test <@ Check.notEqualTo 3 3 = Error [ Custom "notEqualTo:3" ] @>

        [<Fact>]
        let ``Check top-level string facades match direct module behavior`` () =
            let nullString: string = null

            let assertSame (direct: Check<string>) (facade: Check<string>) samples =
                for sample in samples do
                    Assert.Equal<Result<string, CheckFailure list>>(direct sample, facade sample)

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
                    Assert.Equal<Result<int, CheckFailure list>>(direct sample, facade sample)

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
                    Assert.Equal<Result<seq<int>, CheckFailure list>>(direct sample, facade sample)

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

        // These facade comparisons bind the type-directed Check.present/empty/notEmpty side through an explicitly
        // typed `let` first: calling the inline SRTP facade directly inside a quotation or an overloaded xUnit
        // assertion leaves its free 'result witness unpinned, which the F# compiler then defaults incorrectly.
        // An explicit `let` annotation resolves 'result before the value ever reaches the quotation/assertion.
        [<Fact>]
        let ``Check top-level presence facade delegates to the String module`` () =
            let nullString: string = null

            let present1 : Result<string, CheckFailure list> = Check.present "Ada"
            let present2 : Result<string, CheckFailure list> = Check.present nullString
            let present3 : Result<string, CheckFailure list> = Check.present ""
            let empty1 : Result<string, CheckFailure list> = Check.empty ""
            let empty2 : Result<string, CheckFailure list> = Check.empty " "
            let notEmpty1 : Result<string, CheckFailure list> = Check.notEmpty " "
            let notEmpty2 : Result<string, CheckFailure list> = Check.notEmpty ""

            test <@ Check.String.present "Ada" = present1 @>
            test <@ Check.String.present nullString = present2 @>
            test <@ Check.String.present "" = present3 @>
            test <@ Check.String.empty "" = empty1 @>
            test <@ Check.String.empty " " = empty2 @>
            test <@ Check.String.notEmpty " " = notEmpty1 @>
            test <@ Check.String.notEmpty "" = notEmpty2 @>

        [<Fact>]
        let ``Check top-level presence facade delegates to the Option module`` () =
            let present1 : Result<int option, CheckFailure list> = Check.present (Some 1)
            let present2 : Result<int option, CheckFailure list> = Check.present (None: int option)
            let empty1 : Result<int option, CheckFailure list> = Check.empty (None: int option)
            let empty2 : Result<int option, CheckFailure list> = Check.empty (Some 1)
            let notEmpty1 : Result<int option, CheckFailure list> = Check.notEmpty (Some 1)
            let notEmpty2 : Result<int option, CheckFailure list> = Check.notEmpty (None: int option)

            test <@ Check.Option.present (Some 1) = present1 @>
            test <@ Check.Option.present (None: int option) = present2 @>
            test <@ Check.Option.empty (None: int option) = empty1 @>
            test <@ Check.Option.empty (Some 1) = empty2 @>
            test <@ Check.Option.notEmpty (Some 1) = notEmpty1 @>
            test <@ Check.Option.notEmpty (None: int option) = notEmpty2 @>

        [<Fact>]
        let ``Check top-level presence facade delegates to the ValueOption module`` () =
            let present1 : Result<int voption, CheckFailure list> = Check.present (ValueSome 1)
            let present2 : Result<int voption, CheckFailure list> = Check.present (ValueNone: int voption)
            let empty1 : Result<int voption, CheckFailure list> = Check.empty (ValueNone: int voption)
            let empty2 : Result<int voption, CheckFailure list> = Check.empty (ValueSome 1)
            let notEmpty1 : Result<int voption, CheckFailure list> = Check.notEmpty (ValueSome 1)
            let notEmpty2 : Result<int voption, CheckFailure list> = Check.notEmpty (ValueNone: int voption)

            test <@ Check.ValueOption.present (ValueSome 1) = present1 @>
            test <@ Check.ValueOption.present (ValueNone: int voption) = present2 @>
            test <@ Check.ValueOption.empty (ValueNone: int voption) = empty1 @>
            test <@ Check.ValueOption.empty (ValueSome 1) = empty2 @>
            test <@ Check.ValueOption.notEmpty (ValueSome 1) = notEmpty1 @>
            test <@ Check.ValueOption.notEmpty (ValueNone: int voption) = notEmpty2 @>

        [<Fact>]
        let ``Check top-level presence facade delegates to the Nullable module`` () =
            let present1 : Result<System.Nullable<int>, CheckFailure list> = Check.present (System.Nullable 1)
            let present2 : Result<System.Nullable<int>, CheckFailure list> = Check.present (System.Nullable<int>())
            let empty1 : Result<System.Nullable<int>, CheckFailure list> = Check.empty (System.Nullable<int>())
            let empty2 : Result<System.Nullable<int>, CheckFailure list> = Check.empty (System.Nullable 1)
            let notEmpty1 : Result<System.Nullable<int>, CheckFailure list> = Check.notEmpty (System.Nullable 1)
            let notEmpty2 : Result<System.Nullable<int>, CheckFailure list> = Check.notEmpty (System.Nullable<int>())

            test <@ Check.Nullable.present (System.Nullable 1) = present1 @>
            test <@ Check.Nullable.present (System.Nullable<int>()) = present2 @>
            test <@ Check.Nullable.empty (System.Nullable<int>()) = empty1 @>
            test <@ Check.Nullable.empty (System.Nullable 1) = empty2 @>
            test <@ Check.Nullable.notEmpty (System.Nullable 1) = notEmpty1 @>
            test <@ Check.Nullable.notEmpty (System.Nullable<int>()) = notEmpty2 @>

        [<Fact>]
        let ``Check top-level presence facade delegates to the Seq module`` () =
            let emptyValues: int list = []
            let values: int list = [ 1 ]
            let nullValues: int array = null

            let empty1 : Result<int list, CheckFailure list> = Check.empty emptyValues
            let empty2 : Result<int list, CheckFailure list> = Check.empty values
            let empty3 : Result<int array, CheckFailure list> = Check.empty nullValues
            let notEmpty1 : Result<int list, CheckFailure list> = Check.notEmpty values
            let notEmpty2 : Result<int list, CheckFailure list> = Check.notEmpty emptyValues
            let notEmpty3 : Result<int array, CheckFailure list> = Check.notEmpty nullValues

            test <@ Check.Seq.empty emptyValues = empty1 @>
            test <@ Check.Seq.empty values = empty2 @>
            test <@ Check.Seq.empty nullValues = empty3 @>
            test <@ Check.Seq.notEmpty values = notEmpty1 @>
            test <@ Check.Seq.notEmpty emptyValues = notEmpty2 @>
            test <@ Check.Seq.notEmpty nullValues = notEmpty3 @>

        [<Fact>]
        let ``Check composition accepts tightened top-level checks`` () =
            let requiredName =
                Check.all [ Check.present; Check.lengthBetween 2 40 ]

            test <@ requiredName "Ada" = Ok "Ada" @>
            test <@ requiredName "" = Error [ Required; InvalidLength(LengthBetween(2, 40), Some 0) ] @>

            let nullString: string = null

            test <@ requiredName nullString = Error [ Required; InvalidLength(LengthBetween(2, 40), None) ] @>

            let shortCode =
                Check.any [ Check.length 2; Check.length 3 ]

            test <@ shortCode "US" = Ok "US" @>
            test <@ shortCode "USA" = Ok "USA" @>
            test <@ shortCode "United States" = Error [ InvalidLength(ExactLength 2, Some 13); InvalidLength(ExactLength 3, Some 13) ] @>

            let requiredDistinctIds =
                Check.all [ Check.notEmpty; Check.distinct; Check.maxCount 3 ]

            test <@ requiredDistinctIds [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>
            test <@ requiredDistinctIds [] = Error [ InvalidCount(MinimumCount 1, Some 0) ] @>
            test <@ requiredDistinctIds [ 1; 2; 1; 3 ] = Error [ Duplicate; InvalidCount(MaximumCount 3, Some 4) ] @>

        [<Fact>]
        let ``Check Option exposes executable option value checks`` () =
            test <@ Check.Option.some (Some 1) = Ok(Some 1) @>
            test <@ Check.Option.some None = Error [ Required ] @>

            test <@ Check.Option.none (None: int option) = Ok None @>
            test <@ Check.Option.none (Some 1) = Error [ NotOneOf "None" ] @>

            test <@ Check.Option.present (Some 1) = Ok(Some 1) @>
            test <@ Check.Option.present None = Error [ Required ] @>

            test <@ Check.Option.empty (None: int option) = Ok None @>
            test <@ Check.Option.empty (Some 1) = Error [ NotOneOf "None" ] @>

            test <@ Check.Option.notEmpty (Some 1) = Ok(Some 1) @>
            test <@ Check.Option.notEmpty None = Error [ Required ] @>

        [<Fact>]
        let ``Check ValueOption exposes executable value option checks`` () =
            test <@ Check.ValueOption.some (ValueSome 1) = Ok(ValueSome 1) @>
            test <@ Check.ValueOption.some ValueNone = Error [ Required ] @>

            test <@ Check.ValueOption.none (ValueNone: int voption) = Ok ValueNone @>
            test <@ Check.ValueOption.none (ValueSome 1) = Error [ NotOneOf "ValueNone" ] @>

            test <@ Check.ValueOption.present (ValueSome 1) = Ok(ValueSome 1) @>
            test <@ Check.ValueOption.present ValueNone = Error [ Required ] @>

            test <@ Check.ValueOption.empty (ValueNone: int voption) = Ok ValueNone @>
            test <@ Check.ValueOption.empty (ValueSome 1) = Error [ NotOneOf "ValueNone" ] @>

            test <@ Check.ValueOption.notEmpty (ValueSome 1) = Ok(ValueSome 1) @>
            test <@ Check.ValueOption.notEmpty ValueNone = Error [ Required ] @>

        [<Fact>]
        let ``Check Nullable exposes executable nullable value checks`` () =
            test <@ Check.Nullable.hasValue (System.Nullable 1) = Ok(System.Nullable 1) @>
            test <@ Check.Nullable.hasValue (System.Nullable<int>()) = Error [ Required ] @>

            test <@ Check.Nullable.hasNoValue (System.Nullable<int>()) = Ok(System.Nullable<int>()) @>
            test <@ Check.Nullable.hasNoValue (System.Nullable 1) = Error [ NotOneOf "null" ] @>

            test <@ Check.Nullable.present (System.Nullable 1) = Ok(System.Nullable 1) @>
            test <@ Check.Nullable.present (System.Nullable<int>()) = Error [ Required ] @>

            test <@ Check.Nullable.empty (System.Nullable<int>()) = Ok(System.Nullable<int>()) @>
            test <@ Check.Nullable.empty (System.Nullable 1) = Error [ NotOneOf "null" ] @>

            test <@ Check.Nullable.notEmpty (System.Nullable 1) = Ok(System.Nullable 1) @>
            test <@ Check.Nullable.notEmpty (System.Nullable<int>()) = Error [ Required ] @>

        [<Fact>]
        let ``Check Result exposes executable result value checks`` () =
            test <@ Check.Result.ok (Ok 1) = Ok(Ok 1) @>
            test <@ Check.Result.ok (Error "missing") = Error [ NotOneOf "Ok" ] @>

            test <@ Check.Result.error (Error "missing") = Ok(Error "missing") @>
            test <@ Check.Result.error (Ok 1) = Error [ NotOneOf "Error" ] @>

        [<Fact>]
        let ``Predicate exposes boolean helpers outside structured Check`` () =
            let nullString: string = null
            let nullValues: seq<int> = null

            test <@ (Some 1).IsPresent @>
            test <@ (None: int option).IsAbsent @>
            Assert.True(Predicate.present (Some 1))
            Assert.True(Predicate.empty (None: int option))
            Assert.True(Predicate.notEmpty (Some 1))

            test <@ (ValueSome 1).IsPresent @>
            test <@ (ValueNone: int voption).IsAbsent @>
            Assert.True(Predicate.present (ValueSome 1))
            Assert.True(Predicate.empty (ValueNone: int voption))
            Assert.True(Predicate.notEmpty (ValueSome 1))

            test <@ (System.Nullable 1).IsPresent @>
            test <@ (System.Nullable<int>()).IsAbsent @>
            Assert.True(Predicate.present (System.Nullable 1))
            Assert.True(Predicate.empty (System.Nullable<int>()))
            Assert.True(Predicate.notEmpty (System.Nullable 1))

            test <@ (Ok 1).IsOk @>
            test <@ (Error "missing").IsError @>

            test <@ Predicate.Reference.isNull nullString @>
            test <@ Predicate.Reference.notNull "Ada" @>

            test <@ "".IsEmpty @>
            test <@ not nullString.IsEmpty @>
            test <@ " ".IsNotEmpty @>
            test <@ "   ".IsBlank @>
            test <@ nullString.IsBlank @>
            test <@ "Ada".IsNotBlank @>
            test <@ "Ada".HasMinLength 3 @>
            test <@ "Ada".HasMaxLength 3 @>
            test <@ "Ada".HasLength 3 @>
            test <@ "Ada".HasLengthBetween(2, 4) @>
            test <@ "ada".MatchesPattern "^[a-z]+$" @>
            test <@ "ada@example.com".IsEmail @>
            test <@ "123".IsNumeric @>
            test <@ "Ada123".IsAlphaNumeric @>
            test <@ not ("Ada-123").IsAlphaNumeric @>

            test <@ ([]: int list).HasNoItems @>
            test <@ not nullValues.HasNoItems @>
            test <@ [ 1 ].HasItems @>
            test <@ [ 1; 2 ].HasItem 2 @>
            test <@ [ 1; 2 ].HasCount 2 @>
            test <@ [ 1; 2 ].HasMinCount 2 @>
            test <@ [ 1; 2 ].HasMaxCount 2 @>
            test <@ [ 1; 2 ].HasCountBetween(1, 3) @>
            test <@ [ 1 ].HasSingleItem @>
            test <@ ([]: int list).HasAtMostOneItem @>
            test <@ [ 1 ].HasItems @>
            test <@ [ 1; 2 ].HasMoreThanOneItem @>
            test <@ [ 1; 2; 1 ].HasDuplicates @>
            test <@ [ 1; 2; 3 ].IsDistinct @>
            test <@ not [ 1; 2; 1 ].IsDistinct @>
            test <@ not nullValues.IsDistinct @>

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

            let present1 : Result<string, CheckFailure list> = Check.present "Ada"
            let present2 : Result<string, CheckFailure list> = Check.present nullString
            let empty1 : Result<string, CheckFailure list> = Check.empty ""
            let notEmpty1 : Result<string, CheckFailure list> = Check.notEmpty "  "

            Assert.Equal(Ok "Ada", present1)
            Assert.Equal(Error [ Required ], present2)
            Assert.Equal(Ok "", empty1)
            Assert.Equal(Ok "  ", notEmpty1)
            test <@ Check.length 3 "abc" = Ok "abc" @>
            test <@ Check.email "ada@example.com" = Ok "ada@example.com" @>
            test <@ Check.matches "^[a-z]+$" "abc" = Ok "abc" @>
            test <@ Check.count 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.distinct [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>
            test <@ Check.single [ 5 ] = Ok [ 5 ] @>

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
            test <@ ("Ada" |> Check.String.present) = Ok "Ada" @>
            test <@ ("" |> Check.String.present) = Error [ Required ] @>
            test <@ ("Ada" |> Result.okIf (String.IsNullOrWhiteSpace >> not)) = Ok "Ada" @>
            test <@ ("" |> Result.okIf (String.IsNullOrWhiteSpace >> not)) = Error () @>
            test <@ ("" |> Result.okIf (String.IsNullOrWhiteSpace >> not) |> Result.orError "required") = Error "required" @>
            test <@ (true |> Result.requireTrue "invalid") = Ok () @>
            test <@ (false |> Result.requireTrue "invalid") = Error "invalid" @>
            test <@ (Error "boom" |> Result.orError "typed") = Error "typed" @>
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
            test <@ ("Ada" |> Result.okIf (String.IsNullOrWhiteSpace >> not)) = Ok "Ada" @>
            test <@ ("Ada" |> Result.okIf (fun value -> not (obj.ReferenceEquals(value, null)))) = Ok "Ada" @>
            test <@ ([ 1; 2 ] |> Result.okIf (Seq.isEmpty >> not)) = Ok [ 1; 2 ] @>
            test <@ ([ 1; 2 ] |> Result.okIf (Seq.contains 2)) = Ok [ 1; 2 ] @>
            test <@ ([ 1; 2; 3 ] |> Result.okIf (fun values -> (Check.distinct values).IsOk)) = Ok [ 1; 2; 3 ] @>
            test <@ ("ab" |> Check.String.minLength 3) = Error [ InvalidLength(MinimumLength 3, Some 2) ] @>
            test <@ ("abcd" |> Check.String.maxLength 3) = Error [ InvalidLength(MaximumLength 3, Some 4) ] @>
            test <@ ("ab" |> Check.String.exactLength 3) = Error [ InvalidLength(ExactLength 3, Some 2) ] @>
            test <@ (6 |> Check.Number.between 3 5) = Error [ OutOfRange(Between("3", "5"), Some "6") ] @>
            test <@ (3 |> Check.Number.greaterThan 3) = Error [ OutOfRange(GreaterThan "3", Some "3") ] @>
            test <@ (3 |> Check.Number.lessThan 3) = Error [ OutOfRange(LessThan "3", Some "3") ] @>
            test <@ (2 |> Check.Number.atLeast 3) = Error [ OutOfRange(AtLeast "3", Some "2") ] @>
            test <@ (4 |> Check.Number.atMost 3) = Error [ OutOfRange(AtMost "3", Some "4") ] @>
            test <@ ([] |> Check.Seq.notEmpty) = Error [ InvalidCount(MinimumCount 1, Some 0) ] @>
            test <@ ([ 5 ] |> Check.Seq.minCount 2) = Error [ InvalidCount(MinimumCount 2, Some 1) ] @>
            test <@ Collection.traverseResult (fun value -> if value < 3 then Ok(value * 2) else Error value) [ 1; 2 ] = Ok [ 2; 4 ] @>
            test <@ Collection.sequenceResult [ Ok 1; Error "missing"; Ok 3 ] = Error "missing" @>
            test <@ workflow = Ok 10 @>
