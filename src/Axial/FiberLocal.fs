namespace Axial

/// A value scoped to the current fiber and inherited by fibers forked within its scope.
type FiberLocal<'a> = internal { Id: int64; Initial: 'a; Name: string option }

[<RequireQualifiedAccess>]
module FiberLocal =
    let make (initial: 'a) : FiberLocal<'a> =
        { Id = RuntimeIds.next (); Initial = initial; Name = None }

    let named (name: string) (initial: 'a) : FiberLocal<'a> =
        { Id = RuntimeIds.next (); Initial = initial; Name = Some name }

    /// Reads explicitly scoped named values on the current fiber; unnamed and unset locals are omitted.
    let current () : (string * obj) list =
        let locals = RuntimeState.current().Context.Locals
        if Map.isEmpty locals then []
        else
            locals
            |> Map.toList
            |> List.choose (fun (_, struct (name, value)) -> name |> Option.map (fun name -> name, value))

    let get (local: FiberLocal<'a>) : Flow<'env, 'error, 'a> =
        Flow.delay (fun () ->
            let runtime = RuntimeState.current ()
            match Map.tryFind local.Id runtime.Context.Locals with
            | Some (struct (_, value)) -> Flow.ok (unbox<'a> value)
            | None -> Flow.ok local.Initial)

    /// ZIO recommends scoped locally overrides instead of a mutable set.
    let locally (value: 'a) (local: FiberLocal<'a>) (inner: Flow<'env, 'error, 'b>) : Flow<'env, 'error, 'b> =
        Flow(fun environment cancellationToken ->
            let currentRuntime = RuntimeState.current ()
            let runtime = { currentRuntime with Context = { currentRuntime.Context with Locals = Map.add local.Id (struct (local.Name, box value)) currentRuntime.Context.Locals } }
            RuntimeState.withRuntime runtime (fun () -> FlowInternal.invoke inner environment cancellationToken))
