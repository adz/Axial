/// Shows how Policy adapts each Axial verification boundary — raw parsing, refined
/// construction, schema input parsing, intrinsic validation, and contextual rules —
/// into one workflow error type that Flow.verify can run inside a flow.
module PolicyExamples

open Axial.Flow
open Axial.Refined
open Axial.Schema
open Axial.Validation
open Axial.Validation.Schema

type Quantity = private Quantity of int

module Quantity =
    let create (value: int) = Quantity value
    let value (Quantity value) = value

    let schema : ValueSchema<Quantity> =
        Value.int
        |> Value.withConstraint (SchemaConstraint.greaterThan 0)
        |> Value.refined create value

type OrderLine =
    { Sku: string
      Quantity: Quantity }

let orderLineSchema =
    Schema.recordFor<OrderLine, _> (fun sku quantity ->
        { Sku = sku
          Quantity = quantity })
    |> Schema.text "sku" _.Sku
    |> Schema.field "quantity" _.Quantity Quantity.schema
    |> Schema.build

type OrderEnv =
    { MaxLineQuantity: int
      EnforceQuantityCap: bool }

type OrderError =
    | QuantityNotANumber
    | QuantityNotPositive
    | LineRejected of Diagnostic<SchemaError> list
    | QuantityOverCap of int

// 1. Parsing: adapt a raw text parser, replacing its ParseError with a workflow error.
let parseQuantityText : Policy<OrderEnv, OrderError, string, int> =
    Policy.withError Parse.int QuantityNotANumber

// 2. Refined construction: adapt a refinement smart constructor.
let refinePositive : Policy<OrderEnv, OrderError, int, PositiveInt> =
    Policy.withError Refine.positiveInt QuantityNotPositive

// 3. Schema input result: adapt Input.parse over raw boundary input.
let parseOrderLine : Policy<OrderEnv, OrderError, RawInput, OrderLine> =
    Policy.lift
        (fun raw -> (Input.parse orderLineSchema raw).Result)
        (Diagnostics.flatten >> LineRejected)

// 4. Validation result: adapt intrinsic validation of an existing model.
let validateOrderLine : Policy<OrderEnv, OrderError, OrderLine, OrderLine> =
    Policy.lift
        (fun line ->
            Axial.Validation.Schema.Validation.validate orderLineSchema line
            |> Axial.Validation.Validation.toResult)
        (Diagnostics.flatten >> LineRejected)

// 5. Contextual rules: adapt a RuleSet evaluated with the workflow environment.
let quantityCapRules (env: OrderEnv) : RuleSet<OrderLine, OrderError> =
    Rules.create (fun line ->
        if Quantity.value line.Quantity > env.MaxLineQuantity then
            Rules.failAt [ PathSegment.Name "quantity" ] (QuantityOverCap env.MaxLineQuantity)
        else
            Ok ())

let underQuantityCap : Policy<OrderEnv, OrderError, OrderLine, OrderLine> =
    Policy.context
        (fun env line -> Rules.apply (quantityCapRules env) line)
        (Diagnostics.flatten >> List.map _.Error >> List.head)

// Policies over the same input/output compose, and environment predicates can
// switch a policy off without changing the workflow shape.
let acceptOrderLine : Policy<OrderEnv, OrderError, RawInput, OrderLine> =
    Policy.compose
        parseOrderLine
        (Policy.optional (fun env -> env.EnforceQuantityCap) underQuantityCap)

let acceptLine (raw: RawInput) : Flow<OrderEnv, OrderError, OrderLine> =
    flow {
        let! line = raw |> Flow.verify acceptOrderLine
        return line
    }

let run () =
    let env =
        { MaxLineQuantity = 10
          EnforceQuantityCap = true }

    let raw quantity =
        RawInput.Object(
            Map [ "sku", RawInput.Scalar "SKU-1"; "quantity", RawInput.Scalar quantity ])

    printfn "Policy examples"
    printfn "  parse text quantity: %A" (parseQuantityText env "3")
    printfn "  refine positive:     %A" (refinePositive env 3)
    printfn "  accepted line:       %A" (acceptLine (raw "3") |> fun f -> f.RunSynchronously(env))
    printfn "  rejected (not int):  %A" (acceptLine (raw "many") |> fun f -> f.RunSynchronously(env))
    printfn "  rejected (over cap): %A" (acceptLine (raw "50") |> fun f -> f.RunSynchronously(env))

    printfn
        "  cap disabled:        %A"
        (acceptLine (raw "50") |> fun f -> f.RunSynchronously({ env with EnforceQuantityCap = false }))
