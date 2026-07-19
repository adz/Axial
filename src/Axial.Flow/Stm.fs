namespace Axial.Flow

open System
open System.Collections.Generic
open System.Threading

/// <summary>
/// Internal interface for transactional references used by the STM engine.
/// </summary>
type ITRef =
    abstract Id: int64
    abstract Commit: obj -> unit
    abstract CurrentValue: obj

/// <summary>
/// Represents a transactional reference that can be updated atomically within an <see cref="T:Axial.STM`1" /> transaction.
/// </summary>
/// <typeparam name="T">The type of the value stored in the reference.</typeparam>
type TRef<'T>(initialValue: 'T) =
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
type TJournal = Dictionary<int64, obj * ITRef>

type TransactionResult<'T> =
    | Done of 'T
    | Retry

type TContext =
    {
        Journal: TJournal
        Reads: HashSet<int64>
    }

/// <summary>
/// Represents a transactional operation that can be composed, retried, and executed atomically.
/// </summary>
/// <typeparam name="T">The type of the value produced by the operation.</typeparam>
type STM<'T> = STM of (TContext -> TransactionResult<'T>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module TRef =
    /// <summary>Creates a new <see cref="T:Axial.TRef`1" /> with the initial value within an STM transaction.</summary>
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
    let mutable private version = 0L
    let private waiters = ResizeArray<Platform.Signal<unit>>()

    let private snapshot (context: TContext) =
        {
            Journal = TJournal(context.Journal)
            Reads = HashSet<int64>(context.Reads)
        }

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
        let rec run (cancellationToken: CancellationToken) : Execution<'T, 'none> =
            let outcome =
                Platform.lock stmLock (fun () ->
                    let (STM op) = transaction
                    let context = freshContext ()

                    match op context with
                    | Done result ->
                        for KeyValue(_, (v, tref)) in context.Journal do
                            tref.Commit(v)

                        version <- version + 1L
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

                Execution.ofValue result
            | Choice2Of2 signal ->
                Execution.bind (fun () -> run cancellationToken) (Platform.awaitSignal signal cancellationToken)

        Flow(fun _ cancellationToken -> run cancellationToken)
