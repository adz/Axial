namespace FsFlow

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Owns finalizers for resources acquired during provisioning or runtime execution.
/// </summary>
/// <remarks>
/// Scopes aggregate cleanup in reverse registration order, prevent double-disposal, and surface
/// cleanup failures as defects rather than typed business errors.
/// </remarks>
type Scope() =
    let gate = obj()
    let finalizers = ResizeArray<CancellationToken -> Task>()
    let mutable closed = false

    /// <summary>Registers an asynchronous finalizer to run when the scope closes.</summary>
    /// <param name="finalizer">The finalizer to run during scope cleanup.</param>
    member _.AddFinalizer(finalizer: CancellationToken -> Task) =
        if isNull (box finalizer) then
            nullArg (nameof finalizer)

        lock gate (fun () ->
            if closed then
                raise (ObjectDisposedException(nameof Scope))
            else
                finalizers.Add finalizer)

    /// <summary>Registers a disposable resource for synchronous cleanup.</summary>
    /// <param name="resource">The disposable resource to close with the scope.</param>
    member this.AddDisposable(resource: IDisposable) =
        if isNull (box resource) then
            nullArg (nameof resource)

        this.AddFinalizer(fun _ ->
            resource.Dispose()
            Task.CompletedTask)

    /// <summary>Registers an asynchronously disposable resource for cleanup.</summary>
    /// <param name="resource">The asynchronous disposable resource to close with the scope.</param>
    member this.AddAsyncDisposable(resource: IAsyncDisposable) =
        if isNull (box resource) then
            nullArg (nameof resource)

        this.AddFinalizer(fun _ -> resource.DisposeAsync().AsTask())

    /// <summary>Creates a child scope whose cleanup is owned by this scope.</summary>
    /// <returns>A child scope that is closed when this scope closes.</returns>
    /// <remarks>
    /// Child scopes make parallel acquisition deterministic: each branch can register its own
    /// finalizers, while the parent decides the fixed order in which branch scopes are closed.
    /// </remarks>
    member this.AddChild() =
        let child = new Scope()

        this.AddFinalizer(fun cancellationToken -> child.Close(cancellationToken))

        child

    /// <summary>Closes the scope and runs all registered finalizers in reverse order.</summary>
    /// <param name="cancellationToken">The token passed to registered finalizers.</param>
    /// <returns>A task that completes when all finalizers have run.</returns>
    member _.Close(cancellationToken: CancellationToken) : Task =
        task {
            let snapshot =
                lock gate (fun () ->
                    if closed then
                        [||]
                    else
                        closed <- true
                        finalizers.ToArray())

            let errors = ResizeArray<exn>()

            for index = snapshot.Length - 1 downto 0 do
                try
                    do! snapshot[index] cancellationToken
                with error ->
                    errors.Add error

            match errors.Count with
            | 0 -> ()
            | 1 -> raise errors[0]
            | _ -> raise (AggregateException("One or more scope finalizers failed.", errors))
        }

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            ValueTask(this.Close(CancellationToken.None))

    interface IDisposable with
        member this.Dispose() =
            this.Close(CancellationToken.None).GetAwaiter().GetResult()
