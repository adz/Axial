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
        test <@ Collection.traverseResult (fun value -> if value < 3 then Ok(value * 2) else Error value) [ 1; 2 ] = Ok [ 2; 4 ] @>
        test <@ Collection.sequenceResult [ Ok 1; Error "missing"; Ok 3 ] = Error "missing" @>
        test <@ workflow = Ok 10 @>
