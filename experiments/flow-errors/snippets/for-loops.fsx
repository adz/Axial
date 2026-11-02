#load "preamble.fsx"
open Axial

let listLoop : Flow<unit, string, unit> =
    flow {
        for value in [ 1; 2; 3 ] do
            do! Flow.ok (ignore value)
    }

let sequenceLoop : Flow<unit, string, unit> =
    flow {
        for value in (seq { 1; 2; 3 }) do
            do! Flow.ok (ignore value)
    }

let arrayLoop : Flow<unit, string, unit> =
    flow {
        for value in [| 1; 2; 3 |] do
            do! Flow.ok (ignore value)
    }

printfn "list, seq, and array for-loops compiled"
