/// Scenario 1 — checkout orchestration with compensation.
///
/// Reserve stock, charge the card, create a shipment; release the reservation when charging or
/// shipment creation fails. Both versions use the same services and error types, so the diff is
/// the workflow model, not the domain.
///
/// Made visible by the type: required services and the complete expected failure set.
/// Enforced by the runtime: the release action runs on success, typed failure, defect, and interruption.
/// Still the application's responsibility: idempotency keys and reconciliation for remote compensation.
module Axial.Flow.Comparisons.CheckoutCompensation

open System.Threading
open System.Threading.Tasks
open Axial.Flow

// --- Shared domain -----------------------------------------------------------------

type ReservationId = ReservationId of string
type PaymentId = PaymentId of string
type ShipmentId = ShipmentId of string

type InventoryError = | OutOfStock of sku: string
type PaymentError = | CardDeclined of reason: string
type ShippingError = | NoCarrier

type CheckoutError =
    | Inventory of InventoryError
    | Payment of PaymentError
    | Shipping of ShippingError

type CheckoutReceipt = { Reservation: ReservationId; Payment: PaymentId; Shipment: ShipmentId }

type IInventory =
    abstract Reserve : sku: string * quantity: int -> Task<Result<ReservationId, InventoryError>>
    abstract Release : reservation: ReservationId -> Task

type IPayments =
    abstract Charge : amount: decimal -> Task<Result<PaymentId, PaymentError>>

type IShipping =
    abstract CreateShipment : reservation: ReservationId -> Task<Result<ShipmentId, ShippingError>>

// --- Ordinary implementation -------------------------------------------------------

module Ordinary =

    /// Task<CheckoutReceipt> with manually passed services. Expected failures leave the signature:
    /// each Error is rethrown as an exception, so callers see only `Task` and must know the
    /// exception vocabulary out of band.
    exception CheckoutFailed of CheckoutError

    /// The correct version: compensation lives in a catch, written and maintained by hand.
    let checkout (inventory: IInventory) (payments: IPayments) (shipping: IShipping) (sku: string) (amount: decimal) : Task<CheckoutReceipt> =
        task {
            let! reserved = inventory.Reserve(sku, 1)

            match reserved with
            | Error error -> return raise (CheckoutFailed(Inventory error))
            | Ok reservation ->
                try
                    let! charged = payments.Charge amount

                    match charged with
                    | Error error -> return raise (CheckoutFailed(Payment error))
                    | Ok payment ->
                        let! shipped = shipping.CreateShipment reservation

                        match shipped with
                        | Error error -> return raise (CheckoutFailed(Shipping error))
                        | Ok shipment ->
                            return { Reservation = reservation; Payment = payment; Shipment = shipment }
                with error ->
                    // Compensation is coupled to remembering this catch block.
                    do! inventory.Release reservation
                    return raise error
        }

    /// The common bug: a later edit adds an early `return` (here: the shipping branch was moved
    /// out of the try during a refactor) and the newly added failure path skips release.
    let checkoutBuggy (inventory: IInventory) (payments: IPayments) (shipping: IShipping) (sku: string) (amount: decimal) : Task<CheckoutReceipt> =
        task {
            let! reserved = inventory.Reserve(sku, 1)

            match reserved with
            | Error error -> return raise (CheckoutFailed(Inventory error))
            | Ok reservation ->
                let! charged = payments.Charge amount

                match charged with
                | Error error ->
                    // Forgot to release here — the compiler cannot tell.
                    return raise (CheckoutFailed(Payment error))
                | Ok payment ->
                    let! shipped = shipping.CreateShipment reservation

                    match shipped with
                    | Error error ->
                        do! inventory.Release reservation
                        return raise (CheckoutFailed(Shipping error))
                    | Ok shipment ->
                        return { Reservation = reservation; Payment = payment; Shipment = shipment }
        }

// --- Axial implementation ----------------------------------------------------------

module WithFlow =

    /// A named environment record: one field per required capability. Tests provide the same
    /// record with in-memory implementations; nothing about the workflow changes.
    type CheckoutEnv =
        { Inventory: IInventory
          Payments: IPayments
          Shipping: IShipping }

    /// Flow<CheckoutEnv, CheckoutError, CheckoutReceipt>
    ///
    /// The signature carries both the required capabilities and the complete failure set. The
    /// reservation has lexical ownership, so acquire/release form one construct: release runs on
    /// typed failure, defect, and interruption without any hand-written catch.
    let checkout (sku: string) (amount: decimal) : Flow<CheckoutEnv, CheckoutError, CheckoutReceipt> =
        let reserve: Flow<CheckoutEnv, CheckoutError, ReservationId * IInventory> =
            flow {
                let! inventory = Flow.read _.Inventory
                let! reservation = inventory.Reserve(sku, 1) |> Bind.mapError Inventory
                return reservation, inventory
            }

        let fulfil (reservation: ReservationId) : Flow<CheckoutEnv, CheckoutError, CheckoutReceipt> =
            flow {
                let! payments = Flow.read _.Payments
                let! payment = payments.Charge amount |> Bind.mapError Payment

                let! shipping = Flow.read _.Shipping
                let! shipment = shipping.CreateShipment reservation |> Bind.mapError Shipping

                return { Reservation = reservation; Payment = payment; Shipment = shipment }
            }

        Flow.acquireReleaseWith
            reserve
            (fun (reservation, inventory) _ -> inventory.Release reservation)
            (fun (reservation, _) -> fulfil reservation)
