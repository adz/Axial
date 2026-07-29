namespace Axial.Tests

open System
open Microsoft.FSharp.Core
open Axial.Check
open Swensen.Unquote
open Xunit

module CheckTests =
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
                [ Blank
                  Blank
                  InvalidFormat "email"
                  InvalidLength(MinimumLength 3, Some 2)
                  OutOfRange(Between("1", "10"), Some "12")
                  InvalidLength(LengthBetween(1, 3), Some 0)
                  NotOneOf "expected"
                  Custom "domain.rule" ]

            test
                <@
                    failures =
                        [ Blank
                          Blank
                          InvalidFormat "email"
                          InvalidLength(MinimumLength 3, Some 2)
                          OutOfRange(Between("1", "10"), Some "12")
                          InvalidLength(LengthBetween(1, 3), Some 0)
                          NotOneOf "expected"
                          Custom "domain.rule" ]
                @>

        [<Fact>]
        let ``Check composition accumulates alternatives and maps failures`` () =
            let missingWhenEmpty : Check<string> =
                fun value -> if value = "" then Error [ Blank ] else Ok ()

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

            test <@ Check.all [ missingWhenEmpty; blankWhenWhitespace ] "" = Error [ Blank; Blank ] @>
            test <@ Check.all [ missingWhenEmpty; blankWhenWhitespace ] "Ada" = Ok () @>
            test <@ Check.all [] "Ada" = Ok () @>
            test <@ Check.any [ invalidWhenNotEmail; invalidWhenNotPhone ] "ada@example.com" = Ok () @>
            test <@ Check.any [ invalidWhenNotEmail; invalidWhenNotPhone ] "Ada" = Error [ InvalidFormat "email"; InvalidFormat "phone" ] @>
            test <@ Check.any [] "Ada" = Error [] @>
            test <@ Check.not invalidWhenNotEmail "Ada" = Ok () @>
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
                    Ok ()

            let check =
                Check.all
                    [
                        failWith "first" Blank
                        passWith "second"
                        failWith "third" Blank
                        failWith "fourth" (InvalidFormat "email")
                    ]

            test <@ check "" = Error [ Blank; Blank; InvalidFormat "email" ] @>
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
                    Ok ()

            let firstSuccess =
                Check.any
                    [
                        failWith "email" (InvalidFormat "email")
                        failWith "phone" (InvalidFormat "phone")
                        passWith "username"
                        failWith "later" (Custom "unreachable")
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

            test <@ requiredEmail nullString = Error [ Blank; InvalidFormat "email"; InvalidLength(LengthBetween(5, 20), None) ] @>
            test <@ requiredEmail "" = Error [ Blank; InvalidFormat "email"; InvalidLength(LengthBetween(5, 20), Some 0) ] @>
            test <@ requiredEmail "   " = Error [ Blank; InvalidFormat "email"; InvalidLength(LengthBetween(5, 20), Some 3) ] @>
            test <@ requiredEmail "ada" = Error [ InvalidFormat "email"; InvalidLength(LengthBetween(5, 20), Some 3) ] @>
            test <@ requiredEmail "ada@example.com" = Ok () @>

        [<Fact>]
        let ``Check Number behavior keeps inclusive and exclusive range boundaries distinct`` () =
            test <@ Check.Number.between 1 3 1 = Ok () @>
            test <@ Check.Number.between 1 3 3 = Ok () @>
            test <@ Check.Number.between 1 3 0 = Error [ OutOfRange(Between("1", "3"), Some "0") ] @>
            test <@ Check.Number.between 1 3 4 = Error [ OutOfRange(Between("1", "3"), Some "4") ] @>

            test <@ Check.Number.greaterThan 1 1 = Error [ OutOfRange(GreaterThan "1", Some "1") ] @>
            test <@ Check.Number.greaterThan 1 2 = Ok () @>
            test <@ Check.Number.lessThan 3 3 = Error [ OutOfRange(LessThan "3", Some "3") ] @>
            test <@ Check.Number.lessThan 3 2 = Ok () @>
            test <@ Check.Number.atLeast 1 1 = Ok () @>
            test <@ Check.Number.atMost 3 3 = Ok () @>
            test <@ Check.Number.positive 1 = Ok () @>
            test <@ Check.Number.positive 0 = Error [ OutOfRange(GreaterThan "0", Some "0") ] @>
            test <@ Check.Number.nonNegative 0 = Ok () @>
            test <@ Check.Number.nonNegative -1 = Error [ OutOfRange(AtLeast "0", Some "-1") ] @>
            test <@ Check.Number.negative -1 = Ok () @>
            test <@ Check.Number.negative 0 = Error [ OutOfRange(LessThan "0", Some "0") ] @>
            test <@ Check.Number.nonPositive 0 = Ok () @>
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

            test <@ seqCheck [ 1; 2; 3 ] = Ok () @>
            test <@ seqCheck [] = Error [ InvalidLength(MinimumLength 2, Some 0) ] @>
            test <@ seqCheck [ 1; 2; 1; 3 ] = Error [ InvalidLength(MaximumLength 3, Some 4); Duplicate ] @>
            test <@ seqCheck nullValues = Error [ InvalidLength(MinimumLength 2, None); InvalidLength(MaximumLength 3, None); Blank ] @>

        [<Fact>]
        let ``Check Option and Result behavior composes with all and any`` () =
            test <@ Check.all [ Check.Option.some; Check.not Check.Option.none ] (Some 1) = Ok () @>
            test <@ Check.all [ Check.Option.some; Check.not Check.Option.none ] None = Error [ Blank; Custom "check.not" ] @>
            test <@ Check.any [ Check.Option.none; Check.Option.some ] (Some 1) = Ok () @>
            test <@ Check.any [ Check.Option.none; Check.Option.some ] (None: int option) = Ok () @>

            test <@ Check.all [ Check.Result.ok; Check.not Check.Result.error ] (Ok 1) = Ok () @>
            test
                <@
                    Check.all [ Check.Result.ok; Check.not Check.Result.error ] (Error "missing") =
                        Error [ NotOneOf "Ok"; Custom "check.not" ]
                @>
            test <@ Check.any [ Check.Result.error; Check.Result.ok ] (Error "missing") = Ok () @>
            test <@ Check.any [ Check.Result.error; Check.Result.ok ] (Ok 1) = Ok () @>

        [<Fact>]
        let ``Check String exposes executable string value checks`` () =
            let nullString: string = null

            test <@ Check.String.present "Ada" = Ok () @>
            test <@ Check.String.present nullString = Error [ Blank ] @>
            test <@ Check.String.present "" = Error [ Blank ] @>
            test <@ Check.String.present "   " = Error [ Blank ] @>

            test <@ Check.String.empty "" = Ok () @>
            test <@ Check.String.empty " " = Error [ InvalidLength(ExactLength 0, Some 1) ] @>
            test <@ Check.String.empty nullString = Error [ Blank ] @>

            test <@ Check.String.notEmpty " " = Ok () @>
            test <@ Check.String.notEmpty "" = Error [ InvalidLength(MinimumLength 1, Some 0) ] @>
            test <@ Check.String.notEmpty nullString = Error [ Blank ] @>

            test <@ Check.String.minLength 3 "Ada" = Ok () @>
            test <@ Check.String.minLength 3 "Al" = Error [ InvalidLength(MinimumLength 3, Some 2) ] @>
            test <@ Check.String.minLength 3 nullString = Error [ InvalidLength(MinimumLength 3, None) ] @>

            test <@ Check.String.maxLength 3 "Ada" = Ok () @>
            test <@ Check.String.maxLength 3 "Axial" = Error [ InvalidLength(MaximumLength 3, Some 5) ] @>
            test <@ Check.String.maxLength 3 nullString = Error [ InvalidLength(MaximumLength 3, None) ] @>

            test <@ Check.String.lengthBetween 2 4 "Ada" = Ok () @>
            test <@ Check.String.lengthBetween 2 4 "A" = Error [ InvalidLength(LengthBetween(2, 4), Some 1) ] @>
            test <@ Check.String.lengthBetween 2 4 "Axial" = Error [ InvalidLength(LengthBetween(2, 4), Some 5) ] @>
            test <@ Check.String.lengthBetween 2 4 nullString = Error [ InvalidLength(LengthBetween(2, 4), None) ] @>

            test <@ Check.String.length 3 "Ada" = Ok () @>
            test <@ Check.String.length 3 "Axial" = Error [ InvalidLength(ExactLength 3, Some 5) ] @>
            test <@ Check.String.length 3 nullString = Error [ InvalidLength(ExactLength 3, None) ] @>
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
            test <@ Check.String.oneOf [ "draft"; "published" ] "archived" = Error [ NotOneOf "draft|published" ] @>
            test <@ Check.String.oneOf [ "draft"; "published" ] nullString = Error [ NotOneOf "draft|published" ] @>

        [<Fact>]
        let ``Check Number exposes executable range checks`` () =
            test <@ Check.Number.between 1 10 5 = Ok () @>
            test <@ Check.Number.between 1 10 0 = Error [ OutOfRange(Between("1", "10"), Some "0") ] @>
            test <@ Check.Number.between 1 10 11 = Error [ OutOfRange(Between("1", "10"), Some "11") ] @>

            test <@ Check.Number.greaterThan 3 4 = Ok () @>
            test <@ Check.Number.greaterThan 3 3 = Error [ OutOfRange(GreaterThan "3", Some "3") ] @>

            test <@ Check.Number.lessThan 3 2 = Ok () @>
            test <@ Check.Number.lessThan 3 3 = Error [ OutOfRange(LessThan "3", Some "3") ] @>

            test <@ Check.Number.atLeast 3 3 = Ok () @>
            test <@ Check.Number.atLeast 3 2 = Error [ OutOfRange(AtLeast "3", Some "2") ] @>

            test <@ Check.Number.atMost 3 3 = Ok () @>
            test <@ Check.Number.atMost 3 4 = Error [ OutOfRange(AtMost "3", Some "4") ] @>

            test <@ Check.Number.between 1.5m 2.5m 2.0m = Ok () @>
            test <@ Check.Number.atLeast 1.5m 1.0m = Error [ OutOfRange(AtLeast "1.5", Some "1.0") ] @>
            test <@ Check.Number.positive 0.1m = Ok () @>
            test <@ Check.Number.nonPositive 0.1m = Error [ OutOfRange(AtMost "0", Some "0.1") ] @>

        [<Fact>]
        let ``Check Seq exposes executable sequence value checks`` () =
            let nullValues: seq<int> = null

            test <@ Check.Seq.notEmpty [ 1 ] = Ok () @>
            test <@ Check.Seq.notEmpty [] = Error [ InvalidLength(MinimumLength 1, Some 0) ] @>
            test <@ Check.Seq.notEmpty nullValues = Error [ InvalidLength(MinimumLength 1, None) ] @>

            test <@ Check.Seq.empty ([]: int list) = Ok () @>
            test <@ Check.Seq.empty [ 1 ] = Error [ InvalidLength(ExactLength 0, Some 1) ] @>
            test <@ Check.Seq.empty nullValues = Error [ InvalidLength(ExactLength 0, None) ] @>

            test <@ Check.Seq.count 2 [ 1; 2 ] = Ok () @>
            test <@ Check.Seq.count 2 [ 1 ] = Error [ InvalidLength(ExactLength 2, Some 1) ] @>
            test <@ Check.Seq.count 2 nullValues = Error [ InvalidLength(ExactLength 2, None) ] @>

            test <@ Check.Seq.minCount 2 [ 1; 2 ] = Ok () @>
            test <@ Check.Seq.minCount 2 [ 1 ] = Error [ InvalidLength(MinimumLength 2, Some 1) ] @>
            test <@ Check.Seq.minCount 2 nullValues = Error [ InvalidLength(MinimumLength 2, None) ] @>

            test <@ Check.Seq.maxCount 2 [ 1; 2 ] = Ok () @>
            test <@ Check.Seq.maxCount 2 [ 1; 2; 3 ] = Error [ InvalidLength(MaximumLength 2, Some 3) ] @>
            test <@ Check.Seq.maxCount 2 nullValues = Error [ InvalidLength(MaximumLength 2, None) ] @>

            test <@ Check.Seq.countBetween 2 4 [ 1; 2; 3 ] = Ok () @>
            test <@ Check.Seq.countBetween 2 4 [ 1 ] = Error [ InvalidLength(LengthBetween(2, 4), Some 1) ] @>
            test <@ Check.Seq.countBetween 2 4 [ 1; 2; 3; 4; 5 ] = Error [ InvalidLength(LengthBetween(2, 4), Some 5) ] @>
            test <@ Check.Seq.countBetween 2 4 nullValues = Error [ InvalidLength(LengthBetween(2, 4), None) ] @>

            test <@ Check.Seq.noDuplicates [ 1; 2; 3 ] = Ok () @>
            test <@ Check.Seq.noDuplicates [ 1; 2; 1 ] = Error [ Duplicate ] @>
            test <@ Check.Seq.noDuplicates nullValues = Error [ Blank ] @>

            test <@ Check.Seq.contains 2 [ 1; 2 ] = Ok () @>
            test <@ Check.Seq.contains 3 [ 1; 2 ] = Error [ NotOneOf "3" ] @>
            test <@ Check.Seq.contains 3 nullValues = Error [ Blank ] @>
            test <@ Check.Seq.single [ 1 ] = Ok () @>
            test <@ Check.Seq.single [ 1; 2 ] = Error [ InvalidLength(ExactLength 1, Some 2) ] @>
            test <@ Check.Seq.atMostOne [ 1; 2 ] = Error [ InvalidLength(MaximumLength 1, Some 2) ] @>
            test <@ Check.Seq.atLeastOne [] = Error [ InvalidLength(MinimumLength 1, Some 0) ] @>
            test <@ Check.Seq.moreThanOne [ 1 ] = Error [ InvalidLength(MinimumLength 2, Some 1) ] @>

        [<Fact>]
        let ``Check exposes top-level concrete structured checks`` () =
            let nullString: string = null
            let nullValues: int array = null
            let stringLength3: Check<string> = Check.length 3
            let stringMinLength3: Check<string> = Check.minLength 3
            let stringMaxLength3: Check<string> = Check.maxLength 3
            let stringLengthBetween2And4: Check<string> = Check.lengthBetween 2 4
            let listLength2: Check<int list> = Check.length 2
            let listMinLength2: Check<int list> = Check.minLength 2
            let listMaxLength2: Check<int list> = Check.maxLength 2
            let listLengthBetween2And4: Check<int list> = Check.lengthBetween 2 4
            let arrayLength2: Check<int array> = Check.length 2

            test <@ stringLength3 "Ada" = Ok () @>
            test <@ stringLength3 "Axial" = Error [ InvalidLength(ExactLength 3, Some 5) ] @>
            test <@ stringLength3 nullString = Error [ InvalidLength(ExactLength 3, None) ] @>
            test <@ stringMinLength3 "Ada" = Ok () @>
            test <@ stringMaxLength3 "Axial" = Error [ InvalidLength(MaximumLength 3, Some 5) ] @>
            test <@ stringLengthBetween2And4 "Ada" = Ok () @>
            test <@ Check.email "ada@example.com" = Ok () @>
            test <@ Check.matches "^[a-z]+$" "Ada" = Error [ InvalidFormat "^[a-z]+$" ] @>
            test <@ Check.oneOf [ "draft"; "published" ] "archived" = Error [ NotOneOf "draft|published" ] @>

            test <@ Check.between 1 10 5 = Ok () @>
            test <@ Check.greaterThan 3 3 = Error [ OutOfRange(GreaterThan "3", Some "3") ] @>
            test <@ Check.lessThan 3 3 = Error [ OutOfRange(LessThan "3", Some "3") ] @>
            test <@ Check.atLeast 3 2 = Error [ OutOfRange(AtLeast "3", Some "2") ] @>
            test <@ Check.atMost 3 4 = Error [ OutOfRange(AtMost "3", Some "4") ] @>
            test <@ Check.positive 1 = Ok () @>
            test <@ Check.positive 0 = Error [ OutOfRange(GreaterThan "0", Some "0") ] @>
            test <@ Check.nonNegative 0 = Ok () @>
            test <@ Check.nonNegative -1 = Error [ OutOfRange(AtLeast "0", Some "-1") ] @>
            test <@ Check.negative -1 = Ok () @>
            test <@ Check.negative 0 = Error [ OutOfRange(LessThan "0", Some "0") ] @>
            test <@ Check.nonPositive 0 = Ok () @>
            test <@ Check.nonPositive 1 = Error [ OutOfRange(AtMost "0", Some "1") ] @>

            test <@ listLength2 [ 1; 2 ] = Ok () @>
            test <@ listLength2 [ 1 ] = Error [ InvalidLength(ExactLength 2, Some 1) ] @>
            test <@ arrayLength2 nullValues = Error [ InvalidLength(ExactLength 2, None) ] @>
            test <@ listMinLength2 [ 1 ] = Error [ InvalidLength(MinimumLength 2, Some 1) ] @>
            test <@ listMaxLength2 [ 1; 2; 3 ] = Error [ InvalidLength(MaximumLength 2, Some 3) ] @>
            test <@ listLengthBetween2And4 [ 1; 2; 3 ] = Ok () @>
            test <@ Check.distinct [ 1; 2; 3 ] = Ok () @>
            test <@ Check.contains 2 [ 1; 2 ] = Ok () @>
            test <@ Check.contains 3 [ 1; 2 ] = Error [ NotOneOf "3" ] @>
            test <@ Check.contains 3 nullValues = Error [ Blank ] @>
            test <@ Check.single [ 1 ] = Ok () @>
            test <@ Check.single [ 1; 2 ] = Error [ InvalidLength(ExactLength 1, Some 2) ] @>
            test <@ Check.atMostOne [ 1; 2 ] = Error [ InvalidLength(MaximumLength 1, Some 2) ] @>
            test <@ Check.atLeastOne [] = Error [ InvalidLength(MinimumLength 1, Some 0) ] @>
            test <@ Check.moreThanOne [ 1 ] = Error [ InvalidLength(MinimumLength 2, Some 1) ] @>

            test <@ Check.equalTo 3 3 = Ok () @>
            test <@ Check.equalTo 3 4 = Error [ NotOneOf "3" ] @>
            test <@ Check.notEqualTo 3 4 = Ok () @>
            test <@ Check.notEqualTo 3 3 = Error [ Custom "notEqualTo:3" ] @>

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
        let ``Check top-level sequence operations match direct module behavior`` () =
            let nullValues: seq<int> = null

            let assertSame (direct: Check<seq<int>>) (facade: Check<seq<int>>) samples =
                for sample in samples do
                    Assert.Equal<Result<unit, CheckFailure list>>(direct sample, facade sample)

            assertSame Check.Seq.noDuplicates Check.distinct [ seq [ 1; 2; 3 ]; seq [ 1; 2; 1 ]; nullValues ]
            assertSame (Check.Seq.contains 2) (Check.contains 2) [ seq [ 1; 2 ]; seq [ 1; 3 ]; nullValues ]
            assertSame Check.Seq.single Check.single [ seq [ 1 ]; seq []; seq [ 1; 2 ]; nullValues ]
            assertSame Check.Seq.atMostOne Check.atMostOne [ seq []; seq [ 1 ]; seq [ 1; 2 ]; nullValues ]
            assertSame Check.Seq.atLeastOne Check.atLeastOne [ seq []; seq [ 1 ]; nullValues ]
            assertSame Check.Seq.moreThanOne Check.moreThanOne [ seq [ 1 ]; seq [ 1; 2 ]; nullValues ]

        // Quotations and overloaded assertions can obscure the concrete result type, so bind each facade result
        // before comparing it with the direct type-specific implementation.
        [<Fact>]
        let ``Check top-level presence facade delegates to the String module`` () =
            let nullString: string = null

            let present1 : Result<unit, CheckFailure list> = Check.present "Ada"
            let present2 : Result<unit, CheckFailure list> = Check.present nullString
            let present3 : Result<unit, CheckFailure list> = Check.present ""
            let empty1 : Result<unit, CheckFailure list> = Check.empty ""
            let empty2 : Result<unit, CheckFailure list> = Check.empty " "
            let notEmpty1 : Result<unit, CheckFailure list> = Check.notEmpty " "
            let notEmpty2 : Result<unit, CheckFailure list> = Check.notEmpty ""

            test <@ Check.String.present "Ada" = present1 @>
            test <@ Check.String.present nullString = present2 @>
            test <@ Check.String.present "" = present3 @>
            test <@ Check.String.empty "" = empty1 @>
            test <@ Check.String.empty " " = empty2 @>
            test <@ Check.String.notEmpty " " = notEmpty1 @>
            test <@ Check.String.notEmpty "" = notEmpty2 @>

        [<Fact>]
        let ``Check top-level presence facade delegates to the Option module`` () =
            let present1 : Result<unit, CheckFailure list> = Check.present (Some 1)
            let present2 : Result<unit, CheckFailure list> = Check.present (None: int option)
            let empty1 : Result<unit, CheckFailure list> = Check.empty (None: int option)
            let empty2 : Result<unit, CheckFailure list> = Check.empty (Some 1)
            let notEmpty1 : Result<unit, CheckFailure list> = Check.notEmpty (Some 1)
            let notEmpty2 : Result<unit, CheckFailure list> = Check.notEmpty (None: int option)

            test <@ Check.Option.present (Some 1) = present1 @>
            test <@ Check.Option.present (None: int option) = present2 @>
            test <@ Check.Option.empty (None: int option) = empty1 @>
            test <@ Check.Option.empty (Some 1) = empty2 @>
            test <@ Check.Option.notEmpty (Some 1) = notEmpty1 @>
            test <@ Check.Option.notEmpty (None: int option) = notEmpty2 @>

        [<Fact>]
        let ``Check top-level presence facade delegates to the ValueOption module`` () =
            let present1 : Result<unit, CheckFailure list> = Check.present (ValueSome 1)
            let present2 : Result<unit, CheckFailure list> = Check.present (ValueNone: int voption)
            let empty1 : Result<unit, CheckFailure list> = Check.empty (ValueNone: int voption)
            let empty2 : Result<unit, CheckFailure list> = Check.empty (ValueSome 1)
            let notEmpty1 : Result<unit, CheckFailure list> = Check.notEmpty (ValueSome 1)
            let notEmpty2 : Result<unit, CheckFailure list> = Check.notEmpty (ValueNone: int voption)

            test <@ Check.ValueOption.present (ValueSome 1) = present1 @>
            test <@ Check.ValueOption.present (ValueNone: int voption) = present2 @>
            test <@ Check.ValueOption.empty (ValueNone: int voption) = empty1 @>
            test <@ Check.ValueOption.empty (ValueSome 1) = empty2 @>
            test <@ Check.ValueOption.notEmpty (ValueSome 1) = notEmpty1 @>
            test <@ Check.ValueOption.notEmpty (ValueNone: int voption) = notEmpty2 @>

        [<Fact>]
        let ``Check top-level presence facade delegates to the Nullable module`` () =
            let present1 : Result<unit, CheckFailure list> = Check.present (System.Nullable 1)
            let present2 : Result<unit, CheckFailure list> = Check.present (System.Nullable<int>())
            let empty1 : Result<unit, CheckFailure list> = Check.empty (System.Nullable<int>())
            let empty2 : Result<unit, CheckFailure list> = Check.empty (System.Nullable 1)
            let notEmpty1 : Result<unit, CheckFailure list> = Check.notEmpty (System.Nullable 1)
            let notEmpty2 : Result<unit, CheckFailure list> = Check.notEmpty (System.Nullable<int>())

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

            let empty1 : Result<unit, CheckFailure list> = Check.empty emptyValues
            let empty2 : Result<unit, CheckFailure list> = Check.empty values
            let empty3 : Result<unit, CheckFailure list> = Check.empty nullValues
            let notEmpty1 : Result<unit, CheckFailure list> = Check.notEmpty values
            let notEmpty2 : Result<unit, CheckFailure list> = Check.notEmpty emptyValues
            let notEmpty3 : Result<unit, CheckFailure list> = Check.notEmpty nullValues

            test <@ Check.Seq.empty emptyValues = empty1 @>
            test <@ Check.Seq.empty values = empty2 @>
            test <@ Check.Seq.empty nullValues = empty3 @>
            test <@ Check.Seq.notEmpty values = notEmpty1 @>
            test <@ Check.Seq.notEmpty emptyValues = notEmpty2 @>
            test <@ Check.Seq.notEmpty nullValues = notEmpty3 @>

        [<Fact>]
        let ``Check composition accepts tightened top-level checks`` () =
            // F# visits check lists from left to right. Because the first check is an SRTP facade, the program type
            // must be known here rather than inferred later from a call to requiredName.
            let requiredName : Check<string> =
                Check.all [ Check.present; Check.lengthBetween 2 40 ]

            test <@ requiredName "Ada" = Ok () @>
            test <@ requiredName "" = Error [ Blank; InvalidLength(LengthBetween(2, 40), Some 0) ] @>

            let nullString: string = null

            test <@ requiredName nullString = Error [ Blank; InvalidLength(LengthBetween(2, 40), None) ] @>

            let shortCode =
                Check.any [ Check.length 2; Check.length 3 ]

            test <@ shortCode "US" = Ok () @>
            test <@ shortCode "USA" = Ok () @>
            test <@ shortCode "United States" = Error [ InvalidLength(ExactLength 2, Some 13); InvalidLength(ExactLength 3, Some 13) ] @>

            let requiredDistinctIds : Check<int list> =
                Check.all [ Check.notEmpty; Check.distinct; Check.maxLength 3 ]

            test <@ requiredDistinctIds [ 1; 2; 3 ] = Ok () @>
            test <@ requiredDistinctIds [] = Error [ InvalidLength(MinimumLength 1, Some 0) ] @>
            test <@ requiredDistinctIds [ 1; 2; 1; 3 ] = Error [ Duplicate; InvalidLength(MaximumLength 3, Some 4) ] @>

        [<Fact>]
        let ``Check all resolves every type-directed facade when the program type is declared`` () =
            let stringPresent : Check<string> = Check.all [ Check.present ]
            let stringEmpty : Check<string> = Check.all [ Check.empty ]
            let stringNotEmpty : Check<string> = Check.all [ Check.notEmpty ]
            let optionPresent : Check<int option> = Check.all [ Check.present ]
            let optionEmpty : Check<int option> = Check.all [ Check.empty ]
            let optionNotEmpty : Check<int option> = Check.all [ Check.notEmpty ]
            let valueOptionPresent : Check<int voption> = Check.all [ Check.present ]
            let valueOptionEmpty : Check<int voption> = Check.all [ Check.empty ]
            let valueOptionNotEmpty : Check<int voption> = Check.all [ Check.notEmpty ]
            let nullablePresent : Check<System.Nullable<int>> = Check.all [ Check.present ]
            let nullableEmpty : Check<System.Nullable<int>> = Check.all [ Check.empty ]
            let nullableNotEmpty : Check<System.Nullable<int>> = Check.all [ Check.notEmpty ]
            let listPresent : Check<int list> = Check.all [ Check.present ]
            let listEmpty : Check<int list> = Check.all [ Check.empty ]
            let listNotEmpty : Check<int list> = Check.all [ Check.notEmpty ]
            let arrayPresent : Check<int array> = Check.all [ Check.present ]
            let arrayEmpty : Check<int array> = Check.all [ Check.empty ]
            let arrayNotEmpty : Check<int array> = Check.all [ Check.notEmpty ]
            let anyStringPresence : Check<string> = Check.any [ Check.present; Check.empty ]

            test <@ stringPresent "Ada" = Ok () @>
            test <@ stringEmpty "" = Ok () @>
            test <@ stringNotEmpty "Ada" = Ok () @>
            test <@ optionPresent (Some 1) = Ok () @>
            test <@ optionEmpty None = Ok () @>
            test <@ optionNotEmpty (Some 1) = Ok () @>
            test <@ valueOptionPresent (ValueSome 1) = Ok () @>
            test <@ valueOptionEmpty ValueNone = Ok () @>
            test <@ valueOptionNotEmpty (ValueSome 1) = Ok () @>
            test <@ nullablePresent (System.Nullable 1) = Ok () @>
            test <@ nullableEmpty (System.Nullable<int>()) = Ok () @>
            test <@ nullableNotEmpty (System.Nullable 1) = Ok () @>
            test <@ listPresent [ 1 ] = Ok () @>
            test <@ listEmpty [] = Ok () @>
            test <@ listNotEmpty [ 1 ] = Ok () @>
            test <@ arrayPresent [| 1 |] = Ok () @>
            test <@ arrayEmpty [||] = Ok () @>
            test <@ arrayNotEmpty [| 1 |] = Ok () @>
            test <@ anyStringPresence "Ada" = Ok () @>

        [<Fact>]
        let ``Check Option exposes executable option value checks`` () =
            test <@ Check.Option.some (Some 1) = Ok () @>
            test <@ Check.Option.some None = Error [ Blank ] @>

            test <@ Check.Option.none (None: int option) = Ok () @>
            test <@ Check.Option.none (Some 1) = Error [ NotOneOf "None" ] @>

            test <@ Check.Option.present (Some 1) = Ok () @>
            test <@ Check.Option.present None = Error [ Blank ] @>

            test <@ Check.Option.empty (None: int option) = Ok () @>
            test <@ Check.Option.empty (Some 1) = Error [ NotOneOf "None" ] @>

            test <@ Check.Option.notEmpty (Some 1) = Ok () @>
            test <@ Check.Option.notEmpty None = Error [ Blank ] @>

        [<Fact>]
        let ``Check ValueOption exposes executable value option checks`` () =
            test <@ Check.ValueOption.some (ValueSome 1) = Ok () @>
            test <@ Check.ValueOption.some ValueNone = Error [ Blank ] @>

            test <@ Check.ValueOption.none (ValueNone: int voption) = Ok () @>
            test <@ Check.ValueOption.none (ValueSome 1) = Error [ NotOneOf "ValueNone" ] @>

            test <@ Check.ValueOption.present (ValueSome 1) = Ok () @>
            test <@ Check.ValueOption.present ValueNone = Error [ Blank ] @>

            test <@ Check.ValueOption.empty (ValueNone: int voption) = Ok () @>
            test <@ Check.ValueOption.empty (ValueSome 1) = Error [ NotOneOf "ValueNone" ] @>

            test <@ Check.ValueOption.notEmpty (ValueSome 1) = Ok () @>
            test <@ Check.ValueOption.notEmpty ValueNone = Error [ Blank ] @>

        [<Fact>]
        let ``Check Nullable exposes executable nullable value checks`` () =
            test <@ Check.Nullable.hasValue (System.Nullable 1) = Ok () @>
            test <@ Check.Nullable.hasValue (System.Nullable<int>()) = Error [ Blank ] @>

            test <@ Check.Nullable.hasNoValue (System.Nullable<int>()) = Ok () @>
            test <@ Check.Nullable.hasNoValue (System.Nullable 1) = Error [ NotOneOf "null" ] @>

            test <@ Check.Nullable.present (System.Nullable 1) = Ok () @>
            test <@ Check.Nullable.present (System.Nullable<int>()) = Error [ Blank ] @>

            test <@ Check.Nullable.empty (System.Nullable<int>()) = Ok () @>
            test <@ Check.Nullable.empty (System.Nullable 1) = Error [ NotOneOf "null" ] @>

            test <@ Check.Nullable.notEmpty (System.Nullable 1) = Ok () @>
            test <@ Check.Nullable.notEmpty (System.Nullable<int>()) = Error [ Blank ] @>

        [<Fact>]
        let ``Check Result exposes executable result value checks`` () =
            test <@ Check.Result.ok (Ok 1) = Ok () @>
            test <@ Check.Result.ok (Error "missing") = Error [ NotOneOf "Ok" ] @>

            test <@ Check.Result.error (Error "missing") = Ok () @>
            test <@ Check.Result.error (Ok 1) = Error [ NotOneOf "Error" ] @>

        [<Fact>]
        let ``Predicate exposes boolean helpers outside structured Check`` () =
            let nullString: string = null
            let nullValues: seq<int> = null

            Assert.True(Predicate.present "Ada")
            Assert.True(Predicate.empty "")
            Assert.True(Predicate.notEmpty "Ada")

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

            Assert.True(Predicate.empty ([]: int list))
            Assert.True(Predicate.notEmpty [ 1 ])
            Assert.True(Predicate.empty ([||]: int array))
            Assert.True(Predicate.notEmpty [| 1 |])

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
            let stringLength3: Check<string> = Check.length 3
            let listLength2: Check<int list> = Check.length 2

            let present1 : Result<unit, CheckFailure list> = Check.present "Ada"
            let present2 : Result<unit, CheckFailure list> = Check.present nullString
            let empty1 : Result<unit, CheckFailure list> = Check.empty ""
            let notEmpty1 : Result<unit, CheckFailure list> = Check.notEmpty "  "

            Assert.Equal(Ok (), present1)
            Assert.Equal(Error [ Blank ], present2)
            Assert.Equal(Ok (), empty1)
            Assert.Equal(Ok (), notEmpty1)
            test <@ stringLength3 "abc" = Ok () @>
            test <@ Check.email "ada@example.com" = Ok () @>
            test <@ Check.matches "^[a-z]+$" "abc" = Ok () @>
            test <@ listLength2 [ 1; 2 ] = Ok () @>
            test <@ Check.distinct [ 1; 2; 3 ] = Ok () @>
            test <@ Check.single [ 5 ] = Ok () @>

        [<Fact>]
        let ``Check DSL guards values and maps check failures to application errors`` () =
            let requiredName value =
                value |> CheckDSL.guard CheckDSL.present |> CheckDSL.orError "name-required"

            let invalidLength value =
                value |> CheckDSL.guard (CheckDSL.minLength 3) |> CheckDSL.mapError List.length

            test <@ requiredName "Ada" = Ok "Ada" @>
            test <@ requiredName "" = Error "name-required" @>
            test <@ invalidLength "Ad" = Error 1 @>

        [<Fact>]
        let ``portable constraints keep executable behavior and structural metadata together`` () =
            let constraint' = Constraint.lengthBetween 2 4

            test <@ Constraint.check constraint' "abc" = Ok () @>
            test <@ Constraint.check constraint' "a" = Error [ InvalidLength(LengthBetween(2, 4), Some 1) ] @>
            test
                <@
                    Constraint.details constraint' =
                        { Code = "lengthBetween"
                          Arguments = Map [ "maximum", box 4; "minimum", box 2 ] }
                @>
            test
                <@
                    Constraint.tryPortableArguments constraint' =
                        Some(Map [ "maximum", ConstraintArgument.Integer 4L; "minimum", ConstraintArgument.Integer 2L ])
                @>

            test
                <@
                    Constraint.oneOf [ "red"; "blue" ]
                    |> Constraint.tryPortableArguments =
                        Some(
                            Map
                                [ "choices",
                                  ConstraintArgument.List
                                      [ ConstraintArgument.Text "red"
                                        ConstraintArgument.Text "blue" ] ]
                        )
                @>

        [<Fact>]
        let ``runtime operands remain executable when they have no portable projection`` () =
            let constraint' = Constraint.atLeast 'm'

            test <@ Constraint.check constraint' 'z' = Ok () @>
            test <@ Constraint.arguments constraint' = Map [ "minimum", box 'm' ] @>
            test <@ Constraint.tryPortableArguments constraint' = None @>

        [<Fact>]
        let ``custom constraints reject reserved codes and duplicate arguments`` () =
            raises<ArgumentException> <@ Constraint.define "email" [] Check.String.email |> ignore @>
            raises<ArgumentException>
                <@
                    Constraint.define
                        "custom"
                        [ "value", box "a"
                          "value", box "b" ]
                        (fun (_: string) -> Ok ())
                    |> ignore
                @>
