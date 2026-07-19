/// Scenario 7 — atomic inventory reservation under contention.
///
/// Two checkouts race for the last unit; a reservation can wait for replenishment or fall back
/// to an alternative warehouse, without locks leaking into business logic.
///
/// Made visible by the type: the transaction is an STM<'value> value, separate from effects.
/// Enforced by the runtime: a transaction commits all of its TRef changes or none, and
/// STM.retry re-runs from a coherent snapshot when observed state changes.
/// Still the application's responsibility: payment, database, HTTP, and logging effects stay
/// outside the transaction — the guarantee covers STM-managed memory only.
module Axial.Flow.Comparisons.InventoryStm

open System.Threading
open System.Threading.Tasks
open Axial.Flow

// --- Shared domain -----------------------------------------------------------------

type Warehouse = | Local | Regional

// --- Ordinary implementation -------------------------------------------------------

module Ordinary =

    /// Shared mutable state guarded by a lock and a semaphore-signalled wait loop. The failure
    /// modes are the classics: a missed wakeup, an update to one of the two values without the
    /// other, or a lock held across an exception.
    type Inventory(localUnits: int, regionalUnits: int) =
        let gate = obj ()
        let replenished = new SemaphoreSlim(0)
        let mutable localStock = localUnits
        let mutable regionalStock = regionalUnits
        let mutable reservations = 0

        member _.Snapshot = lock gate (fun () -> localStock, regionalStock, reservations)

        member _.Replenish(warehouse: Warehouse, units: int) =
            lock gate (fun () ->
                match warehouse with
                | Local -> localStock <- localStock + units
                | Regional -> regionalStock <- regionalStock + units)

            replenished.Release() |> ignore

        /// Reserve one unit, preferring Local, falling back to Regional, waiting when both are
        /// empty. Both the stock decrement and the reservation increment must stay consistent.
        member this.Reserve(cancellationToken: CancellationToken) : Task<Warehouse> =
            task {
                let mutable outcome = None

                while outcome.IsNone do
                    let claimed =
                        lock gate (fun () ->
                            if localStock > 0 then
                                localStock <- localStock - 1
                                reservations <- reservations + 1
                                Some Local
                            elif regionalStock > 0 then
                                regionalStock <- regionalStock - 1
                                reservations <- reservations + 1
                                Some Regional
                            else
                                None)

                    match claimed with
                    | Some warehouse -> outcome <- Some warehouse
                    | None ->
                        // Wait for a replenishment signal. A Release() between the lock exit
                        // above and this wait is only correct because SemaphoreSlim counts;
                        // swap it for a Monitor pulse and this becomes a missed wakeup.
                        do! replenished.WaitAsync cancellationToken

                return outcome.Value
            }

// --- Axial implementation ----------------------------------------------------------

module WithFlow =

    type Inventory =
        { LocalStock: TRef<int>
          RegionalStock: TRef<int>
          Reservations: TRef<int> }

    let createInventory (localUnits: int) (regionalUnits: int) : Flow<'env, 'error, Inventory> =
        STM.atomically (
            stm {
                let! localStock = TRef.make localUnits
                let! regionalStock = TRef.make regionalUnits
                let! reservations = TRef.make 0
                return { LocalStock = localStock; RegionalStock = regionalStock; Reservations = reservations }
            }
        )

    /// Take one unit from a warehouse, retrying (suspending) until stock is visible there.
    let private reserveFrom (stock: TRef<int>) (warehouse: Warehouse) (inventory: Inventory) : STM<Warehouse> =
        stm {
            let! units = TRef.get stock

            if units < 1 then
                return! STM.retry
            else
                do! TRef.set (units - 1) stock
                do! TRef.update ((+) 1) inventory.Reservations
                return warehouse
        }

    /// Prefer Local; orElse falls back to Regional in the same transaction; if both retry, the
    /// whole transaction suspends until any participating TRef changes, then re-runs from a
    /// coherent snapshot.
    let reserve (inventory: Inventory) : Flow<'env, 'error, Warehouse> =
        STM.atomically (
            STM.orElse
                (reserveFrom inventory.LocalStock Local inventory)
                (reserveFrom inventory.RegionalStock Regional inventory)
        )

    let replenish (warehouse: Warehouse) (units: int) (inventory: Inventory) : Flow<'env, 'error, unit> =
        let stock =
            match warehouse with
            | Local -> inventory.LocalStock
            | Regional -> inventory.RegionalStock

        STM.atomically (TRef.update ((+) units) stock)

    let snapshot (inventory: Inventory) : Flow<'env, 'error, int * int * int> =
        STM.atomically (
            stm {
                let! localStock = TRef.get inventory.LocalStock
                let! regionalStock = TRef.get inventory.RegionalStock
                let! reservations = TRef.get inventory.Reservations
                return localStock, regionalStock, reservations
            }
        )
