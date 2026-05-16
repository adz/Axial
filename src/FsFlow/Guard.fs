namespace FsFlow

open System.Threading.Tasks

#if !FABLE_COMPILER
module private GuardFlow =
    let inline fromAsyncAdapter
        (flow: AsyncAdapterFlow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    let! exit =
                        Async.StartAsTask(
                            AsyncAdapter.run environment flow,
                            cancellationToken = cancellationToken)

                    return exit
                }))

    let inline fromTaskAdapter
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    let! exit = TaskAdapter.run environment cancellationToken flow
                    return exit
                }))
#endif

/// <summary>
/// Constructors for turning predicate-like and error-bearing sources into bindable results,
/// validations, and flows.
/// </summary>
type Guard private () =
    static member Of(error: 'error, result: Result<'value, unit>) : Result<'value, 'error> =
        Check.orError error result

    static member Of(error: 'error, validation: Validation<'value, unit>) : Validation<'value, 'error> =
        Validation.mapError (fun () -> error) validation

    static member Of(error: 'error, value: bool) : Result<unit, 'error> =
        if value then Ok () else Error error

    static member Of(error: 'error, value: 'value option) : Result<'value, 'error> =
        OptionFlow.toResult error value

    static member Of(error: 'error, value: 'value voption) : Result<'value, 'error> =
        OptionFlow.toResultValueOption error value

#if !FABLE_COMPILER
    static member Of(error: 'error, result: Async<Result<'value, unit>>) : Async<Result<'value, 'error>> =
        async {
            let! outcome = result
            return Check.orError error outcome
        }

    static member Of(error: 'error, value: Async<bool>) : Async<Result<unit, 'error>> =
        async {
            let! outcome = value
            return if outcome then Ok () else Error error
        }

    static member Of(error: 'error, value: Async<'value option>) : Async<Result<'value, 'error>> =
        async {
            let! outcome = value
            return OptionFlow.toResult error outcome
        }

    static member Of(error: 'error, value: Async<'value voption>) : Async<Result<'value, 'error>> =
        async {
            let! outcome = value
            return OptionFlow.toResultValueOption error outcome
        }

    static member Of(error: 'error, result: Task<Result<'value, unit>>) : Task<Result<'value, 'error>> =
        task {
            let! outcome = result
            return Check.orError error outcome
        }

    static member Of(error: 'error, value: Task<bool>) : Task<Result<unit, 'error>> =
        task {
            let! outcome = value
            return if outcome then Ok () else Error error
        }

    static member Of(error: 'error, value: Task<'value option>) : Task<Result<'value, 'error>> =
        task {
            let! outcome = value
            return OptionFlow.toResult error outcome
        }

    static member Of(error: 'error, value: Task<'value voption>) : Task<Result<'value, 'error>> =
        task {
            let! outcome = value
            return OptionFlow.toResultValueOption error outcome
        }

    static member Of(error: 'error, result: ValueTask<Result<'value, unit>>) : ValueTask<Result<'value, 'error>> =
        ValueTask<Result<'value, 'error>>(
            task {
                let! outcome = result
                return Check.orError error outcome
            }
        )

    static member Of(error: 'error, value: ValueTask<bool>) : ValueTask<Result<unit, 'error>> =
        ValueTask<Result<unit, 'error>>(
            task {
                let! outcome = value
                return if outcome then Ok () else Error error
            }
        )

    static member Of(error: 'error, value: ValueTask<'value option>) : ValueTask<Result<'value, 'error>> =
        ValueTask<Result<'value, 'error>>(
            task {
                let! outcome = value
                return OptionFlow.toResult error outcome
            }
        )

    static member Of(error: 'error, value: ValueTask<'value voption>) : ValueTask<Result<'value, 'error>> =
        ValueTask<Result<'value, 'error>>(
            task {
                let! outcome = value
                return OptionFlow.toResultValueOption error outcome
            }
        )
#endif

    static member Of(error: 'error, flow: Flow<'env, unit, 'value>) : Flow<'env, 'error, 'value> =
        let (Flow operation) = flow
        Flow(fun environment cancellationToken ->
            operation environment cancellationToken
            |> EffectFlow.fold
                EffectFlow.ofValue
                (fun _ -> EffectFlow.ofError error)
        )

#if !FABLE_COMPILER
    static member internal Of(error: 'error, flow: AsyncAdapterFlow<'env, unit, 'value>) : AsyncAdapterFlow<'env, 'error, 'value> =
        flow
        |> GuardFlow.fromAsyncAdapter
        |> Flow.mapError (fun () -> error)
        |> AsyncAdapter.fromFlow

    static member internal Of(error: 'error, flow: TaskAdapterFlow<'env, unit, 'value>) : TaskAdapterFlow<'env, 'error, 'value> =
        flow
        |> GuardFlow.fromTaskAdapter
        |> Flow.mapError (fun () -> error)
        |> TaskAdapter.fromFlow
#endif

    static member MapError(mapper: 'error1 -> 'error2, result: Result<'value, 'error1>) : Result<'value, 'error2> =
        Result.mapError mapper result

    static member MapError(mapper: 'error1 -> 'error2, validation: Validation<'value, 'error1>) : Validation<'value, 'error2> =
        Validation.mapError mapper validation

#if !FABLE_COMPILER
    static member MapError(mapper: 'error1 -> 'error2, result: Async<Result<'value, 'error1>>) : Async<Result<'value, 'error2>> =
        async {
            let! outcome = result
            return Result.mapError mapper outcome
        }

    static member MapError(mapper: 'error1 -> 'error2, result: Task<Result<'value, 'error1>>) : Task<Result<'value, 'error2>> =
        task {
            let! outcome = result
            return Result.mapError mapper outcome
        }

    static member MapError(mapper: 'error1 -> 'error2, result: ValueTask<Result<'value, 'error1>>) : ValueTask<Result<'value, 'error2>> =
        ValueTask<Result<'value, 'error2>>(
            task {
                let! outcome = result
                return Result.mapError mapper outcome
            }
        )
    static member MapError(mapper: 'error1 -> 'error2, flow: Flow<'env, 'error1, 'value>) : Flow<'env, 'error2, 'value> =
        Flow.mapError mapper flow
#endif

#if !FABLE_COMPILER
    static member internal MapError(mapper: 'error1 -> 'error2, flow: AsyncAdapterFlow<'env, 'error1, 'value>) : AsyncAdapterFlow<'env, 'error2, 'value> =
        flow
        |> GuardFlow.fromAsyncAdapter
        |> Flow.mapError mapper
        |> AsyncAdapter.fromFlow

    static member internal MapError(mapper: 'error1 -> 'error2, flow: TaskAdapterFlow<'env, 'error1, 'value>) : TaskAdapterFlow<'env, 'error2, 'value> =
        flow
        |> GuardFlow.fromTaskAdapter
        |> Flow.mapError mapper
        |> TaskAdapter.fromFlow
#endif

#if !FABLE_COMPILER
[<AutoOpen>]
module internal AsyncAdapterBuilderExtensions =
    type AsyncAdapterBuilder with
        member this.ReturnFrom(operation: ValueTask) : AsyncAdapterFlow<'env, 'error, unit> =
            operation.AsTask()
            |> this.ReturnFrom

        member this.ReturnFrom(operation: ValueTask<'value>) : AsyncAdapterFlow<'env, 'error, 'value> =
            operation.AsTask()
            |> this.ReturnFrom

        member _.ReturnFrom(operation: Task) : AsyncAdapterFlow<'env, 'error, unit> =
            operation
            |> Async.AwaitTask
            |> AsyncAdapter.fromAsync

        member _.ReturnFrom(operation: Task<'value>) : AsyncAdapterFlow<'env, 'error, 'value> =
            operation
            |> Async.AwaitTask
            |> AsyncAdapter.fromAsync

        member _.ReturnFrom(operation: ColdTask<Result<'value, 'error>>) : AsyncAdapterFlow<'env, 'error, 'value> =
            async {
                let! cancellationToken = Async.CancellationToken
                return! ColdTask.run cancellationToken operation |> Async.AwaitTask
            }
            |> AsyncAdapter.fromAsyncResult

        member this.Bind
            (
                operation: Task,
                binder: unit -> AsyncAdapterFlow<'env, 'error, 'next>
            ) : AsyncAdapterFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncAdapter.bind binder

        member this.Bind
            (
                operation: Task<'value>,
                binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
            ) : AsyncAdapterFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncAdapter.bind binder

        member this.Bind
            (
                operation: ValueTask,
                binder: unit -> AsyncAdapterFlow<'env, 'error, 'next>
            ) : AsyncAdapterFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncAdapter.bind binder

        member this.Bind
            (
                operation: ValueTask<'value>,
                binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
            ) : AsyncAdapterFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncAdapter.bind binder

        member this.Bind
            (
                operation: ColdTask<Result<'value, 'error>>,
                binder: 'value -> AsyncAdapterFlow<'env, 'error, 'next>
            ) : AsyncAdapterFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncAdapter.bind binder
#endif
