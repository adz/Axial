// Axial.Result installed alone. Asserts the package restores with no sibling Axial package present
// and that the result { } builder and accumulating helpers are reachable from one open.

open Axial.Result

type SignupError = MissingName

let private accumulated : Result<int, SignupError list> =
    result.list {
        let! first = Ok 1
        and! second = Ok 2
        return first + second
    }

let private failFast : Result<string, SignupError> =
    result {
        let! name = Ok "Ada"
        do! Ok()
        return name
    }

[<EntryPoint>]
let main _ =
    let traversed = Result.traverse (fun value -> Ok(value * 2)) [ 1; 2; 3 ]

    match accumulated, failFast, traversed with
    | Ok 3, Ok "Ada", Ok [ 2; 4; 6 ] ->
        printfn "Consumer.Result OK"
        0
    | other ->
        eprintfn "Consumer.Result FAILED: %A" other
        1
