#r "../../../artifacts/bin/Axial/release_net8.0/Axial.dll"
open Axial

type IHasPayments = abstract Payments: string
type PricingEnv = { Pricing: decimal }
type CheckoutEnv = { Checkout: string }

module Payments =
    let inline service<'env, 'error when 'env :> IHasPayments> : Flow<'env, 'error, string> =
        Flow.envWith (fun env -> env.Payments)
