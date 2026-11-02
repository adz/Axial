#load "preamble.fsx"
open Axial
open Preamble

let source : Flow<PricingEnv, int, int> = Flow.fail 1
let program : Flow<PricingEnv, string, int> = flow {
    let! value = source
    return value
}
