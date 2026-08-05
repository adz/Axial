// Axial.Parse installed alone. Parse has zero dependencies, so this fixture is the direct test of
// that claim: if anything leaks in, the restore graph shows it.

open Axial.Parse

[<EntryPoint>]
let main _ =
    let quantity = Parse.int "42"
    let price = Parse.decimal "9.99"
    let broken = Parse.int "not-a-number"

    match quantity, price, broken with
    | Ok 42, Ok 9.99m, Error _ ->
        printfn "Consumer.Parse OK"
        0
    | other ->
        eprintfn "Consumer.Parse FAILED: %A" other
        1
