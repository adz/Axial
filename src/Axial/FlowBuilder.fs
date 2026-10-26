namespace Axial

open System
open System.ComponentModel

[<EditorBrowsable(EditorBrowsableState.Never)>]
module internal FlowBuilderRuntime =
    let run environment cancellationToken source =
        FlowInternal.invoke source environment cancellationToken

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
        Flow(fun environment cancellationToken ->
            try
                FlowBuilderRuntime.run environment cancellationToken flow
            with error ->
                FlowBuilderRuntime.run environment cancellationToken (handler error))

    member _.TryFinally(flow: Flow<'env, 'error, 'value>, compensation: unit -> unit) : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            FlowBuilderRuntime.run environment cancellationToken flow
            |> Execution.mapBoth
                (fun value -> compensation (); value)
                (fun cause -> compensation (); cause))

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> Flow<'env, 'error, 'value>
        ) : Flow<'env, 'error, 'value>
        when 'resource :> IDisposable =
        this.TryFinally(
            binder resource,
            fun () ->
                if not (isNull (box resource)) then
                    resource.Dispose()
        )

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
