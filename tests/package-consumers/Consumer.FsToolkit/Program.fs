// FsToolkit.ErrorHandling alongside Axial.Refined and Axial.Schema.
//
// The thing being tested is builder ambiguity. Axial.Result also defines a result { } builder, so a
// consumer who wants FsToolkit's must be able to take Refined and Schema WITHOUT Axial.Result being
// dragged in behind them. This fixture deliberately does not reference Axial.Result: if either
// package ever grows that dependency, the result { } below becomes ambiguous and this stops
// compiling, which is the signal we want.

open Axial.Refined
open Axial.Schema
open FsToolkit.ErrorHandling

type Order = { Sku: string; Quantity: int }

module private Order =
    let create sku quantity = { Sku = sku; Quantity = quantity }

let private orderSchema =
    schema<Order> {
        field "sku" _.Sku
        field "quantity" _.Quantity
        construct Order.create
    }

// FsToolkit's result builder, unambiguous.
let private pipeline (sku: string) (quantity: int) =
    result {
        let! items = NonEmptyList.create [ sku ] |> Result.mapError (fun _ -> "empty sku")
        let! checked' = if quantity > 0 then Ok quantity else Error "non-positive quantity"
        return items.ToList(), checked'
    }

[<EntryPoint>]
let main _ =
    let good = pipeline "SKU-1" 3
    let bad = pipeline "SKU-1" 0
    let roundTripped = Schema.check orderSchema { Sku = "SKU-1"; Quantity = 3 }

    match good, bad, roundTripped with
    | Ok([ "SKU-1" ], 3), Error "non-positive quantity", Ok _ ->
        printfn "Consumer.FsToolkit OK"
        0
    | other ->
        eprintfn "Consumer.FsToolkit FAILED: %A" other
        1
