namespace Axial.Flow

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.CompilerServices
open System.ComponentModel

[<EditorBrowsable(EditorBrowsableState.Never)>]
module FlowBuilderRuntime =
    let run environment cancellationToken (Flow operation) =
        operation environment cancellationToken

    let fromResult<'env, 'error, 'value> (result: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofResult result)

    let fromAsync<'env, 'error, 'value> (operation: Async<'value>) : Flow<'env, 'error, 'value> =
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

    let fromAsyncResult<'env, 'error, 'value>
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
    let fromColdTask<'env, 'error, 'value> (ColdTask operation: ColdTask<'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        let! value = operation cancellationToken
                        return Exit.Success value
                })
        )

    let fromTask<'env, 'error, 'value> (operation: Task<'value>) : Flow<'env, 'error, 'value> =
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

    let fromTaskResult<'env, 'error, 'value>
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

    let fromTaskUnit<'env, 'error> (operation: Task) : Flow<'env, 'error, unit> =
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

    let fromValueTask<'env, 'error, 'value> (operation: ValueTask<'value>) : Flow<'env, 'error, 'value> =
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

    let fromValueTaskResult<'env, 'error, 'value>
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

    let fromValueTaskUnit<'env, 'error> (operation: ValueTask) : Flow<'env, 'error, unit> =
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

    member _.ReturnFrom(source: BindError<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Bind.toFlow source

    member _.ReturnFrom(operation: Async<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromAsync operation

    member _.ReturnFrom(operation: Async<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromAsyncResult operation

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

#if !FABLE_COMPILER
    member _.ReturnFrom(operation: ColdTask<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromColdTask operation

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
            source: BindError<'env, 'error, 'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        source
        |> Bind.toFlow
        |> Flow.bind binder

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

#if !FABLE_COMPILER
    member _.Bind
        (
            operation: ColdTask<'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        operation
        |> FlowBuilderRuntime.fromColdTask
        |> Flow.bind binder

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
