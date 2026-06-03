namespace FsFlow

#if !FABLE_COMPILER

open System
open System.Threading
open System.Threading.Tasks

/// <summary>
/// A one-shot, typed handoff point that can be completed exactly once with a full <see cref="T:FsFlow.Exit`2" />.
/// </summary>
/// <remarks>
/// Use <c>Deferred</c> when fibers need to coordinate through FsFlow outcomes rather than raw
/// <see cref="T:System.Threading.Tasks.TaskCompletionSource`1" /> values. Completion functions are idempotent and
/// return <c>true</c> only to the caller that won the completion race.
/// </remarks>
/// <typeparam name="error">The typed failure channel of the deferred outcome.</typeparam>
/// <typeparam name="value">The success value of the deferred outcome.</typeparam>
type Deferred<'error, 'value> =
    private
    | Deferred of TaskCompletionSource<Exit<'value, 'error>>

/// <summary>Flow-native helpers for one-shot typed coordination.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Deferred =
    let private newSource<'value, 'error> () =
        TaskCompletionSource<Exit<'value, 'error>>(TaskCreationOptions.RunContinuationsAsynchronously)

    let private awaitTask
        (deferredTask: Task<Exit<'value, 'error>>)
        (cancellationToken: CancellationToken)
        : Task<Exit<'value, 'error>> =
        task {
            if cancellationToken.IsCancellationRequested then
                return Exit.Failure Cause.Interrupt
            else
                let cancellation = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                use _registration =
                    cancellationToken.Register(fun () -> cancellation.TrySetResult() |> ignore)

                let! completed = Task.WhenAny(deferredTask :> Task, cancellation.Task :> Task)

                if obj.ReferenceEquals(completed, deferredTask :> Task) then
                    return deferredTask.Result
                else
                    return Exit.Failure Cause.Interrupt
        }

    /// <summary>Creates an empty deferred value.</summary>
    let make<'env, 'error, 'value> () : Flow<'env, 'error, Deferred<'error, 'value>> =
        Flow.ok (Deferred(newSource()))

    /// <summary>Waits for the deferred outcome, preserving success, typed failure, defect, or interruption.</summary>
    let await (Deferred source: Deferred<'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, 'error>>(awaitTask source.Task cancellationToken))

    /// <summary>Attempts to complete the deferred value with a full outcome.</summary>
    let complete
        (exit: Exit<'value, 'error>)
        (Deferred source: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        Flow.read (fun _ -> source.TrySetResult exit)

    /// <summary>Attempts to complete the deferred value successfully.</summary>
    let succeed
        (value: 'value)
        (deferred: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        complete (Exit.Success value) deferred

    /// <summary>Attempts to complete the deferred value with a typed failure.</summary>
    let fail
        (error: 'error)
        (deferred: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        complete (Exit.Failure(Cause.Fail error)) deferred

    /// <summary>Attempts to complete the deferred value with a defect.</summary>
    let die
        (error: exn)
        (deferred: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        complete (Exit.Failure(Cause.Die error)) deferred

    /// <summary>Attempts to complete the deferred value as interrupted.</summary>
    let interrupt (deferred: Deferred<'error, 'value>) : Flow<'env, 'workflowError, bool> =
        complete (Exit.Failure Cause.Interrupt) deferred

/// <summary>A Flow-native semaphore handle used to limit concurrent workflow sections.</summary>
type FlowSemaphore =
    private
    | FlowSemaphore of SemaphoreSlim

/// <summary>Flow-native semaphore helpers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Semaphore =
    /// <summary>Creates a semaphore with the supplied initial permit count.</summary>
    let make (permits: int) : Flow<'env, 'error, FlowSemaphore> =
        Flow(fun _ _ ->
            if permits <= 0 then
                EffectFlow.ofDie (ArgumentOutOfRangeException(nameof permits, "Permit count must be positive."))
            else
                EffectFlow.ofValue (FlowSemaphore(new SemaphoreSlim(permits, permits))))

    /// <summary>Alias for <c>make</c>.</summary>
    let create (permits: int) : Flow<'env, 'error, FlowSemaphore> =
        make permits

    /// <summary>Runs a workflow while holding one permit and always releases the permit afterward.</summary>
    let withPermit
        (FlowSemaphore semaphore)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    let! acquired =
                        task {
                            try
                                do! semaphore.WaitAsync(cancellationToken)
                                return true
                            with
                            | :? OperationCanceledException ->
                                return false
                        }

                    if not acquired then
                        return Exit.Failure Cause.Interrupt
                    else
                        try
                            return! FlowInternal.invoke flow environment cancellationToken |> _.AsTask()
                        finally
                            semaphore.Release() |> ignore
                }))

#endif
