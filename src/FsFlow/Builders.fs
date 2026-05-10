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
            Promise.map Exit.Success (Async.StartAsPromise(operation, cancellationToken = cancellationToken))
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
            Promise.map Exit.fromResult (Async.StartAsPromise(operation, cancellationToken = cancellationToken))
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    let! result = Async.StartAsTask(operation, cancellationToken = cancellationToken)
                    return Exit.fromResult result
                })
            #endif
        )

    let inline fromTask<'env, 'error, 'value> (operation: Task<'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            #if FABLE_COMPILER
            Promise.map Exit.Success (JS.Constructors.Promise.Create(fun resolve reject ->
                operation.ContinueWith(fun (t: Task<'value>) ->
                    if t.IsCanceled then reject(OperationCanceledException(cancellationToken))
                    elif t.IsFaulted then reject(t.Exception)
                    else resolve(t.Result)) |> ignore))
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        let! value = operation
                        return Exit.Success value
                })
            #endif
        )

    let inline fromTaskResult<'env, 'error, 'value>
        (operation: Task<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            #if FABLE_COMPILER
            Promise.map Exit.fromResult (JS.Constructors.Promise.Create(fun resolve reject ->
                operation.ContinueWith(fun (t: Task<Result<'value, 'error>>) ->
                    if t.IsCanceled then reject(OperationCanceledException(cancellationToken))
                    elif t.IsFaulted then reject(t.Exception)
                    else resolve(t.Result)) |> ignore))
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        let! result = operation
                        return Exit.fromResult result
                })
            #endif
        )

    let inline fromTaskUnit<'env, 'error> (operation: Task) : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken ->
            #if FABLE_COMPILER
            Promise.map (fun () -> Exit.Success ()) (JS.Constructors.Promise.Create(fun resolve reject ->
                operation.ContinueWith(fun (t: Task) ->
                    if t.IsCanceled then reject(OperationCanceledException(cancellationToken))
                    elif t.IsFaulted then reject(t.Exception)
                    else resolve()) |> ignore))
            #else
            ValueTask<Exit<unit, 'error>>(
                task {
                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure Cause.Interrupt
                    else
                        do! operation
                        return Exit.Success ()
                })
            #endif
        )

type FlowBuilder() =
    member _.Return(value: 'value) : Flow<'env, 'error, 'value> =
        Flow.ok value

    member _.Yield(value: obj) : Flow<'env, 'error, 'value> =
        Flow.ok (unbox<'value> value)

    member _.Yield(project: 'env -> 'value) : Flow<'env, 'error, 'value> =
        Flow.read project

    member _.YieldFrom(flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    member _.YieldFrom(operation: Async<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromAsync operation

    member _.YieldFrom(operation: Async<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromAsyncResult operation

    member _.YieldFrom(operation: Task<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromTask operation

    member _.YieldFrom(operation: Task<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromTaskResult operation

    member _.YieldFrom(operation: Task) : Flow<'env, 'error, unit> =
        FlowBuilderRuntime.fromTaskUnit operation

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(operation: Async<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromAsync operation

    member _.ReturnFrom(operation: Async<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromAsyncResult operation

    member _.ReturnFrom(operation: Task<'value>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromTask operation

    member _.ReturnFrom(operation: Task<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        FlowBuilderRuntime.fromTaskResult operation

    member _.ReturnFrom(operation: Task) : Flow<'env, 'error, unit> =
        FlowBuilderRuntime.fromTaskUnit operation

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
            _request: Env<'dep>,
            binder: 'dep -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            binder dependency
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            _request: Env<'dep>,
            binder: unit -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let _dependency = (environment :> Needs<'dep>).Dep
            binder ()
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, 'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            binder (project dependency)
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, Flow<'env, 'error, 'value>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, Async<'value>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> FlowBuilderRuntime.fromAsync
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, Async<Result<'value, 'error>>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> FlowBuilderRuntime.fromAsyncResult
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, Task<'value>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> FlowBuilderRuntime.fromTask
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, Task<Result<'value, 'error>>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> FlowBuilderRuntime.fromTaskResult
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, Task>,
            binder: unit -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> FlowBuilderRuntime.fromTaskUnit
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, Result<'value, 'error>>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> FlowBuilderRuntime.fromResult
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, 'value option>,
            binder: 'value -> Flow<'env, unit, 'next>
        ) : Flow<'env, unit, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> OptionFlow.toUnitResult
            |> FlowBuilderRuntime.fromResult
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

    member _.Bind
        (
            request: Env<'dep, 'value voption>,
            binder: 'value -> Flow<'env, unit, 'next>
        ) : Flow<'env, unit, 'next>
        when 'env :> Needs<'dep> =
        Flow(fun environment cancellationToken ->
            let dependency = (environment :> Needs<'dep>).Dep
            let (Env project) = request

            project dependency
            |> OptionFlow.toUnitResultValueOption
            |> FlowBuilderRuntime.fromResult
            |> Flow.bind binder
            |> FlowBuilderRuntime.run environment cancellationToken)

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
            try
                FlowBuilderRuntime.run environment cancellationToken flow
            finally
                compensation ())

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

/// <summary>
/// Computation expression builder for internal async compatibility helpers.
/// </summary>
/// <exclude/>
type internal AsyncFlowBuilder() =
    member _.Return(value: 'value) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.ok value

    member _.Yield(value: obj) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.ok (unbox<'value> value)

    member _.Yield(project: 'env -> 'value) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.read project

    member _.YieldFrom(flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(operation: Async<'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.fromAsync operation

    member _.ReturnFrom(operation: Async<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.fromAsyncResult operation

    member _.ReturnFrom(operation: Task<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value> =
        operation
        |> Async.AwaitTask
        |> AsyncFlow.fromAsyncResult

    member _.ReturnFrom(operation: ValueTask<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value> =
        operation.AsTask()
        |> Async.AwaitTask
        |> AsyncFlow.fromAsyncResult

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.fromFlow flow

    member _.ReturnFrom(result: Result<'value, 'error>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.fromResult result

    member _.ReturnFrom(option: 'value option) : AsyncFlow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResult
        |> AsyncFlow.fromResult

    member _.ReturnFrom(option: 'value voption) : AsyncFlow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> AsyncFlow.fromResult

    member _.Zero() : AsyncFlow<'env, 'error, unit> =
        AsyncFlow.ok ()

    member _.Bind
        (
            flow: AsyncFlow<'env, 'error, 'value>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        AsyncFlow.bind binder flow

    member _.Bind
        (
            _request: Env<'dep>,
            binder: 'dep -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep

                return! AsyncFlow.run environment (binder dependency)
            })

    member _.Bind
        (
            _request: Env<'dep>,
            binder: unit -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let _dependency = (environment :> Needs<'dep>).Dep

                return! AsyncFlow.run environment (binder ())
            })

    member _.Bind
        (
            request: Env<'dep, 'value>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! AsyncFlow.run environment (binder (project dependency))
            })

    member _.Bind
        (
            request: Env<'dep, Flow<'env, 'error, 'value>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! AsyncFlow.run environment (project dependency |> AsyncFlow.fromFlow |> AsyncFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, AsyncFlow<'env, 'error, 'value>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! AsyncFlow.run environment (project dependency |> AsyncFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, Async<'value>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! AsyncFlow.run environment (project dependency |> AsyncFlow.fromAsync |> AsyncFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, Async<Result<'value, 'error>>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! AsyncFlow.run environment (project dependency |> AsyncFlow.fromAsyncResult |> AsyncFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, Result<'value, 'error>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! AsyncFlow.run environment (project dependency |> AsyncFlow.fromResult |> AsyncFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, 'value option>,
            binder: 'value -> AsyncFlow<'env, unit, 'next>
        ) : AsyncFlow<'env, unit, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return!
                    AsyncFlow.run environment (
                        project dependency
                        |> OptionFlow.toUnitResult
                        |> AsyncFlow.fromResult
                        |> AsyncFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, 'value voption>,
            binder: 'value -> AsyncFlow<'env, unit, 'next>
        ) : AsyncFlow<'env, unit, 'next>
        when 'env :> Needs<'dep> =
        AsyncFlow(fun environment ->
            async {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return!
                    AsyncFlow.run environment (
                        project dependency
                        |> OptionFlow.toUnitResultValueOption
                        |> AsyncFlow.fromResult
                        |> AsyncFlow.bind binder)
            })

    member _.Bind
        (
            flow: Flow<'env, 'error, 'value>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        flow
        |> AsyncFlow.fromFlow
        |> AsyncFlow.bind binder

    member _.Bind
        (
            operation: Async<'value>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        operation
        |> AsyncFlow.fromAsync
        |> AsyncFlow.bind binder

    member _.Bind
        (
            operation: Async<Result<'value, 'error>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        operation
        |> AsyncFlow.fromAsyncResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            operation: Task<Result<'value, 'error>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        operation
        |> Async.AwaitTask
        |> AsyncFlow.fromAsyncResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            operation: ValueTask<Result<'value, 'error>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        operation.AsTask()
        |> Async.AwaitTask
        |> AsyncFlow.fromAsyncResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        result
        |> AsyncFlow.fromResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            option: 'value option,
            binder: 'value -> AsyncFlow<'env, unit, 'next>
        ) : AsyncFlow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResult
        |> AsyncFlow.fromResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            option: 'value voption,
            binder: 'value -> AsyncFlow<'env, unit, 'next>
        ) : AsyncFlow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> AsyncFlow.fromResult
        |> AsyncFlow.bind binder

    member _.Delay(factory: unit -> AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.delay factory

    member _.Run(flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        flow

    member _.Combine
        (
            first: AsyncFlow<'env, 'error, unit>,
            second: AsyncFlow<'env, 'error, 'value>
        ) : AsyncFlow<'env, 'error, 'value> =
        first
        |> AsyncFlow.bind (fun () -> second)

    member _.TryWith
        (
            flow: AsyncFlow<'env, 'error, 'value>,
            handler: exn -> AsyncFlow<'env, 'error, 'value>
        ) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                try
                    return! AsyncFlow.run environment flow
                with error ->
                    return! AsyncFlow.run environment (handler error)
            })

    member _.TryFinally
        (
            flow: AsyncFlow<'env, 'error, 'value>,
            compensation: unit -> unit
        ) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                try
                    return! AsyncFlow.run environment flow
                finally
                    compensation ()
            })

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> AsyncFlow<'env, 'error, 'value>
        ) : AsyncFlow<'env, 'error, 'value>
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
            body: AsyncFlow<'env, 'error, unit>
        ) : AsyncFlow<'env, 'error, unit> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> AsyncFlow<'env, 'error, unit>
        ) : AsyncFlow<'env, 'error, unit> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

[<AutoOpen>]
module Builders =
    /// <summary>
    /// The fail-fast <c>result { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this builder when the happy path should short-circuit on the first error
    /// and you want to keep the workflow in <c>Result</c> shape all the way through.
    /// </para>
    /// <para>
    /// It works well for parsing, validation, and other boundaries where failure is expected
    /// to stop the flow immediately instead of accumulating diagnostics.
    /// </para>
    /// <para>
    /// Use <c>Check.orError</c> when a pure check needs a domain error, and <c>Guard.MapError</c> when
    /// you need to remap an existing error before entering the CE.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let parsedUser =
    ///     result {
    ///         let! age = parseAge input
    ///         let! name = parseName input
    ///         return { Age = age; Name = name }
    ///     }
    /// ```
    /// </example>
    let result = ResultBuilder()

    /// <summary>
    /// The universal <c>flow { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this builder when the boundary can mix synchronous values, <c>Async</c>, <c>Task</c>,
    /// <c>Result</c>, and environment requests while keeping typed failures and explicit
    /// dependency access.
    /// </para>
    /// <para>
    /// It preserves the current environment model while allowing the workflow to compose
    /// task-oriented inputs directly, so callers do not need to switch builders just to cross
    /// an async boundary.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let greeting =
    ///     flow {
    ///         let! name = Flow.env
    ///         let! suffix = async { return "!" }
    ///         return $"Hello, {name}{suffix}"
    ///     }
    /// ```
    /// </example>
    let flow = FlowBuilder()

    let internal asyncFlow = AsyncFlowBuilder()

    /// <summary>
    /// The accumulating <c>validate { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this builder when you want to collect all validation failures instead of stopping
    /// at the first one.
    /// </para>
    /// <para>
    /// Use <c>and!</c> when sibling validations should accumulate into the same diagnostics graph.
    /// Plain <c>let!</c> and <c>do!</c> are sequential: if the left side fails, the later step is
    /// not evaluated.
    /// </para>
    /// <para>
    /// `Check<'value>` covers both value-preserving checks and gate checks.
    /// Use <c>Check.orError</c> to attach an application error, and <c>Guard.Of</c> /
    /// <c>Guard.MapError</c> when you want the same error-bound source shape to participate
    /// directly in validation.
    /// </para>
    /// <para>
    /// When nested API response fields need to keep their place in the diagnostics graph, use
    /// the scoped helpers <c>validate.key</c>, <c>validate.index</c>, and <c>validate.name</c>
    /// inside the computation expression. If you already have a <c>Validation</c> value, use
    /// <c>Validation.key</c>, <c>Validation.index</c>, or <c>Validation.name</c> to prefix it
    /// after the fact.
    /// </para>
    /// <para>
    /// It is intended for forms, configuration checks, and other input-heavy boundaries where
    /// the user benefits from seeing every problem at once.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let validatedUser =
    ///     validate {
    ///         let! name = Check.notBlank input.Name
    ///         let! age = Check.okIf (input.Age > 0) "Age must be positive"
    ///         return { Name = name; Age = age }
    ///     }
    /// ```
    ///
    /// ```fsharp
    /// let validatedCustomer =
    ///     validate.key "customer" {
    ///         let! name =
    ///             validate.name "Name" {
    ///                 return! input.Name |> Check.notBlank |> Check.orError "Name required"
    ///             }
    ///
    ///         return name
    ///     }
    /// ```
    /// </example>
    let validate = ValidateBuilder()
