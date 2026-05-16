namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks

module private FlowBuilderRuntime =
    let inline run environment cancellationToken (Flow operation) =
        operation environment cancellationToken

    let inline fromResult<'env, 'error, 'value> (result: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> EffectFlow.ofResult result)

    let inline fromAsync<'env, 'error, 'value> (operation: Async<'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            #if FABLE_COMPILER
            async {
                let! value = operation
                return Exit.Success value
            }
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    let! value = Async.StartAsTask(operation, cancellationToken = cancellationToken)
                    return Exit.Success value
                })
            #endif
        )

    let inline fromAsyncResult<'env, 'error, 'value>
        (operation: Async<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            #if FABLE_COMPILER
            async {
                let! result = operation
                return Exit.fromResult result
            }
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    let! result = Async.StartAsTask(operation, cancellationToken = cancellationToken)
                    return Exit.fromResult result
                })
            #endif
        )

#if !FABLE_COMPILER
    let inline fromTask<'env, 'error, 'value> (operation: Task<'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        let! value = operation
                        return Exit.Success value
                })
        )

    let inline fromTaskResult<'env, 'error, 'value>
        (operation: Task<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        let! result = operation
                        return Exit.fromResult result
                })
        )

    let inline fromTaskUnit<'env, 'error> (operation: Task) : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<unit, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        do! operation
                        return Exit.Success ()
                })
        )

    let inline fromValueTask<'env, 'error, 'value> (operation: ValueTask<'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        let! value = operation
                        return Exit.Success value
                })
        )

    let inline fromValueTaskResult<'env, 'error, 'value>
        (operation: ValueTask<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        let! result = operation
                        return Exit.fromResult result
                })
        )

    let inline fromValueTaskUnit<'env, 'error> (operation: ValueTask) : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<unit, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        do! operation
                        return Exit.Success ()
                })
        )
#endif

type FlowBuilder() =
    member _.Return(value: 'value) : Flow<'env, 'error, 'value> =
        Flow.ok value

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(operation: Async<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromAsync operation

    member _.ReturnFrom(operation: Async<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromAsyncResult operation

#if !FABLE_COMPILER
    member _.ReturnFrom(operation: Task<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromTask operation

    member _.ReturnFrom(operation: Task<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromTaskResult operation

    member _.ReturnFrom(operation: Task) : Flow<'env, 'error, unit> =
        FlowBuilderRuntime.fromTaskUnit operation

    member _.ReturnFrom(operation: ValueTask<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromValueTask operation

    member _.ReturnFrom(operation: ValueTask<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromValueTaskResult operation

    member _.ReturnFrom(operation: ValueTask) : Flow<'env, 'error, unit> =
        FlowBuilderRuntime.fromValueTaskUnit operation
#endif

    member _.ReturnFrom(result: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromResult result

    member _.ReturnFrom(option: 'value option) : Flow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResult
        |> FlowBuilderRuntime.fromResult

    member _.ReturnFrom(option: 'value voption) : Flow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> FlowBuilderRuntime.fromResult

    member _.Zero() : Flow<'env, 'error, unit> =
        Flow.ok ()

    member _.Bind
        (
            flow: Flow<'env, 'error, 'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        Flow.bind binder flow

    member _.Bind
        (
            operation: Async<'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromAsync
        |> Flow.bind binder

    member _.Bind
        (
            operation: Async<Result<'value, 'error>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromAsyncResult
        |> Flow.bind binder

#if !FABLE_COMPILER
    member _.Bind
        (
            operation: Task<'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromTask
        |> Flow.bind binder

    member _.Bind
        (
            operation: Task<Result<'value, 'error>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromTaskResult
        |> Flow.bind binder

    member _.Bind
        (
            operation: Task,
            binder: unit -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromTaskUnit
        |> Flow.bind binder

    member _.Bind
        (
            operation: ValueTask<'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromValueTask
        |> Flow.bind binder

    member _.Bind
        (
            operation: ValueTask<Result<'value, 'error>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromValueTaskResult
        |> Flow.bind binder

    member _.Bind
        (
            operation: ValueTask,
            binder: unit -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromValueTaskUnit
        |> Flow.bind binder
#endif

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        result
        |> FlowBuilderRuntime.fromResult
        |> Flow.bind binder

    member _.Bind
        (
            option: 'value option,
            binder: 'value -> Flow<'env, unit, 'next>
        ) : Flow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResult
        |> FlowBuilderRuntime.fromResult
        |> Flow.bind binder

    member _.Bind
        (
            option: 'value voption,
            binder: 'value -> Flow<'env, unit, 'next>
        ) : Flow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> FlowBuilderRuntime.fromResult
        |> Flow.bind binder

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
            |> EffectFlow.mapBoth
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
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> Flow<'env, 'error, unit>
        ) : Flow<'env, 'error, unit> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

#if !FABLE_COMPILER
/// <summary>
/// Computation expression builder for internal async compatibility helpers.
/// </summary>
/// <exclude/>
type internal AsyncAdapterBuilder() =
    member _.Return(value: 'value) : AsyncAdapterFlow<'env, 'error, 'value> =
        AsyncAdapter.ok value

    member _.ReturnFrom(flow: AsyncAdapterFlow<'env, 'error, 'value>) : AsyncAdapterFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(operation: Async<'value>) : AsyncAdapterFlow<'env, 'error, 'value> =
        AsyncAdapter.fromAsync operation

    member _.ReturnFrom(operation: Async<Result<'value, 'error>>) : AsyncAdapterFlow<'env, 'error, 'value> =
        AsyncAdapter.fromAsyncResult operation

    member _.ReturnFrom(operation: Task<Result<'value, 'error>>) : AsyncAdapterFlow<'env, 'error, 'value> =
        operation
        |> Async.AwaitTask
        |> AsyncAdapter.fromAsyncResult

    member _.ReturnFrom(operation: ValueTask<Result<'value, 'error>>) : AsyncAdapterFlow<'env, 'error, 'value> =
        operation.AsTask()
        |> Async.AwaitTask
        |> AsyncAdapter.fromAsyncResult

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : AsyncAdapterFlow<'env, 'error, 'value> =
        AsyncAdapter.fromFlow flow

    member _.ReturnFrom(result: Result<'value, 'error>) : AsyncAdapterFlow<'env, 'error, 'value> =
        AsyncAdapter.fromResult result

    member _.ReturnFrom(option: 'value option) : AsyncAdapterFlow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResult
        |> AsyncAdapter.fromResult

    member _.ReturnFrom(option: 'value voption) : AsyncAdapterFlow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> AsyncAdapter.fromResult

    member _.Zero() : AsyncAdapterFlow<'env, 'error, unit> =
        AsyncAdapter.ok ()

    member _.Bind
        (
            flow: AsyncAdapterFlow<'env, 'error, 'value>,
            binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
        ) : AsyncAdapterFlow<'env, 'error, 'next> =
        AsyncAdapter.bind binder flow

    member _.Bind
        (
            flow: Flow<'env, 'error, 'value>,
            binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
        ) : AsyncAdapterFlow<'env, 'error, 'next> =
        flow
        |> AsyncAdapter.fromFlow
        |> AsyncAdapter.bind binder

    member _.Bind
        (
            operation: Async<'value>,
            binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
        ) : AsyncAdapterFlow<'env, 'error, 'next> =
        operation
        |> AsyncAdapter.fromAsync
        |> AsyncAdapter.bind binder

    member _.Bind
        (
            operation: Async<Result<'value, 'error>>,
            binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
        ) : AsyncAdapterFlow<'env, 'error, 'next> =
        operation
        |> AsyncAdapter.fromAsyncResult
        |> AsyncAdapter.bind binder

    member _.Bind
        (
            operation: Task<Result<'value, 'error>>,
            binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
        ) : AsyncAdapterFlow<'env, 'error, 'next> =
        operation
        |> Async.AwaitTask
        |> AsyncAdapter.fromAsyncResult
        |> AsyncAdapter.bind binder

    member _.Bind
        (
            operation: ValueTask<Result<'value, 'error>>,
            binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
        ) : AsyncAdapterFlow<'env, 'error, 'next> =
        operation.AsTask()
        |> Async.AwaitTask
        |> AsyncAdapter.fromAsyncResult
        |> AsyncAdapter.bind binder

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
        ) : AsyncAdapterFlow<'env, 'error, 'next> =
        result
        |> AsyncAdapter.fromResult
        |> AsyncAdapter.bind binder

    member _.Bind
        (
            option: 'value option,
            binder: 'value -> AsyncAdapterFlow<'env, unit, 'next>
        ) : AsyncAdapterFlow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResult
        |> AsyncAdapter.fromResult
        |> AsyncAdapter.bind binder

    member _.Bind
        (
            option: 'value voption,
            binder: 'value -> AsyncAdapterFlow<'env, unit, 'next>
        ) : AsyncAdapterFlow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> AsyncAdapter.fromResult
        |> AsyncAdapter.bind binder

    member _.Delay(factory: unit -> AsyncAdapterFlow<'env, 'error, 'value>) : AsyncAdapterFlow<'env, 'error, 'value> =
        AsyncAdapter.delay factory

    member _.Run(flow: AsyncAdapterFlow<'env, 'error, 'value>) : AsyncAdapterFlow<'env, 'error, 'value> =
        flow

    member _.Combine
        (
            first: AsyncAdapterFlow<'env, 'error, unit>,
            second: AsyncAdapterFlow<'env, 'error, 'value>
        ) : AsyncAdapterFlow<'env, 'error, 'value> =
        first
        |> AsyncAdapter.bind (fun () -> second)

    member _.TryWith
        (
            flow: AsyncAdapterFlow<'env, 'error, 'value>,
            handler: exn -> AsyncAdapterFlow<'env, 'error, 'value>
        ) : AsyncAdapterFlow<'env, 'error, 'value> =
        AsyncAdapterFlow(fun environment ->
            async {
                try
                    return! AsyncAdapter.run environment flow
                with error ->
                    return! AsyncAdapter.run environment (handler error)
            })

    member _.TryFinally
        (
            flow: AsyncAdapterFlow<'env, 'error, 'value>,
            compensation: unit -> unit
        ) : AsyncAdapterFlow<'env, 'error, 'value> =
        AsyncAdapterFlow(fun environment ->
            async {
                try
                    return! AsyncAdapter.run environment flow
                finally
                    compensation ()
            })

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> AsyncAdapterFlow<'env, 'error, 'value>
        ) : AsyncAdapterFlow<'env, 'error, 'value>
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
            body: AsyncAdapterFlow<'env, 'error, unit>
        ) : AsyncAdapterFlow<'env, 'error, unit> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> AsyncAdapterFlow<'env, 'error, unit>
        ) : AsyncAdapterFlow<'env, 'error, unit> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )
#endif
