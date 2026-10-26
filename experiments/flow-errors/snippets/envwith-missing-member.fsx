#load "preamble.fsx"
open Axial
open Preamble

let program : Flow<PricingEnv, string, int> =
    Flow.envWith _.Clock
