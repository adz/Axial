#load "preamble.fsx"
open Axial
open Preamble

let pricing : Flow<PricingEnv, string, decimal> = Flow.envWith _.Pricing
let checkout : Flow<CheckoutEnv, string, string> = Flow.envWith _.Checkout
let program : Flow<PricingEnv, string, string> = flow {
    let! _ = pricing
    let! value = checkout
    return value
}
