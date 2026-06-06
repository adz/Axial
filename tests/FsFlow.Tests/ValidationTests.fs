namespace FsFlow.Tests

open Microsoft.FSharp.Core
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module ValidationTests =
        [<Fact>]
        let ``Check covers the pure result surface`` () =
            test <@ Check.negate (Check.isTrue true) = Error () @>
            test <@ Check.negate (Check.isTrue false) = Ok () @>

            test <@ Check.both (Check.isTrue true) (Check.isSome (Some 10)) = Ok () @>
            test <@ Check.both (Check.isTrue true) (Check.isTrue false) = Error () @>

            test <@ Check.either (Check.isTrue false) (Check.isSome (Some 10)) = Ok () @>
            test <@ Check.either (Check.isTrue false) (Check.isTrue false) = Error () @>

            test <@ Check.all [ Check.isTrue true; Check.isTrue true; Check.isTrue true ] = Ok () @>

            let allShortCircuits =
                seq {
                    yield Check.isTrue true
                    yield Check.isTrue false
                    failwith "Check.all should short-circuit before the third item"
                }

            test <@ Check.all allShortCircuits = Error () @>

            test <@ Check.any [ Check.isTrue false; Check.isTrue false; Check.isTrue true ] = Ok () @>

            let anyShortCircuits =
                seq {
                    yield Check.isTrue true
                    failwith "Check.any should short-circuit before the second item"
                }

            test <@ Check.any anyShortCircuits = Ok () @>

            test <@ Check.isTrue true = Ok () @>
            test <@ Check.isTrue false = Error () @>
            test <@ Check.whenTrue true = Ok true @>
            test <@ Check.whenTrue false = Error () @>
            test <@ Check.isFalse true = Error () @>
            test <@ Check.isFalse false = Ok () @>
            test <@ Check.whenFalse false = Ok false @>
            test <@ Check.whenFalse true = Error () @>
            test <@ Check.fromPredicate (fun value -> value > 3) 4 = Ok 4 @>
            test <@ Check.fromPredicate (fun value -> value > 3) 2 = Error () @>
            test <@ Check.fromTry (true, 42) = Ok 42 @>
            test <@ Check.fromTry (false, 42) = Error () @>
            test <@ Check.fromChoice (Choice1Of2 42) = Ok 42 @>
            test <@ Check.fromChoice (Choice2Of2 "missing") = Error "missing" @>

            test <@ Check.isSome (Some 10) = Ok () @>
            test <@ Check.isSome None = Error () @>
            test <@ Check.isNone None = Ok () @>
            test <@ Check.isNone (Some 7) = Error () @>
            test <@ Check.whenSome (Some 7) = Ok (Some 7) @>
            test <@ Check.whenNone None = Ok None @>
            test <@ Check.takeSome (Some 7) = Ok 7 @>
            test <@ Check.takeSome None = Error () @>

            test <@ Check.isValueSome (ValueSome 11) = Ok () @>
            test <@ Check.isValueNone ValueNone = Ok () @>
            test <@ Check.isValueNone (ValueSome 8) = Error () @>
            test <@ Check.whenValueSome (ValueSome 8) = Ok (ValueSome 8) @>
            test <@ Check.whenValueNone ValueNone = Ok ValueNone @>
            test <@ Check.takeValueSome (ValueSome 8) = Ok 8 @>
            test <@ Check.takeValueSome ValueNone = Error () @>

            test <@ Check.hasValue (System.Nullable 12) = Ok () @>
            test <@ Check.hasValue (System.Nullable<int>()) = Error () @>
            test <@ Check.hasNoValue (System.Nullable<int>()) = Ok () @>
            test <@ Check.hasNoValue (System.Nullable 12) = Error () @>
            test <@ Check.whenHasValue (System.Nullable 12) = Ok (System.Nullable 12) @>
            test <@ Check.whenHasNoValue (System.Nullable<int>()) = Ok (System.Nullable<int>()) @>
            test <@ Check.takeHasValue (System.Nullable 12) = Ok 12 @>
            test <@ Check.takeHasValue (System.Nullable<int>()) = Error () @>

            let nonNull = "flowkit"
            let nullString: string = null

            test <@ Check.notNull nonNull = Ok () @>
            test <@ Check.notNull nullString = Error () @>
            test <@ Check.isNull nullString = Ok () @>
            test <@ Check.isNull nonNull = Error () @>
            test <@ Check.whenNotNull nonNull = Ok "flowkit" @>
            test <@ Check.whenNull nullString = Ok nullString @>

            test <@ Check.isOk (Ok 3) = Ok () @>
            test <@ Check.isOk (Error "missing") = Error () @>
            test <@ Check.whenOk (Ok 3) = Ok (Ok 3) @>
            test <@ Check.takeOk (Ok 3) = Ok 3 @>
            test <@ Check.takeOk (Error "missing") = Error () @>
            test <@ Check.isError (Error "missing") = Ok () @>
            test <@ Check.isError (Ok 3) = Error () @>
            test <@ Check.whenError (Error "missing") = Ok (Error "missing") @>
            test <@ Check.takeError (Error "missing") = Ok "missing" @>
            test <@ Check.takeError (Ok 3) = Error () @>

            test <@ Check.notEmpty [ 1; 2 ] = Ok () @>
            test <@ Check.whenNotEmpty [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.takeHead [ 1; 2 ] = Ok 1 @>
            test <@ Check.takeHead [] = Error () @>
            test <@ Check.empty Seq.empty = Ok () @>
            test <@ Check.empty [ 1 ] = Error () @>
            test <@ Check.whenEmpty [] = Ok [] @>
            test <@ Check.isSingle [ 5 ] = Ok () @>
            test <@ Check.isSingle [] = Error(ExpectedSingle 0) @>
            test <@ Check.isSingle [ 1; 2 ] = Error(ExpectedSingle 2) @>
            test <@ Check.whenSingle [ 5 ] = Ok [ 5 ] @>
            test <@ Check.takeSingle [ 5 ] = Ok 5 @>
            test <@ Check.takeSingle [] = Error(ExpectedSingle 0) @>
            test <@ Check.takeSingle [ 1; 2 ] = Error(ExpectedSingle 2) @>
            test <@ Check.atMostOne [] = Ok () @>
            test <@ Check.atMostOne [ 1; 2 ] = Error(ExpectedAtMostOne 2) @>
            test <@ Check.whenAtMostOne [ 5 ] = Ok [ 5 ] @>
            test <@ Check.takeAtMostOne [] = Ok None @>
            test <@ Check.takeAtMostOne [ 5 ] = Ok(Some 5) @>
            test <@ Check.takeAtMostOne [ 1; 2 ] = Error(ExpectedAtMostOne 2) @>
            test <@ Check.atLeastOne [ 5 ] = Ok () @>
            test <@ Check.atLeastOne [] = Error ExpectedAtLeastOne @>
            test <@ Check.whenAtLeastOne [ 5 ] = Ok [ 5 ] @>
            test <@ Check.moreThanOne [ 1; 2 ] = Ok () @>
            test <@ Check.moreThanOne [ 5 ] = Error(ExpectedMoreThanOne 1) @>
            test <@ Check.whenMoreThanOne [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.hasCount 2 [ 1; 2 ] = Ok () @>
            test <@ Check.hasCount 2 [ 1 ] = Error () @>
            test <@ Check.whenCount 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.contains 2 [ 1; 2 ] = Ok () @>
            test <@ Check.contains 3 [ 1; 2 ] = Error () @>
            test <@ Check.whenContains 2 [ 1; 2 ] = Ok [ 1; 2 ] @>
            test <@ Check.hasDuplicates [ 1; 2; 1 ] = Ok () @>
            test <@ Check.hasDuplicates [ 1; 2; 3 ] = Error () @>
            test <@ Check.whenHasDuplicates [ 1; 2; 1 ] = Ok [ 1; 2; 1 ] @>
            test <@ Check.hasNoDuplicates [ 1; 2; 3 ] = Ok () @>
            test <@ Check.hasNoDuplicates [ 1; 2; 1 ] = Error () @>
            test <@ Check.whenHasNoDuplicates [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>

            test <@ Check.equalTo 3 3 = Ok () @>
            test <@ Check.whenEqualTo 3 3 = Ok 3 @>
            test <@ Check.notEqualTo 3 4 = Ok () @>
            test <@ Check.whenNotEqualTo 3 4 = Ok 4 @>

            test <@ Check.notNullOrEmpty "hello" = Ok () @>
            test <@ Check.nullOrEmpty "" = Ok () @>
            test <@ Check.whenNotNullOrEmpty "hello" = Ok "hello" @>
            test <@ Check.whenNullOrEmpty "" = Ok "" @>
            test <@ Check.emptyString "" = Ok () @>
            test <@ Check.emptyString nullString = Error () @>
            test <@ Check.whenEmptyString "" = Ok "" @>
            test <@ Check.notEmptyString " " = Ok () @>
            test <@ Check.notEmptyString "" = Error () @>
            test <@ Check.whenNotEmptyString " " = Ok " " @>
            test <@ Check.notBlank "  x  " = Ok () @>
            test <@ Check.whenNotBlank "  x  " = Ok "  x  " @>
            test <@ Check.blank "   " = Ok () @>
            test <@ Check.whenBlank "   " = Ok "   " @>
            test <@ Check.minLength 3 "abcd" = Ok () @>
            test <@ Check.minLength 3 "ab" = Error(ExpectedMinLength(3, 2)) @>
            test <@ Check.whenMinLength 3 "abcd" = Ok "abcd" @>
            test <@ Check.maxLength 3 "ab" = Ok () @>
            test <@ Check.maxLength 3 "abcd" = Error(ExpectedMaxLength(3, 4)) @>
            test <@ Check.whenMaxLength 3 "ab" = Ok "ab" @>
            test <@ Check.exactLength 3 "abc" = Ok () @>
            test <@ Check.exactLength 3 "ab" = Error(ExpectedExactLength(3, 2)) @>
            test <@ Check.whenExactLength 3 "abc" = Ok "abc" @>
            test <@ Check.matchesRegex "^[a-z]+$" "abc" = Ok () @>
            test <@ Check.matchesRegex "^[a-z]+$" "abc123" = Error () @>
            test <@ Check.whenMatchesRegex "^[a-z]+$" "abc" = Ok "abc" @>

            test <@ Check.greaterThan 3 4 = Ok () @>
            test <@ Check.greaterThan 3 3 = Error(ExpectedGreaterThan(3, 3)) @>
            test <@ Check.whenGreaterThan 3 4 = Ok 4 @>
            test <@ Check.lessThan 3 2 = Ok () @>
            test <@ Check.lessThan 3 3 = Error(ExpectedLessThan(3, 3)) @>
            test <@ Check.whenLessThan 3 2 = Ok 2 @>
            test <@ Check.atLeast 3 3 = Ok () @>
            test <@ Check.atLeast 3 2 = Error(ExpectedAtLeast(3, 2)) @>
            test <@ Check.whenAtLeast 3 3 = Ok 3 @>
            test <@ Check.atMost 3 3 = Ok () @>
            test <@ Check.atMost 3 4 = Error(ExpectedAtMost(3, 4)) @>
            test <@ Check.whenAtMost 3 3 = Ok 3 @>
            test <@ Check.between 3 5 4 = Ok () @>
            test <@ Check.between 3 5 6 = Error(ExpectedBetween(3, 5, 6)) @>
            test <@ Check.whenBetween 3 5 4 = Ok 4 @>
            test <@ Check.positive 1 = Ok () @>
            test <@ Check.positive 0 = Error(ExpectedGreaterThan(0, 0)) @>
            test <@ Check.whenPositive 1 = Ok 1 @>
            test <@ Check.nonNegative 0 = Ok () @>
            test <@ Check.nonNegative -1 = Error(ExpectedAtLeast(0, -1)) @>
            test <@ Check.whenNonNegative 0 = Ok 0 @>
            test <@ Check.negative -1 = Ok () @>
            test <@ Check.negative 0 = Error(ExpectedLessThan(0, 0)) @>
            test <@ Check.whenNegative -1 = Ok -1 @>
            test <@ Check.nonPositive 0 = Ok () @>
            test <@ Check.nonPositive 1 = Error(ExpectedAtMost(0, 1)) @>
            test <@ Check.whenNonPositive 0 = Ok 0 @>

        [<Fact>]
        let ``Result covers fail-fast helpers and the result computation expression`` () =
            let workflow =
                result {
                    let! value = Ok 20
                    let! divisor = Ok 2
                    do! Ok ()
                    return value / divisor
                }

            test <@ Result.map ((+) 1) (Ok 10) = Ok 11 @>
            test <@ Result.bind (fun value -> Ok(value + 5)) (Ok 7) = Ok 12 @>
            test <@ Result.mapError string (Error 42) = Error "42" @>
            test <@ (Check.isTrue false |> Result.mapError (fun _ -> "invalid")) = Error "invalid" @>
            test <@ workflow = Ok 10 @>

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
                            return! address.City |> Check.whenNotBlank |> Check.withError "City required"
                        }

                    return { address with City = city }
                }

            let validateCustomer customer =
                validate.key "customer" {
                    let! name =
                        validate.name "Name" {
                            return! customer.Name |> Check.whenNotBlank |> Check.withError "Name required"
                        }

                    and! address = validateAddress customer.Address

                    and! lines =
                        Validation.key "lines" (
                            customer.Lines
                            |> Validation.traverseIndexed (fun index line ->
                                validate.name "Name" {
                                    return! line |> Check.whenNotBlank |> Check.withError $"Line {index} name required"
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
