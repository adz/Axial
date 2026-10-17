namespace Axial.State

open System
open System.Collections.Generic
open System.Threading
open Axial

/// <summary>
/// Internal interface for transactional references used by the STM engine.
/// </summary>
type internal ITRef =
    abstract Id: int64
    abstract Commit: obj -> unit
    abstract CurrentValue: obj

/// <summary>
/// Represents a transactional reference that can be updated atomically within an <see cref="T:Axial.State.STM`1" /> transaction.
/// </summary>
/// <typeparam name="T">The type of the value stored in the reference.</typeparam>
type TRef<'T> internal (initialValue: 'T) =
    static let idCounter = ref 0L
    let id = Platform.nextId idCounter
    let mutable value = initialValue
    
    interface ITRef with
        member _.Id = id
        member _.Commit(v) = value <- unbox<'T> v
        member _.CurrentValue = box value
        
    member internal _.Id = id
    member internal _.Value = value

/// <summary>
/// Internal journal used to track transactional changes.
/// </summary>
type internal TJournal = Dictionary<int64, obj * ITRef>

type internal TransactionResult<'T> =
    | Done of 'T
    | Retry

type internal TContext =
    {
        Journal: TJournal
        Reads: HashSet<int64>
    }

/// <summary>
/// Represents a transactional operation that can be composed, retried, and executed atomically.
/// </summary>
/// <typeparam name="T">The type of the value produced by the operation.</typeparam>
type STM<'T> = private STM of (TContext -> TransactionResult<'T>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module TRef =
    /// <summary>Creates a new <see cref="T:Axial.State.TRef`1" /> with the initial value within an STM transaction.</summary>
    /// <param name="value">The initial value for the transactional reference.</param>
    /// <returns>An STM operation that, when executed, produces a new transactional reference.</returns>
    /// <example>
    /// <code>
    /// let tx = stm {
    ///     let! counter = TRef.make 0
    ///     return counter
    /// }
    /// </code>
    /// </example>
    let make (value: 'T) : STM<TRef<'T>> =
        STM(fun _ -> Done(TRef(value)))

    /// <summary>Reads the current value of the transactional reference within a transaction.</summary>
    /// <param name="tref">The transactional reference to read.</param>
    /// <returns>An STM operation that produces the current value of the reference.</returns>
    /// <example>
    /// <code>
    /// let tx (counter: TRef&lt;int&gt;) = stm {
    ///     let! value = TRef.get counter
    ///     return value
    /// }
    /// </code>
    /// </example>
    let get (tref: TRef<'T>) : STM<'T> =
        STM(fun context ->
            context.Reads.Add tref.Id |> ignore

            match context.Journal.TryGetValue tref.Id with
            | true, (v, _) -> Done(unbox<'T> v)
            | false, _ -> Done tref.Value)

    /// <summary>Sets the value of the transactional reference within a transaction.</summary>
    /// <param name="value">The new value to store in the reference.</param>
    /// <param name="tref">The transactional reference to update.</param>
    /// <returns>An STM operation that sets the reference value.</returns>
    /// <example>
    /// <code>
    /// let tx (counter: TRef&lt;int&gt;) = stm {
    ///     do! TRef.set 10 counter
    /// }
    /// </code>
    /// </example>
    let set (value: 'T) (tref: TRef<'T>) : STM<unit> =
        STM(fun context ->
            context.Journal[tref.Id] <- (box value, tref :> ITRef)
            Done ())

    /// <summary>Updates the value of the transactional reference within a transaction using the supplied function.</summary>
    /// <param name="f">The function to apply to the current value to produce the new value.</param>
    /// <param name="tref">The transactional reference to update.</param>
    /// <returns>An STM operation that updates the reference value.</returns>
    /// <example>
    /// <code>
    /// let tx (counter: TRef&lt;int&gt;) = stm {
    ///     do! TRef.update (fun n -> n + 1) counter
    /// }
    /// </code>
    /// </example>
    let update (f: 'T -> 'T) (tref: TRef<'T>) : STM<unit> =
        STM(fun context ->
            context.Reads.Add tref.Id |> ignore

            let current =
                match context.Journal.TryGetValue tref.Id with
                | true, (v, _) -> unbox<'T> v
                | false, _ -> tref.Value

            context.Journal[tref.Id] <- (box (f current), tref :> ITRef)
            Done ())

    /// <summary>Atomically applies <paramref name="f" />, writes the new state, and returns the value it produced.</summary>
    let modify (f: 'T -> 'v * 'T) (tref: TRef<'T>) : STM<'v> =
        STM(fun context ->
            context.Reads.Add tref.Id |> ignore

            let current =
                match context.Journal.TryGetValue tref.Id with
                | true, (v, _) -> unbox<'T> v
                | false, _ -> tref.Value

            let result, next = f current
            context.Journal[tref.Id] <- (box next, tref :> ITRef)
            Done result)

    /// <summary>Sets the value and returns the previous value.</summary>
    let getAndSet (value: 'T) (tref: TRef<'T>) : STM<'T> =
        modify (fun previous -> previous, value) tref

    /// <summary>Updates the value and returns the previous value.</summary>
    let getAndUpdate (f: 'T -> 'T) (tref: TRef<'T>) : STM<'T> =
        modify (fun previous -> previous, f previous) tref

    /// <summary>Updates the value and returns the new value.</summary>
    let updateAndGet (f: 'T -> 'T) (tref: TRef<'T>) : STM<'T> =
        modify (fun previous -> let next = f previous in next, next) tref

/// <summary>
/// Computation expression builder for STM transactions.
/// </summary>
type StmBuilder() =
    /// <summary>Wraps a value in an STM transaction.</summary>
    /// <param name="value">The value to return.</param>
    /// <returns>An STM transaction that produces the value.</returns>
    member _.Return(value: 'T) : STM<'T> = STM(fun _ -> Done value)
    /// <summary>Returns the result of another STM transaction.</summary>
    /// <param name="stm">The STM transaction to return from.</param>
    /// <returns>The provided STM transaction.</returns>
    member _.ReturnFrom(stm: STM<'T>) : STM<'T> = stm
    /// <summary>Binds the result of an STM transaction to a function that returns another STM transaction.</summary>
    /// <param name="stm">The first STM transaction.</param>
    /// <param name="f">A function that takes the result of the first transaction and returns a second transaction.</param>
    /// <returns>A new STM transaction that sequences the two operations.</returns>
    member _.Bind(stm: STM<'T>, f: 'T -> STM<'U>) : STM<'U> =
        STM(fun context ->
            let (STM op) = stm
            match op context with
            | Retry -> Retry
            | Done value ->
                let (STM nextOp) = f value
                nextOp context)
    /// <summary>Returns a transaction that does nothing and produces unit.</summary>
    /// <returns>A unit-producing transaction.</returns>
    member _.Zero() : STM<unit> = STM(fun _ -> Done ())
    /// <summary>Delays the execution of a transaction until it is run.</summary>
    /// <param name="f">A function that produces the transaction.</param>
    /// <returns>A delayed STM transaction.</returns>
    member _.Delay(f: unit -> STM<'T>) : STM<'T> = STM(fun context -> let (STM op) = f () in op context)
    /// <summary>Combines two STM transactions, executing them in sequence.</summary>
    /// <param name="stm1">The first transaction to execute.</param>
    /// <param name="stm2">The second transaction to execute.</param>
    /// <returns>A transaction that executes <paramref name="stm1"/> and then <paramref name="stm2"/>.</returns>
    member _.Combine(stm1: STM<unit>, stm2: STM<'T>) : STM<'T> =
        STM(fun context ->
            let (STM op1) = stm1
            let (STM op2) = stm2
            match op1 context with
            | Retry -> Retry
            | Done () -> op2 context)

[<AutoOpen>]
module StmBuilders =
    /// <summary>
    /// The <c>stm { }</c> computation expression for building atomic transactions.
    /// </summary>
    let stm = StmBuilder()

[<RequireQualifiedAccess>]
module STM =
    let private stmLock = obj()
    let private waiters = ResizeArray<Platform.Signal<unit>>()

    let private snapshot (context: TContext) =
        {
            Journal = TJournal(context.Journal)
            Reads = HashSet<int64>(context.Reads)
        }

    /// The number of transactions waiting in <c>retry</c>; used by tests to prove interrupted waits are withdrawn.
    let internal pendingRetries () = Platform.lock stmLock (fun () -> waiters.Count)

    let private freshContext () =
        {
            Journal = TJournal()
            Reads = HashSet<int64>()
        }

    /// <summary>Signals that the current branch should retry once observed state changes.</summary>
    /// <returns>An STM operation that triggers a retry.</returns>
    /// <remarks>
    /// In Axial Flow's current implementation, this suspends the transaction (without blocking the calling
    /// thread) until another transaction commits a change, then re-runs it from the start.
    /// </remarks>
    let retry<'T> : STM<'T> =
        STM(fun _ -> Retry)

    /// <summary>Tries the left branch and falls back to the right branch when the left branch retries.</summary>
    /// <param name="left">The primary STM transaction to attempt.</param>
    /// <param name="right">The fallback STM transaction to run if the first one retries.</param>
    /// <returns>A combined STM transaction that implements choice.</returns>
    let orElse (left: STM<'T>) (right: STM<'T>) : STM<'T> =
        STM(fun context ->
            // Each branch runs against a snapshot so a retrying branch leaves no writes behind;
            // a successful branch's journal and read set must then flow back into the caller's
            // context, or the commit step would never see the branch's writes.
            let adopt (branch: TContext) =
                context.Journal.Clear()

                for KeyValue(key, value) in branch.Journal do
                    context.Journal[key] <- value

                context.Reads.UnionWith branch.Reads

            let attempt (STM op) =
                let branch = snapshot context

                match op branch with
                | Done value ->
                    adopt branch
                    Some(Done value)
                | Retry -> None

            match attempt left with
            | Some outcome -> outcome
            | None ->
                match attempt right with
                | Some outcome -> outcome
                | None -> Retry)

    /// <summary>
    /// Executes an STM transaction atomically within a flow while preserving retry/orElse coordination.
    /// </summary>
    /// <param name="transaction">The STM transaction to execute.</param>
    /// <returns>A flow that performs the transaction and returns its result.</returns>
    /// <remarks>
    /// Axial Flow's STM uses a single mutual-exclusion section around the commit/read step to ensure atomicity
    /// and coordinate retries. While this avoids complex optimistic concurrency control, it means that
    /// transactions are mutually exclusive and high contention can impact throughput. A transaction that
    /// retries does not block the calling thread (or, on Fable, the single JS thread) while it waits: it
    /// suspends on a broadcast signal that every successful commit resolves, then re-runs from the start.
    /// </remarks>
    /// <example>
    /// <code>
    /// let transfer (fromAcc: TRef&lt;int&gt;) (toAcc: TRef&lt;int&gt;) amount =
    ///     stm {
    ///         let! bal = TRef.get fromAcc
    ///         if bal &lt; amount then do! STM.retry
    ///         do! TRef.set (bal - amount) fromAcc
    ///         do! TRef.update (fun b -> b + amount) toAcc
    ///     }
    ///
    /// let flow = STM.atomically (transfer acc1 acc2 100)
    /// </code>
    /// </example>
    let atomically (transaction: STM<'T>) : Flow<'env, 'none, 'T> =
        let attempt (cancellationToken: CancellationToken) : Execution<Platform.LoopStep<unit, 'T>, 'none> =
            let outcome =
                Platform.lock stmLock (fun () ->
                    let (STM op) = transaction
                    let context = freshContext ()

                    match op context with
                    | Done result ->
                        for KeyValue(_, (v, tref)) in context.Journal do
                            tref.Commit(v)

                        // A transaction that wrote nothing changed no state a retrying transaction could be
                        // waiting on, so it wakes nobody.
                        if context.Journal.Count = 0 then
                            Choice1Of2(result, [||])
                        else
                            let pending = waiters.ToArray()
                            waiters.Clear()
                            Choice1Of2(result, pending)
                    | Retry ->
                        let signal = Platform.newSignal ()
                        waiters.Add signal
                        Choice2Of2 signal)

            match outcome with
            | Choice1Of2(result, pending) ->
                for signal in pending do
                    Platform.resolveSignal signal () |> ignore

                Execution.ofValue (Platform.Break result)
            | Choice2Of2 signal ->
                Execution.fold
                    (fun () -> Execution.ofValue (Platform.Continue ()))
                    (fun cause ->
                        // An interrupted retry withdraws its wake-up signal instead of leaving it for the next commit.
                        Platform.lock stmLock (fun () ->
                            let index = waiters.FindIndex(fun waiter -> obj.ReferenceEquals(waiter, signal))
                            if index >= 0 then waiters.RemoveAt index)

                        Execution.ofCause cause)
                    (Platform.awaitSignal signal cancellationToken)

        // A loop rather than recursion, so a transaction that retries many times runs in constant memory.
        Flow(fun _ cancellationToken -> Execution.loop () (fun () -> attempt cancellationToken))

/// <summary>Immutable state held by a transactional queue.</summary>
type internal TQueueState<'a> =
    { Items: 'a list
      Capacity: int option }

/// <summary>A transactional FIFO queue, built from a transactional reference.</summary>
type TQueue<'a> internal (state: TRef<TQueueState<'a>>) =
    member internal _.State = state

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module TQueue =
    /// <summary>Creates a transactional queue with the supplied capacity, or unbounded when <c>None</c>.</summary>
    /// <remarks>A non-positive capacity throws <see cref="T:System.ArgumentOutOfRangeException" />.</remarks>
    let make (capacity: int option) : STM<TQueue<'a>> =
        match capacity with
        | Some capacity when capacity <= 0 ->
            raise (ArgumentOutOfRangeException(nameof capacity, "Queue capacity must be positive."))
        | _ ->
            stm {
                let! state = TRef.make { Items = []; Capacity = capacity }
                return TQueue(state)
            }

    /// <summary>Creates an unbounded transactional queue.</summary>
    let unbounded<'a> : STM<TQueue<'a>> = make None

    /// <summary>Creates a bounded transactional queue.</summary>
    let bounded<'a> (capacity: int) : STM<TQueue<'a>> = make (Some capacity)

    /// <summary>Adds an item, retrying when the queue is at capacity.</summary>
    let offer (item: 'a) (queue: TQueue<'a>) : STM<unit> =
        stm {
            let! state = TRef.get queue.State
            if state.Capacity |> Option.exists (fun capacity -> state.Items.Length >= capacity) then
                do! STM.retry
            do! TRef.set { state with Items = state.Items @ [ item ] } queue.State
        }

    /// <summary>Removes and returns the oldest item, retrying when the queue is empty.</summary>
    let take (queue: TQueue<'a>) : STM<'a> =
        stm {
            let! state = TRef.get queue.State
            match state.Items with
            | [] -> return! STM.retry
            | head :: tail ->
                do! TRef.set { state with Items = tail } queue.State
                return head
        }

    /// <summary>Returns the number of items currently in the queue.</summary>
    let size (queue: TQueue<'a>) : STM<int> =
        stm {
            let! state = TRef.get queue.State
            return state.Items.Length
        }

    /// <summary>Returns whether the queue is empty.</summary>
    let isEmpty (queue: TQueue<'a>) : STM<bool> =
        stm {
            let! state = TRef.get queue.State
            return state.Items.IsEmpty
        }

    /// <summary>Returns whether the queue is at its capacity.</summary>
    let isFull (queue: TQueue<'a>) : STM<bool> =
        stm {
            let! state = TRef.get queue.State
            return (state.Capacity |> Option.exists (fun capacity -> state.Items.Length >= capacity))
        }

/// <summary>A transactional semaphore, built from a transactional reference.</summary>
type TSemaphore internal (permits: TRef<int>) =
    member internal _.Permits = permits

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module TSemaphore =
    /// <summary>Creates a transactional semaphore with the supplied initial permits.</summary>
    /// <remarks>A negative permit count throws <see cref="T:System.ArgumentOutOfRangeException" />.</remarks>
    let make (permits: int) : STM<TSemaphore> =
        if permits < 0 then
            raise (ArgumentOutOfRangeException(nameof permits, "Semaphore permits must be non-negative."))

        stm {
            let! cell = TRef.make permits
            return TSemaphore(cell)
        }

    /// <summary>Acquires a permit, retrying until one is available.</summary>
    let acquire (semaphore: TSemaphore) : STM<unit> =
        stm {
            let! available = TRef.get semaphore.Permits
            if available <= 0 then do! STM.retry
            do! TRef.set (available - 1) semaphore.Permits
        }

    /// <summary>Releases a permit.</summary>
    let release (semaphore: TSemaphore) : STM<unit> =
        stm {
            let! available = TRef.get semaphore.Permits
            do! TRef.set (available + 1) semaphore.Permits
        }

    /// <summary>Returns the number of available permits.</summary>
    let available (semaphore: TSemaphore) : STM<int> = TRef.get semaphore.Permits

/// <summary>Immutable state held by a transactional promise.</summary>
type internal TPromiseState<'error, 'value> =
    | Pending
    | Completed of Result<'value, 'error>

/// <summary>A transactional promise that blocks readers until it is completed.</summary>
type TPromise<'error, 'value> internal (cell: TRef<TPromiseState<'error, 'value>>) =
    member internal _.Cell = cell

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module TPromise =
    /// <summary>Creates a pending transactional promise.</summary>
    let make<'error, 'value> : STM<TPromise<'error, 'value>> =
        stm {
            let! cell = TRef.make Pending
            return TPromise(cell)
        }

    /// <summary>Completes the promise with a value; returns <c>false</c> when it was already completed.</summary>
    let succeed (value: 'value) (promise: TPromise<'error, 'value>) : STM<bool> =
        stm {
            let! state = TRef.get promise.Cell
            match state with
            | Pending ->
                do! TRef.set (Completed(Ok value)) promise.Cell
                return true
            | Completed _ -> return false
        }

    /// <summary>Completes the promise with a typed error; returns <c>false</c> when it was already completed.</summary>
    let fail (error: 'error) (promise: TPromise<'error, 'value>) : STM<bool> =
        stm {
            let! state = TRef.get promise.Cell
            match state with
            | Pending ->
                do! TRef.set (Completed(Error error)) promise.Cell
                return true
            | Completed _ -> return false
        }

    /// <summary>Returns the completed value, retrying until the promise is completed.</summary>
    let await (promise: TPromise<'error, 'value>) : STM<Result<'value, 'error>> =
        stm {
            let! state = TRef.get promise.Cell
            match state with
            | Pending -> return! STM.retry
            | Completed result -> return result
        }

    /// <summary>Returns the completed value without retrying, or <c>None</c> when still pending.</summary>
    let poll (promise: TPromise<'error, 'value>) : STM<Result<'value, 'error> option> =
        stm {
            let! state = TRef.get promise.Cell
            match state with
            | Pending -> return None
            | Completed result -> return Some result
        }
