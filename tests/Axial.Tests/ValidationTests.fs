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
            test <@ Check.equalTo 3 3 @>
            test <@ Check.notEqualTo 3 4 @>
            test <@ Check.greaterThan 3 4 @>
            test <@ Check.lessThan 3 2 @>
            test <@ Check.atLeast 3 3 @>
            test <@ Check.atMost 3 3 @>
            test <@ Check.between 3 5 4 @>
            test <@ Check.positive 1 @>
            test <@ Check.nonNegative 0 @>
            test <@ Check.negative -1 @>
            test <@ Check.nonPositive 0 @>
            test <@ Check.notEmpty [ 1; 2 ] @>
            test <@ Check.isEmpty Seq.empty<int> @>
            test <@ Check.contains 2 [ 1; 2 ] @>
            test <@ Check.hasCount 2 [ 1; 2 ] @>
            test <@ Check.hasDuplicates [ 1; 2; 1 ] @>
            test <@ Check.hasNoDuplicates [ 1; 2; 3 ] @>
            test <@ Check.isSingle [ 5 ] @>
            test <@ Check.atMostOne [ 5 ] @>
            test <@ Check.atLeastOne [ 5 ] @>
            test <@ Check.moreThanOne [ 1; 2 ] @>
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
            test <@ ([ 1; 2 ] |> Result.keepIf (Check.contains 2) "missing") = Ok [ 1; 2 ] @>
            test <@ ([ 1; 2; 3 ] |> Result.keepIf Check.hasNoDuplicates "duplicate") = Ok [ 1; 2; 3 ] @>
            test <@ ("ab" |> Result.minLength 3) = Error(ExpectedMinLength(3, 2)) @>
            test <@ ("abcd" |> Result.maxLength 3) = Error(ExpectedMaxLength(3, 4)) @>
            test <@ ("ab" |> Result.exactLength 3) = Error(ExpectedExactLength(3, 2)) @>
            test <@ (6 |> Result.range 3 5) = Error(ExpectedAtMost(5, 6)) @>
            test <@ (3 |> Result.greaterThan 3) = Error(ExpectedGreaterThan(3, 3)) @>
            test <@ (3 |> Result.lessThan 3) = Error(ExpectedLessThan(3, 3)) @>
            test <@ (2 |> Result.atLeast 3) = Error(ExpectedAtLeast(3, 2)) @>
            test <@ (4 |> Result.atMost 3) = Error(ExpectedAtMost(3, 4)) @>
            test <@ ([ 5 ] |> Result.single) = Ok 5 @>
            test <@ ([] |> Result.single) = Error(ExpectedSingle 0) @>
            test <@ ([ 5 ] |> Result.atMostOne) = Ok(Some 5) @>
            test <@ ([] |> Result.atLeastOne) = Error ExpectedAtLeastOne @>
            test <@ ([ 5 ] |> Result.moreThanOne) = Error(ExpectedMoreThanOne 1) @>
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
