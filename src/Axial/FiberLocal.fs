namespace Axial

open System.Threading
open System.Collections.Generic

/// A value scoped to the current fiber and inherited by fibers forked within its scope.
type FiberLocal<'a> = internal { Id: int64; Initial: 'a; Name: string option }

[<RequireQualifiedAccess>]
module FiberLocal =
    let mutable private nextId = 0L
    let private names = Dictionary<int64, string>()

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
        let id = freshId ()
        lock names (fun () -> names[id] <- name)
        { Id = id; Initial = initial; Name = Some name }

    /// Reads explicitly scoped named values on the current fiber; unnamed and unset locals are omitted.
    let current () : (string * obj) list =
        let locals = RuntimeState.current().Locals
        if Map.isEmpty locals then []
        else
            lock names (fun () ->
                locals
                |> Map.toList
                |> List.choose (fun (id, value) ->
                    match names.TryGetValue id with
                    | true, name -> Some(name, value)
                    | _ -> None))

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
