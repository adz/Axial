namespace Axial.Tests

open System
open Microsoft.FSharp.Core
open Axial.Result
open Swensen.Unquote
open Xunit

module ResultTests =
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
        test <@ ("Ada" |> Result.okIf (String.IsNullOrWhiteSpace >> not)) = Ok "Ada" @>
        test <@ ("" |> Result.okIf (String.IsNullOrWhiteSpace >> not)) = Error () @>
        test <@ ("" |> Result.okIf (String.IsNullOrWhiteSpace >> not) |> Result.orError "required") = Error "required" @>
        test <@ (true |> Result.requireTrue "invalid") = Ok () @>
        test <@ (false |> Result.requireTrue "invalid") = Error "invalid" @>
        test <@ (Error "boom" |> Result.orError "typed") = Error "typed" @>
        test <@ ("Ada" |> Result.guard (fun _ -> Ok ())) = Ok "Ada" @>
        test <@ ("" |> Result.guard (fun _ -> Error "required")) = Error "required" @>
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
        test <@ Result.traverse (fun value -> if value < 3 then Ok(value * 2) else Error value) [ 1; 2 ] = Ok [ 2; 4 ] @>
        test <@ Result.sequence [ Ok 1; Error "missing"; Ok 3 ] = Error "missing" @>
        test <@ workflow = Ok 10 @>

    [<Fact>]
    let ``tap and tapError observe without changing the result`` () =
        let observed = ResizeArray<string>()

        let success =
            Ok 10
            |> Result.tap (fun value -> observed.Add(sprintf "ok %d" value))
            |> Result.tapError (fun failure -> observed.Add(sprintf "error %s" failure))

        let failure =
            Error "boom"
            |> Result.tap (fun value -> observed.Add(sprintf "ok %d" value))
            |> Result.tapError (fun failure -> observed.Add(sprintf "error %s" failure))

        test <@ success = Ok 10 @>
        test <@ failure = Error "boom" @>
        test <@ List.ofSeq observed = [ "ok 10"; "error boom" ] @>

    [<Fact>]
    let ``result.list collects every error across and! bindings`` () =
        let parseName value =
            if String.IsNullOrWhiteSpace value then Error "name is required" else Ok value

        let parseAge value =
            if value >= 0 then Ok value else Error "age must not be negative"

        let bothOk =
            result.list {
                let! name = parseName "Ada"
                and! age = parseAge 36
                return name, age
            }

        let bothFail =
            result.list {
                let! name = parseName ""
                and! age = parseAge -1
                return name, age
            }

        let oneFails =
            result.list {
                let! name = parseName ""
                and! age = parseAge 36
                return name, age
            }

        test <@ bothOk = Ok("Ada", 36) @>
        test <@ bothFail = Error [ "name is required"; "age must not be negative" ] @>
        test <@ oneFails = Error [ "name is required" ] @>

    [<Fact>]
    let ``accumulating builders fail fast between dependent let! groups`` () =
        let mutable secondRan = false

        let workflow =
            result.list {
                let! first = Error "first failed"

                let! second =
                    secondRan <- true
                    Ok 2

                return first + second
            }

        test <@ workflow = Error [ "first failed" ] @>
        test <@ not secondRan @>

    [<Fact>]
    let ``result.array collects into an array`` () =
        let asArray =
            result.array {
                let! first = Error "first"
                and! second = Error "second"
                return first + second
            }

        test <@ asArray = Error [| "first"; "second" |] @>

    [<Fact>]
    let ``accumulating builders accept an already-collected result without re-wrapping`` () =
        let alreadyCollected: Result<int, string list> = Error [ "earlier"; "failures" ]

        let workflow =
            result.list {
                let! first = alreadyCollected
                and! second = Error "later"
                return first + second
            }

        test <@ workflow = Error [ "earlier"; "failures"; "later" ] @>
