#load "preamble.fsx"
open Axial
open Preamble

let program : Flow<PricingEnv, string, string> = flow {
    let! payments = Payments.service
    return payments
}
