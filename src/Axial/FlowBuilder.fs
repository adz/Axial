namespace Axial

open System
open System.ComponentModel

[<EditorBrowsable(EditorBrowsableState.Never)>]
module internal FlowBuilderRuntime =
    let run environment cancellationToken source =
        FlowInternal.invoke source environment cancellationToken

    // Rethrows an exception caught earlier, keeping its original stack trace where the platform can.
    let rethrow (error: exn) : 'value =
#if FABLE_COMPILER
        raise error
#else
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw()
        Unchecked.defaultof<'value>
#endif

    let fromResult<'env, 'error, 'value> (result: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofResult result)

    let fromAsync<'env, 'error, 'value> (operation: Async<'value>) : Flow<'env, 'error, 'value> =
        AsyncInterop.from Exit.Success operation

    let fromAsyncResult<'env, 'error, 'value>
        (operation: Async<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        AsyncInterop.from Exit.fromResult operation

#if !FABLE_COMPILER
    let fromColdTask<'env, 'error, 'value> (ColdTask operation: ColdTask<'value>) : Flow<'env, 'error, 'value> =
        TaskInterop.from Exit.Success operation

    let fromColdTaskResult<'env, 'error, 'value>
        (ColdTask operation: ColdTask<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        TaskInterop.from Exit.fromResult operation
#endif

/// <exclude/>
[<EditorBrowsable(EditorBrowsableState.Never)>]
type FlowBindable =
    | FlowBindable

    static member inline Convert(_: FlowBindable, source: Flow<'env, 'error, 'value>) = source

    static member inline Convert(_: FlowBindable, source: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow.fromResult source

    static member inline Convert(_: FlowBindable, source: 'value option) : Flow<'env, unit, 'value> =
        Flow.fromOption () source

    static member inline Convert(_: FlowBindable, source: 'value voption) : Flow<'env, unit, 'value> =
        Flow.fromValueOption () source

    static member inline Convert(_: FlowBindable, source: BindError<'env, 'error, 'value>) =
        Bind.toFlow source

    static member inline Convert(_: FlowBindable, source: Async<'value>) : Flow<'env, 'error, 'value> =
        Flow.fromAsync source

    static member inline Convert(_: FlowBindable, source: Async<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        Flow.fromAsyncResult source

#if !FABLE_COMPILER
    static member inline Convert(_: FlowBindable, source: ColdTask<'value>) : Flow<'env, 'error, 'value> =
        Flow.fromColdTask source

    static member inline Convert(_: FlowBindable, source: ColdTask<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        Flow.fromColdTaskResult source
#endif

    static member inline Convert(_: FlowBindable, source: seq<'value>) : seq<'value> = source
    static member inline Convert(_: FlowBindable, source: 'value list) : seq<'value> = source
    static member inline Convert(_: FlowBindable, source: 'value array) : seq<'value> = source

/// <exclude/>
[<EditorBrowsable(EditorBrowsableState.Never)>]
module FlowBuilderSource =
    let inline toFlow (witness: ^witness) (source: ^source) : ^result =
        ((^witness or ^source) : (static member Convert: ^witness * ^source -> ^result) (witness, source))

type FlowBuilder() =
    member inline _.Source(source) =
        FlowBuilderSource.toFlow FlowBindable source

    member _.Return(value: 'value) : Flow<'env, 'error, 'value> =
        Flow.ok value

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    member _.Zero() : Flow<'env, 'error, unit> =
        Flow.ok ()

    member _.Bind
        (
            flow: Flow<'env, 'error, 'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        Flow.bind binder flow

    member _.Delay(factory: unit -> Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow.delay factory

    member _.Run(flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    member _.Combine
        (
            first: Flow<'env, 'error, unit>,
            second: Flow<'env, 'error, 'value>
        ) : Flow<'env, 'error, 'value> =
        first
        |> Flow.bind (fun () -> second)

    member _.TryWith
        (
            flow: Flow<'env, 'error, 'value>,
            handler: exn -> Flow<'env, 'error, 'value>
        ) : Flow<'env, 'error, 'value> =
        // The handler sees an exception the body throws while it is invoked and one its execution faults with
        // after an async hop, as `try/with` around `return!` in a task would. Typed failures and `Cause.Die`
        // values are outcomes, not exceptions, and pass through. A completed body allocates nothing extra.
        Flow(fun environment cancellationToken ->
            let mutable thrown: exn = null

            let body =
                try
                    FlowBuilderRuntime.run environment cancellationToken flow
                with error ->
                    thrown <- error
                    Unchecked.defaultof<Execution<'value, 'error>>

            if not (isNull thrown) then
                FlowBuilderRuntime.run environment cancellationToken (handler thrown)
            else
#if FABLE_COMPILER
                Platform.tryExecution
                    (fun () -> body)
                    (fun error -> FlowBuilderRuntime.run environment cancellationToken (handler error))
#else
                if body.IsCompletedSuccessfully then
                    body
                else
                    Platform.tryExecution
                        (fun () -> body)
                        (fun error -> FlowBuilderRuntime.run environment cancellationToken (handler error))
#endif
        )

    member _.TryFinally(flow: Flow<'env, 'error, 'value>, compensation: unit -> unit) : Flow<'env, 'error, 'value> =
        // The compensation runs exactly once however the body ends: with a value, a cause, a synchronous throw, or
        // a faulted execution. A thrown exception is rethrown after it, so an enclosing `try/with` still sees it.
        Flow(fun environment cancellationToken ->
            let mutable thrown: exn = null

            let body =
                try
                    FlowBuilderRuntime.run environment cancellationToken flow
                with error ->
                    thrown <- error
                    Unchecked.defaultof<Execution<'value, 'error>>

            if not (isNull thrown) then
                compensation ()
                FlowBuilderRuntime.rethrow thrown
            else
#if FABLE_COMPILER
                Platform.tryExecution (fun () -> body) (fun error ->
                    compensation ()
                    FlowBuilderRuntime.rethrow error)
                |> Execution.mapBoth
                    (fun value -> compensation (); value)
                    (fun cause -> compensation (); cause))
#else
                // One state machine, as before: the compensation runs after the outcome, or before a rethrow.
                Platform.execution {
                    let mutable exit = Unchecked.defaultof<Exit<'value, 'error>>

                    try
                        let! settled = body
                        exit <- settled
                    with error ->
                        compensation ()
                        FlowBuilderRuntime.rethrow error

                    compensation ()
                    return exit
                }
                |> Platform.ofAwaitable)
#endif

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> Flow<'env, 'error, 'value>
        ) : Flow<'env, 'error, 'value>
        when 'resource :> IDisposable =
        let dispose () =
            if not (isNull (box resource)) then
                resource.Dispose()

        // The binder builds the body eagerly, so a body that throws before its first await throws here.
        let body =
            try
                binder resource
            with _ ->
                dispose ()
                reraise ()

        this.TryFinally(body, dispose)

    member this.While
        (
            guard: unit -> bool,
            body: Flow<'env, 'error, unit>
        ) : Flow<'env, 'error, unit> =
        // A loop, not recursion through Bind: a `while` that suspends each iteration (a queue consumer, a polling
        // loop) would otherwise retain every iteration until it ends.
        Flow(fun environment cancellationToken ->
            Platform.whileLoop guard (fun () -> FlowBuilderRuntime.run environment cancellationToken body))

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> Flow<'env, 'error, unit>
        ) : Flow<'env, 'error, unit> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )
