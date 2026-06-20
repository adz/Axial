# STM Evolution: From Composable Locks to Optimistic Coordination

## Status
Proposed Plan (Target: 1.0 Milestone)

## Context
The current Axial `STM` implementation is a "Composable Lock" model. It uses a **Global Lock** and a **Journal** to ensure atomicity across multiple `TRef` variables. While it is deadlock-safe and composable, it lacks the core **Coordination** features that make STM powerful in ZIO or Haskell: `retry` and `orElse`.

Without `retry`, users cannot express "wait until this condition is met" without manual, inefficient polling.

## Target Model: Optimistic Coordination Engine
We will shift from a pessimistic (global lock) model to an **Optimistic Concurrency Control (OCC)** model with suspension support.

### 1. Versioned TRef
Every `TRef<'T>` will contain:
- `Value: 'T`
- `Version: int64` (Global monotonic counter or per-ref counter)
- `Lock: obj` (For fine-grained commit-phase locking)

### 2. Enhanced Journaling
The `TJournal` will track:
- **Read-Set:** Every `TRef` accessed, and the `Version` it had at the time of the read.
- **Write-Set:** New values intended for commit.

### 3. The Coordination Primitives

#### `retry` (Suspension)
When `retry` is called:
1. The transaction identifies all `TRef`s in its **Read-Set**.
2. It registers the current Fiber (via `TaskCompletionSource`) into a **Waiter Registry** for those specific `TRef` IDs.
3. The Fiber suspends.
4. When any other transaction commits a change to one of those `TRef`s, the Waiter Registry wakes the suspended Fiber to try again.

#### `orElse` (Compositional Choice)
Allows `stm1 |> STM.orElse stm2`. 
1. If `stm1` succeeds, its journal is merged into the parent/committed.
2. If `stm1` calls `retry`, the engine discards `stm1`'s local changes and immediately attempts `stm2`.
3. If both `retry`, the Read-Sets are combined, and the fiber suspends.

## ZIO Performance Strategy
To match ZIO/Haskell performance levels on the .NET ThreadPool:

### 1. Fine-Grained Sorted Locking
Instead of a Global Lock, we use a **Two-Phase Commit (2PC)**:
- **Phase 1 (Validation):** Check if any `TRef` in the Read-Set has changed version.
- **Phase 2 (Locking):** Lock only the `TRef`s in the Write-Set.
- **Deadlock Avoidance:** Locks MUST be acquired in strictly increasing order of `TRef.Id`.

### 2. Lock-Free Validation
The Read-Set validation happens *before* locking to maximize concurrency. If validation fails, we immediately back off and retry without ever touching a lock.

### 3. Efficient Waiter Registry
Use a `ConcurrentDictionary<int64, WaiterList>` with high-performance linked nodes. Waking waiters happens **outside** the critical commit section to prevent holding locks while triggering thread-pool activity.

## Compelling Examples

### 1. Bounded Queue (Atomic Coordination)
This is impossible in the current model without polling.
```fsharp
type TQueue<'T> = { 
    Items: TRef<FSharpList<'T>>
    Capacity: int 
}

let take (q: TQueue<'T>) = stm {
    let! items = TRef.get q.Items
    match items with
    | [] -> return! STM.retry // Automatically suspends until items are added
    | head::tail ->
        do! TRef.set tail q.Items
        return head
}

let put (item: 'T) (q: TQueue<'T>) = stm {
    let! items = TRef.get q.Items
    if items.Length >= q.Capacity then
        return! STM.retry // Suspends until space is available
    else
        do! TRef.set (items @ [item]) q.Items
}
```

### 2. Atomic Transfer with Pre-condition
```fsharp
let transfer amount fromAcc toAcc = stm {
    let! balance = TRef.get fromAcc
    if balance < amount then
        return! STM.retry // Wait until the account has enough money
    do! TRef.update (fun b -> b - amount) fromAcc
    do! TRef.update (fun b -> b + amount) toAcc
}
```

## Implementation Road-map
1. **Phase A:** Internal refactor of `TRef` to support `Version` and `Lock`.
2. **Phase B:** Rewrite `STM.atomically` to use 2PC and version validation.
3. **Phase C:** Implement Global Waiter Registry and `retry` exception handling.
4. **Phase D:** Add `orElse` with nested journaling.
5. **Phase E:** Benchmarking vs. ZIO (Scala) and standard .NET `BlockingCollection`.

## Risks
- **Livelock:** Optimistic retries can lead to livelock under extreme contention. We need an adaptive backoff/yield strategy.
- **Memory Pressure:** Waiter registration must be extremely careful to avoid leaking `TaskCompletionSource` nodes on cancellation.
