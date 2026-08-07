/// Shows how Policy adapts ordinary result-returning checks — parsing, construction, and
/// environment-aware admission — into one workflow error type that Flow.verify can run inside a flow.
///
/// Every check below is plain F# returning a plain Result. That is the point: Policy does not care where
/// a check came from, only that it fails into the workflow's error channel. Swap any of them for a
/// validation library's function and nothing else in this file changes.
module PolicyExamples

open Axial

// A raw request line, exactly as it arrives at a boundary.
type RawLine = { Sku: string; Quantity: string }

type OrderLine = { Sku: string; Quantity: int }

type OrderEnv =
    { MaxLineQuantity: int
      EnforceQuantityCap: bool }

type OrderError =
    | QuantityNotANumber
    | QuantityNotPositive
    | SkuMissing
    | QuantityOverCap of int

// Ordinary result-returning checks. Nothing here knows about Flow.
let private parseInt (text: string) : Result<int, string> =
    match System.Int32.TryParse text with
    | true, value -> Ok value
    | _ -> Error $"'{text}' is not a number"

let private requirePositive (value: int) : Result<int, unit> =
    if value > 0 then Ok value else Error()

// 1. withError: adapt a check, discarding its own error for a fixed workflow error.
let parseQuantity : Policy<OrderEnv, OrderError, string, int> =
    Policy.withError parseInt QuantityNotANumber

// 2. lift: adapt a check, mapping its error into the workflow error type.
let positiveQuantity : Policy<OrderEnv, OrderError, int, int> =
    Policy.lift requirePositive (fun () -> QuantityNotPositive)

// 3. compose: policies over matching types chain left to right, short-circuiting on the first failure.
let quantity : Policy<OrderEnv, OrderError, string, int> =
    Policy.compose parseQuantity positiveQuantity

let private sku : Policy<OrderEnv, OrderError, string, string> =
    Policy.withError
        (fun (text: string) ->
            if System.String.IsNullOrWhiteSpace text then Error() else Ok text)
        SkuMissing

// 4. context: a check that reads the environment before deciding.
let underQuantityCap : Policy<OrderEnv, OrderError, OrderLine, OrderLine> =
    Policy.context
        (fun env line ->
            if line.Quantity > env.MaxLineQuantity then
                Error env.MaxLineQuantity
            else
                Ok line)
        QuantityOverCap

let private buildLine : Policy<OrderEnv, OrderError, RawLine, OrderLine> =
    fun env raw ->
        match sku env raw.Sku, quantity env raw.Quantity with
        | Ok checkedSku, Ok checkedQuantity -> Ok { Sku = checkedSku; Quantity = checkedQuantity }
        | Error failure, _
        | _, Error failure -> Error failure

// 5. optional: an environment predicate switches a policy off without changing the workflow's shape.
let acceptOrderLine : Policy<OrderEnv, OrderError, RawLine, OrderLine> =
    Policy.compose buildLine (Policy.optional _.EnforceQuantityCap underQuantityCap)

// Flow.verify is the only place the workflow meets the policy. The flow's error type is OrderError,
// and no check above had to know that.
let acceptLine (raw: RawLine) : Flow<OrderEnv, OrderError, OrderLine> =
    flow {
        let! line = raw |> Flow.verify acceptOrderLine
        return line
    }

let run () =
    let environment =
        { MaxLineQuantity = 10
          EnforceQuantityCap = true }

    let raw quantity : RawLine = { Sku = "SKU-1"; Quantity = quantity }

    let accept quantity =
        (acceptLine (raw quantity)).RunSynchronously(environment)

    printfn "Policy examples"
    printfn "  accepted:            %A" (accept "3")
    printfn "  rejected (not int):  %A" (accept "many")
    printfn "  rejected (zero):     %A" (accept "0")
    printfn "  rejected (over cap): %A" (accept "50")

    // With the cap disabled, the same workflow admits the same line.
    let relaxed = { environment with EnforceQuantityCap = false }
    printfn "  cap disabled:        %A" ((acceptLine (raw "50")).RunSynchronously(relaxed))
