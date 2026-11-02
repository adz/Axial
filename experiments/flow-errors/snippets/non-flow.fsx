#load "preamble.fsx"
open Axial
open Preamble

let program : Flow<PricingEnv, string, int> = flow {
    let! value = 42
    return value
}
