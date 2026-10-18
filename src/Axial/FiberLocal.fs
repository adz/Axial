namespace Axial

open System.Threading

/// A value scoped to the current fiber and inherited by fibers forked within its scope.
type FiberLocal<'a> = internal { Id: int64; Initial: 'a; Name: string option }

[<RequireQualifiedAccess>]
module FiberLocal =
    let mutable private nextId = 0L

    let private freshId () =
#if FABLE_COMPILER
        nextId <- nextId + 1L
        nextId
#else
        Interlocked.Increment(&nextId)
#endif

    let make (initial: 'a) : FiberLocal<'a> =
        { Id = freshId (); Initial = initial; Name = None }

    let named (name: string) (initial: 'a) : FiberLocal<'a> =
        { Id = freshId (); Initial = initial; Name = Some name }

    let get (local: FiberLocal<'a>) : Flow<'env, 'error, 'a> =
        Flow.delay (fun () ->
            let runtime = RuntimeState.current ()
            match Map.tryFind local.Id runtime.Locals with
            | Some value -> Flow.ok (unbox<'a> value)
            | None -> Flow.ok local.Initial)

    /// ZIO recommends scoped locally overrides instead of a mutable set.
    let locally (value: 'a) (local: FiberLocal<'a>) (inner: Flow<'env, 'error, 'b>) : Flow<'env, 'error, 'b> =
        Flow(fun environment cancellationToken ->
            let currentRuntime = RuntimeState.current ()
            let runtime = { currentRuntime with Locals = Map.add local.Id (box value) currentRuntime.Locals }
            RuntimeState.withRuntime runtime (fun () -> FlowInternal.invoke inner environment cancellationToken))
