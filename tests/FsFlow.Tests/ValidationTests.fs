namespace FsFlow.Tests

open Microsoft.FSharp.Core
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module ValidationTests =
        [<Fact>]
        let ``Check covers the pure result surface`` () =
            test <@ Check.not (Check.okIf true) = Error () @>
            test <@ Check.not (Check.okIf false) = Ok () @>

            test <@ Check.``and`` (Check.okIf true) (Check.okIfSome (Some 10)) = Ok () @>
            test <@ Check.``and`` (Check.okIf true) (Check.okIf false) = Error () @>

            test <@ Check.``or`` (Check.okIf false) (Check.okIfSome (Some 10)) = Ok () @>
            test <@ Check.``or`` (Check.okIf false) (Check.okIf false) = Error () @>

            test <@ Check.all [ Check.okIf true; Check.okIf true; Check.okIf true ] = Ok () @>

            let allShortCircuits =
                seq {
                    yield Check.okIf true
                    yield Check.okIf false
                    failwith "Check.all should short-circuit before the third item"
                }

            test <@ Check.all allShortCircuits = Error () @>

            test <@ Check.any [ Check.okIf false; Check.okIf false; Check.okIf true ] = Ok () @>

            let anyShortCircuits =
                seq {
                    yield Check.okIf true
                    failwith "Check.any should short-circuit before the second item"
                }

            test <@ Check.any anyShortCircuits = Ok () @>

            test <@ Check.okIf true = Ok () @>
            test <@ Check.okIf false = Error () @>
            test <@ Check.failIf true = Error () @>
            test <@ Check.failIf false = Ok () @>
            test <@ Check.fromPredicate (fun value -> value > 3) 4 = Ok 4 @>
            test <@ Check.fromPredicate (fun value -> value > 3) 2 = Error () @>
            test <@ Check.fromTry (true, 42) = Ok 42 @>
            test <@ Check.fromTry (false, 42) = Error () @>
            test <@ Check.okIfTrueTuple (true, "value") = Ok "value" @>
            test <@ Check.okIfTrueTuple (false, "value") = Error () @>
            test <@ Check.fromChoice (Choice1Of2 42) = Ok 42 @>
            test <@ Check.fromChoice (Choice2Of2 "missing") = Error "missing" @>

            test <@ Check.okIfSome (Some 10) = Ok 10 @>
            test <@ Check.okIfSome None = Error () @>
            test <@ Check.failIfNone None = Error () @>
            test <@ Check.failIfNone (Some 7) = Ok 7 @>

            test <@ Check.okIfValueSome (ValueSome 11) = Ok 11 @>
            test <@ Check.okIfValueNone ValueNone = Ok () @>
            test <@ Check.failIfValueSome ValueNone = Ok () @>
            test <@ Check.failIfValueNone (ValueSome 8) = Ok 8 @>
            test <@ Check.okIfNotNullable (System.Nullable 12) = Ok 12 @>
            test <@ Check.okIfNotNullable (System.Nullable<int>()) = Error () @>
            test <@ Check.okIfNullable (System.Nullable<int>()) = Ok () @>
            test <@ Check.okIfNullable (System.Nullable 12) = Error () @>
            test <@ Check.failIfNotNullable (System.Nullable<int>()) = Ok () @>
            test <@ Check.failIfNotNullable (System.Nullable 12) = Error () @>
            test <@ Check.failIfNullable (System.Nullable 12) = Ok 12 @>
            test <@ Check.failIfNullable (System.Nullable<int>()) = Error () @>
            test <@ Check.notNullable (System.Nullable 12) = Ok 12 @>
            test <@ Check.notNullable (System.Nullable<int>()) = Error CheckError.Null @>

            let nonNull = "flowkit"
            let nullString: string = null

            test <@ Check.okIfNotNull nonNull = Ok "flowkit" @>
            test <@ Check.okIfNull nullString = Ok () @>
            test <@ Check.failIfNotNull nullString = Error () @>
            test <@ Check.failIfNull nonNull = Error () @>

            test <@ Check.okIfNotEmpty [ 1; 2 ] |> Result.map Seq.toList = Ok [ 1; 2 ] @>
            test <@ Check.okIfEmpty Seq.empty = Ok () @>
            test <@ Check.failIfNotEmpty Seq.empty = Ok () @>
            test <@ Check.failIfEmpty Seq.empty = Error () @>
            test <@ Check.okIfExactlyOne [ 5 ] = Ok 5 @>
            test <@ Check.okIfExactlyOne [] = Error(ExpectedExactlyOne 0) @>
            test <@ Check.okIfExactlyOne [ 1; 2 ] = Error(ExpectedExactlyOne 2) @>
            test <@ Check.failIfExactlyOne [] |> Result.map Seq.toList = Ok [] @>
            test <@ Check.failIfExactlyOne [ 5 ] = Error ExpectedNotExactlyOne @>
            test <@ Check.okIfAtMostOne [] = Ok None @>
            test <@ Check.okIfAtMostOne [ 5 ] = Ok(Some 5) @>
            test <@ Check.okIfAtMostOne [ 1; 2 ] = Error(ExpectedAtMostOne 2) @>
            test <@ Check.failIfAtMostOne [ 1; 2 ] |> Result.map Seq.toList = Ok [ 1; 2 ] @>
            test <@ Check.failIfAtMostOne [] = Error(ExpectedMoreThanOne 0) @>
            test <@ Check.failIfAtMostOne [ 5 ] = Error(ExpectedMoreThanOne 1) @>
            test <@ Check.okIfCountIs 2 [ 1; 2 ] = Ok () @>
            test <@ Check.okIfCountIs 2 [ 1 ] = Error () @>
            test <@ Check.okIfContains 2 [ 1; 2 ] = Ok () @>
            test <@ Check.okIfContains 3 [ 1; 2 ] = Error () @>

            test <@ Check.okIfEqual 3 3 = Ok () @>
            test <@ Check.okIfNotEqual 3 4 = Ok () @>
            test <@ Check.failIfEqual 3 4 = Ok () @>
            test <@ Check.failIfNotEqual 3 3 = Ok () @>

            test <@ Check.okIfNonEmptyStr "hello" = Ok "hello" @>
            test <@ Check.okIfEmptyStr "" = Ok () @>
            test <@ Check.okIfNotBlank "  x  " = Ok "  x  " @>
            test <@ Check.okIfBlank "   " = Ok () @>
            test <@ Check.failIfNonEmptyStr "" = Ok () @>
            test <@ Check.failIfEmptyStr "hello" = Ok "hello" @>
            test <@ Check.failIfNotBlank "   " = Ok () @>
            test <@ Check.failIfBlank "x" = Ok "x" @>
            test <@ Check.notNull nonNull = Ok "flowkit" @>
            test <@ Check.notBlank "  x  " = Ok "  x  " @>
            test <@ Check.notEmpty [ 1; 2 ] |> Result.map Seq.toList = Ok [ 1; 2 ] @>
            test <@ Check.blank "   " = Ok () @>
            test <@ Check.equal 3 3 = Ok () @>
            test <@ Check.notEqual 3 4 = Ok () @>

            test <@ Check.notBlank "  x  " = Ok "  x  " @>
            test <@ Check.notNull nonNull = Ok "flowkit" @>

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
            test <@ (Check.okIf false |> Result.mapError (fun _ -> "invalid")) = Error "invalid" @>
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
                            return! address.City |> Check.notBlank |> Check.orError "City required"
                        }

                    return { address with City = city }
                }

            let validateCustomer customer =
                validate.key "customer" {
                    let! name =
                        validate.name "Name" {
                            return! customer.Name |> Check.notBlank |> Check.orError "Name required"
                        }

                    and! address = validateAddress customer.Address

                    and! lines =
                        Validation.key "lines" (
                            customer.Lines
                            |> Validation.traverseIndexed (fun index line ->
                                validate.name "Name" {
                                    return! line |> Check.notBlank |> Check.orError $"Line {index} name required"
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
