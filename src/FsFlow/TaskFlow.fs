namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks
open FsFlow

/// <summary>
/// Represents a cold task-based workflow that reads an environment, observes a runtime cancellation token,
/// returns a typed result, and is executed explicitly through <c>TaskFlow.run</c>.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success value.</typeparam>
type TaskFlow<'env, 'error, 'value> =
    private
    | TaskFlow of ('env -> CancellationToken -> Task<Result<'value, 'error>>)

/// <summary>
/// Represents delayed task work that can observe a runtime cancellation token when it is started.
/// </summary>
/// <typeparam name="value">The type of the produced task value.</typeparam>
type ColdTask<'value> =
    | ColdTask of (CancellationToken -> Task<'value>)

type TaskFlow<'env, 'error, 'value> with
    static member CapabilityService
        (projection: 'env -> 'service)
        : TaskFlow<'env, 'error, 'service> =
        TaskFlow(fun environment _ -> Task.FromResult(Ok(projection environment)))

    static member ServiceFromProvider
        ()
        : TaskFlow<IServiceProvider, MissingCapability, 'service> =
        TaskFlow(fun provider _ ->
            match provider.GetService typeof<'service> with
            | null ->
                Task.FromResult(
                    Error
                        {
                            CapabilityType = typeof<'service>
                        })
            | value -> Task.FromResult(Ok(unbox<'service> value)))

    static member ProvideLayer
        (
            layer: TaskFlow<'input, 'error, 'environment>,
            flow: TaskFlow<'environment, 'error, 'value>
        ) : TaskFlow<'input, 'error, 'value> =
        let (TaskFlow layerOperation) = layer
        let (TaskFlow flowOperation) = flow

        TaskFlow(fun environment cancellationToken ->
            task {
                let! outcome = layerOperation environment cancellationToken

                match outcome with
                | Ok environment -> return! flowOperation environment cancellationToken
                | Error error -> return Error error
            })

/// <summary>
/// Core functions for creating and executing cold tasks.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module ColdTask =
    let create (operation: CancellationToken -> Task<'value>) : ColdTask<'value> =
        ColdTask operation

    let fromTaskFactory (factory: unit -> Task<'value>) : ColdTask<'value> =
        create (fun _ -> factory ())

    let fromTask (startedTask: Task<'value>) : ColdTask<'value> =
        fromTaskFactory (fun () -> startedTask)

    let fromValueTaskFactory
        (factory: CancellationToken -> ValueTask<'value>)
        : ColdTask<'value> =
        create (fun cancellationToken -> factory cancellationToken |> _.AsTask())

    let fromValueTaskFactoryWithoutCancellation
        (factory: unit -> ValueTask<'value>)
        : ColdTask<'value> =
        create (fun _ -> factory () |> _.AsTask())

    let fromValueTask (startedValueTask: ValueTask<'value>) : ColdTask<'value> =
        let startedTask = startedValueTask.AsTask()
        fromTask startedTask

    let run (cancellationToken: CancellationToken) (ColdTask operation: ColdTask<'value>) : Task<'value> =
        operation cancellationToken

/// <summary>
/// Core functions for creating, composing, executing, and adapting task flows.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module TaskFlow =
    /// <summary>Executes a task flow with the provided environment and cancellation token.</summary>
    /// <param name="environment">The environment of type <c>'env</c>.</param>
    /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
    /// <param name="flow">The <see cref="T:FsFlow.TaskFlow`3" /> to execute.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing the <see cref="T:System.Result`2" />.</returns>
    let run
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (TaskFlow operation: TaskFlow<'env, 'error, 'value>)
        : Task<Result<'value, 'error>> =
        operation environment cancellationToken

    /// <summary>Runs a task flow against a <see cref="T:FsFlow.RuntimeContext`2" /> and its internal cancellation token.</summary>
    /// <param name="context">The <see cref="T:FsFlow.RuntimeContext`2" /> providing services and cancellation.</param>
    /// <param name="flow">The task flow to run.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> with the final result.</returns>
    let runContext
        (context: RuntimeContext<'runtime, 'env>)
        (flow: TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>)
        : Task<Result<'value, 'error>> =
        run context context.CancellationToken flow

    /// <summary>Converts a task flow into a hot <see cref="T:System.Threading.Tasks.Task`1" />.</summary>
    /// <remarks>
    /// This is an alias for <see cref="run" /> that emphasizes the conversion to a standard .NET Task.
    /// </remarks>
    /// <param name="environment">The environment of type <c>'env</c>.</param>
    /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
    /// <param name="flow">The task flow to convert.</param>
    /// <returns>A started task.</returns>
    let toTask
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (flow: TaskFlow<'env, 'error, 'value>)
        : Task<Result<'value, 'error>> =
        run environment cancellationToken flow

    /// <summary>Creates a successful task flow.</summary>
    /// <param name="value">The success value of type <c>'value</c>.</param>
    /// <returns>A <see cref="T:FsFlow.TaskFlow`3" /> that always succeeds.</returns>
    let ok (value: 'value) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ -> Task.FromResult(Ok value))

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    let succeed (value: 'value) : TaskFlow<'env, 'error, 'value> =
        ok value

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    let value (item: 'value) : TaskFlow<'env, 'error, 'value> =
        succeed item

    /// <summary>Creates a failing task flow.</summary>
    /// <param name="error">The failure value of type <c>'error</c>.</param>
    /// <returns>A <see cref="T:FsFlow.TaskFlow`3" /> that always fails.</returns>
    let error (failure: 'error) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ -> Task.FromResult(Error failure))

    /// <summary>Alias for <see cref="error" /> that reads well in some call sites.</summary>
    let fail (failure: 'error) : TaskFlow<'env, 'error, 'value> =
        error failure

    /// <summary>Lifts a standard <see cref="T:System.Result`2" /> into a task flow.</summary>
    /// <param name="result">The result to lift.</param>
    /// <returns>A task flow mirroring the result.</returns>
    let fromResult (result: Result<'value, 'error>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ -> Task.FromResult result)

    /// <summary>Lifts an option into a task flow with the supplied error.</summary>
    /// <param name="error">The error to return if the option is <see cref="T:Microsoft.FSharp.Core.FSharpOption`1.None" />.</param>
    /// <param name="value">The option to lift.</param>
    /// <returns>A task flow succeeding with the option's value or failing.</returns>
    let fromOption (error: 'error) (value: 'value option) : TaskFlow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResult error
        |> fromResult

    /// <summary>Lifts a value option into a task flow with the supplied error.</summary>
    /// <param name="error">The error of type <c>'error</c> to return if the value option is <see cref="T:Microsoft.FSharp.Core.FSharpValueOption`1.ValueNone" />.</param>
    /// <param name="value">The value option to lift.</param>
    /// <returns>A task flow succeeding with the option's value or failing.</returns>
    let fromValueOption (error: 'error) (value: 'value voption) : TaskFlow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResultValueOption error
        |> fromResult

    let orElseTask
        (errorTask: Task<'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ ->
            task {
                match result with
                | Ok value -> return Ok value
                | Error () ->
                    let! error = errorTask
                    return Error error
            })

    /// <summary>Turns a pure validation result into a task flow with task-provided failure.</summary>
    /// <returns>A <see cref="T:FsFlow.TaskFlow`3" /> that mirrors the result or produces the task error.</returns>
    let orElseAsync
        (errorAsync: Async<'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ ->
            task {
                match result with
                | Ok value -> return Ok value
                | Error () ->
                    let! error = errorAsync |> Async.StartAsTask
                    return Error error
            })

    let orElseFlow
        (errorFlow: Flow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Ok value
                | Error () ->
                    match Flow.run environment errorFlow with
                    | Ok error -> return Error error
                    | Error error -> return Error error
            })

    let orElseAsyncFlow
        (errorFlow: AsyncFlow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Ok value
                | Error () ->
                    let! outcome =
                        AsyncFlow.run environment errorFlow
                        |> fun operation -> Async.StartAsTask(operation, cancellationToken = cancellationToken)

                    match outcome with
                    | Ok error -> return Error error
                    | Error error -> return Error error
            })

    let orElseTaskFlow
        (errorFlow: TaskFlow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Ok value
                | Error () ->
                    let! outcome = run environment cancellationToken errorFlow

                    match outcome with
                    | Ok error -> return Error error
                    | Error error -> return Error error
            })

    let fromFlow (flow: Flow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment _ -> Task.FromResult(Flow.run environment flow))

    let fromAsyncFlow (flow: AsyncFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            AsyncFlow.run environment flow
            |> fun operation -> Async.StartAsTask(operation, cancellationToken = cancellationToken))

    let fromTask (coldTask: ColdTask<'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ cancellationToken ->
            task {
                let! value = ColdTask.run cancellationToken coldTask
                return Ok value
            })

    let fromTaskResult
        (coldTask: ColdTask<Result<'value, 'error>>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ cancellationToken -> ColdTask.run cancellationToken coldTask)

    let env<'env, 'error> : TaskFlow<'env, 'error, 'env> =
        TaskFlow(fun environment _ -> Task.FromResult(Ok environment))

    let read (projection: 'env -> 'value) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment _ -> Task.FromResult(Ok(projection environment)))

    /// <summary>Reads the runtime half of a runtime-context environment.</summary>
    let readRuntime
        (projection: 'runtime -> 'value)
        : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value> =
        read (fun context -> projection context.Runtime)

    /// <summary>Reads the application environment half of a runtime-context environment.</summary>
    let readEnvironment
        (projection: 'env -> 'value)
        : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value> =
        read (fun context -> projection context.Environment)

    /// <summary>Maps the successful value of a task flow.</summary>
    /// <param name="mapper">A function of type <c>'value -> 'next</c> to transform the success value.</param>
    /// <param name="flow">The source task flow of type <see cref="T:FsFlow.TaskFlow`3" />.</param>
    /// <returns>A new <see cref="T:FsFlow.TaskFlow`3" /> with the transformed success value.</returns>
    let map
        (mapper: 'value -> 'next)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.mapWith
                (fun mapOutcome operation ->
                    task {
                        let! result = operation
                        return mapOutcome result
                    })
                mapper
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Maps the successful value of a task flow to <c>unit</c>.</summary>
    let ignore (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, unit> =
        map (fun _ -> ()) flow

    /// <summary>Sequences a task-flow-producing continuation after a successful value.</summary>
    /// <remarks>
    /// This is the "flatmap" operation for <see cref="T:FsFlow.TaskFlow`3" />. It allows for dependent
    /// asynchronous steps where the second flow depends on the value produced by the first.
    /// </remarks>
    /// <param name="binder">A function of type <c>'value -> TaskFlow&lt;'env, 'error, 'next&gt;</c>.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A new task flow representing the combined workflow.</returns>
    let bind
        (binder: 'value -> TaskFlow<'env, 'error, 'next>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.bindWith
                (fun operation onSuccess onError ->
                    task {
                        let! result = operation

                        match result with
                        | Ok value -> return! onSuccess value
                        | Error error -> return! onError error
                    })
                (fun (environment, cancellationToken) value -> binder value |> run environment cancellationToken)
                (Error >> Task.FromResult)
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Sequences a task-flow-producing continuation after a successful value.</summary>
    let inline (>>=)
        (flow: TaskFlow<'env, 'error, 'value>)
        (binder: 'value -> TaskFlow<'env, 'error, 'next>)
        : TaskFlow<'env, 'error, 'next> =
        bind binder flow

    /// <summary>Runs a task-based side effect on success and preserves the original value.</summary>
    /// <param name="binder">A function that produces a side-effect task flow from the successful value.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A task flow that preserves the original success value after the side effect.</returns>
    let tap
        (binder: 'value -> TaskFlow<'env, 'error, unit>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        bind
            (fun value ->
                binder value
                |> map (fun () -> value))
            flow

    /// <summary>Runs a task-based side effect on failure and preserves the original error.</summary>
    /// <param name="binder">A function that produces a side-effect task flow from the error value.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A task flow that preserves the original error after the side effect.</returns>
    let tapError
        (binder: 'error -> TaskFlow<'env, 'error, unit>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let! result = run environment cancellationToken flow

                match result with
                | Ok value -> return Ok value
                | Error error ->
                    let! tapResult = binder error |> run environment cancellationToken

                    match tapResult with
                    | Ok () -> return Error error
                    | Error nextError -> return Error nextError
            })

    /// <summary>Maps the error value of a task flow.</summary>
    /// <param name="mapper">A function of type <c>'error -> 'nextError</c>.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A new <see cref="T:FsFlow.TaskFlow`3" /> with the transformed error type.</returns>
    let mapError
        (mapper: 'error -> 'nextError)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'nextError, 'value> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.mapErrorWith
                (fun mapOutcome operation ->
                    task {
                        let! result = operation
                        return mapOutcome result
                    })
                mapper
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Catches exceptions raised during execution and maps them to a typed error.</summary>
    /// <param name="handler">A function of type <c>exn -> 'error</c> to map the exception.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A task flow that converts exceptions into success-path errors.</returns>
    let catch
        (handler: exn -> 'error)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                try
                    return! run environment cancellationToken flow
                with error ->
                    return Error(handler error)
            })

    /// <summary>Falls back to another task flow when the source flow fails.</summary>
    /// <param name="fallback">The fallback flow of type <see cref="T:FsFlow.TaskFlow`3" />.</param>
    /// <param name="flow">The primary task flow.</param>
    /// <returns>A task flow that tries the primary first, then the fallback.</returns>
    /// <summary>Computes a fallback task flow from the source error when the source flow fails.</summary>
    let orElseWith
        (fallback: 'error -> TaskFlow<'env, 'error, 'value>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let! result = run environment cancellationToken flow

                match result with
                | Ok value -> return Ok value
                | Error error -> return! run environment cancellationToken (fallback error)
            })

    /// <summary>Falls back to another task flow when the source flow fails.</summary>
    let orElse
        (fallback: TaskFlow<'env, 'error, 'value>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        orElseWith (fun _ -> fallback) flow

    /// <summary>Combines two task flows into a tuple of their values.</summary>
    /// <param name="left">The first task flow.</param>
    /// <param name="right">The second task flow.</param>
    /// <returns>A task flow containing a tuple of results.</returns>
    let zip
        (left: TaskFlow<'env, 'error, 'left>)
        (right: TaskFlow<'env, 'error, 'right>)
        : TaskFlow<'env, 'error, 'left * 'right> =
        bind
            (fun leftValue ->
                right
                |> map (fun rightValue -> leftValue, rightValue))
            left

    /// <summary>Combines two task flows with a mapping function.</summary>
    /// <param name="mapper">A function of type <c>'left -> 'right -> 'value</c>.</param>
    /// <param name="left">The first task flow.</param>
    /// <param name="right">The second task flow.</param>
    /// <returns>A task flow with the combined value.</returns>
    let map2
        (mapper: 'left -> 'right -> 'value)
        (left: TaskFlow<'env, 'error, 'left>)
        (right: TaskFlow<'env, 'error, 'right>)
        : TaskFlow<'env, 'error, 'value> =
        zip left right
        |> map (fun (leftValue, rightValue) -> mapper leftValue rightValue)

    /// <summary>Applies a task-flow-wrapped function to a task-flow-wrapped value.</summary>
    let apply
        (flow: TaskFlow<'env, 'error, 'value -> 'next>)
        (value: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        map2 (fun mapper input -> mapper input) flow value

    /// <summary>Combines three task flows with a mapping function.</summary>
    let map3
        (mapper: 'left -> 'middle -> 'right -> 'value)
        (left: TaskFlow<'env, 'error, 'left>)
        (middle: TaskFlow<'env, 'error, 'middle>)
        (right: TaskFlow<'env, 'error, 'right>)
        : TaskFlow<'env, 'error, 'value> =
        apply
            (map2 (fun leftValue middleValue -> fun rightValue -> mapper leftValue middleValue rightValue) left middle)
            right

    /// <summary>Maps the successful value of a task flow.</summary>
    let inline (<!>)
        (mapper: 'value -> 'next)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        map mapper flow

    /// <summary>Applies a task-flow-wrapped function to a task-flow-wrapped value.</summary>
    let inline (<*>)
        (flow: TaskFlow<'env, 'error, 'value -> 'next>)
        (value: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        apply flow value

    /// <summary>Transforms the environment before running a task flow.</summary>
    /// <param name="mapping">A function of type <c>'outerEnvironment -> 'innerEnvironment</c>.</param>
    /// <param name="flow">The task flow expecting <c>'innerEnvironment</c>.</param>
    /// <returns>A task flow that accepts <c>'outerEnvironment</c>.</returns>
    let localEnv
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: TaskFlow<'innerEnvironment, 'error, 'value>)
        : TaskFlow<'outerEnvironment, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.localEnvWith
                (fun (environment, cancellationToken) innerFlow -> run environment cancellationToken innerFlow)
                (fun (environment, cancellationToken) -> mapping environment, cancellationToken)
                flow
                (environment, cancellationToken))

    /// <summary>Defers task flow construction until execution time.</summary>
    /// <param name="factory">A function of type <c>unit -> TaskFlow&lt;'env, 'error, 'value&gt;</c>.</param>
    /// <returns>A task flow that evaluates the factory only when executed.</returns>
    let delay (factory: unit -> TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.delayWith
                (fun (environment, cancellationToken) delayedFlow -> run environment cancellationToken delayedFlow)
                factory
                (environment, cancellationToken))

    /// <summary>Transforms a sequence of values into a task flow and stops at the first failure.</summary>
    /// <param name="mapping">A function of type <c>'value -> TaskFlow&lt;'env, 'error, 'next&gt;</c>.</param>
    /// <param name="values">The input sequence.</param>
    /// <returns>A task flow containing the list of successful results.</returns>
    let traverse
        (mapping: 'value -> TaskFlow<'env, 'error, 'next>)
        (values: seq<'value>)
        : TaskFlow<'env, 'error, 'next list> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let results = ResizeArray()
                let mutable currentError = None
                use enumerator = values.GetEnumerator()

                while currentError.IsNone && enumerator.MoveNext() do
                    let! outcome = mapping enumerator.Current |> run environment cancellationToken

                    match outcome with
                    | Ok value -> results.Add value
                    | Error error -> currentError <- Some error

                match currentError with
                | Some error -> return Error error
                | None -> return Ok(List.ofSeq results)
            })

    /// <summary>Transforms a sequence of task flows into a task flow of a sequence and stops at the first failure.</summary>
    /// <param name="flows">A sequence of task flows.</param>
    /// <returns>A task flow containing the list of successful results.</returns>
    let sequence (flows: seq<TaskFlow<'env, 'error, 'value>>) : TaskFlow<'env, 'error, 'value list> =
        traverse id flows

    /// <summary>Provides a derived environment from a layer flow to a downstream task flow.</summary>
    let provideLayer
        (layer: TaskFlow<'input, 'error, 'environment>)
        (flow: TaskFlow<'environment, 'error, 'value>)
        : TaskFlow<'input, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let! outcome = run environment cancellationToken layer

                match outcome with
                | Ok environment -> return! run environment cancellationToken (flow |> localEnv (fun _ -> environment))
                | Error error -> return Error error
            })

    /// <summary>
    /// Task-native runtime helpers for operational concerns like logging, timeout, retry, and scoped cleanup.
    /// </summary>
    [<RequireQualifiedAccess>]
    module Runtime =
        /// <summary>Reads the current runtime cancellation token.</summary>
        let cancellationToken<'env, 'error> : TaskFlow<'env, 'error, CancellationToken> =
            TaskFlow(fun _environment cancellationToken -> Task.FromResult(Ok cancellationToken))

        /// <summary>Converts an <see cref="OperationCanceledException" /> into a typed error.</summary>
        let catchCancellation
            (handler: OperationCanceledException -> 'error)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    try
                        return! run environment cancellationToken flow
                    with :? OperationCanceledException as error ->
                        return Error(handler error)
                })

        /// <summary>Returns a typed error immediately when the runtime token is already canceled.</summary>
        let ensureNotCanceled<'env, 'error> (canceledError: 'error) : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun _environment cancellationToken ->
                if cancellationToken.IsCancellationRequested then
                    Task.FromResult(Error canceledError)
                else
                    Task.FromResult(Ok ()))

        /// <summary>Suspends the flow for the specified duration while observing cancellation.</summary>
        let sleep<'env, 'error> (delay: TimeSpan) : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun _environment cancellationToken ->
                task {
                    do! Task.Delay(delay, cancellationToken)
                    return Ok ()
                })

        /// <summary>Writes a fixed log message through the environment-provided logger.</summary>
        let log
            (writer: 'env -> LogEntry -> unit)
            (level: LogLevel)
            (message: string)
            : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun environment _ ->
                writer
                    environment
                    { Level = level
                      Message = message
                      TimestampUtc = DateTimeOffset.UtcNow }

                Task.FromResult(Ok ()))

        /// <summary>Writes a log message computed from the current environment.</summary>
        let logWith
            (writer: 'env -> LogEntry -> unit)
            (level: LogLevel)
            (messageFactory: 'env -> string)
            : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun environment _ ->
                writer
                    environment
                    { Level = level
                      Message = messageFactory environment
                      TimestampUtc = DateTimeOffset.UtcNow }

                Task.FromResult(Ok ()))

        /// <summary>Acquires a resource, uses it, and always runs the release action.</summary>
        let useWithAcquireRelease
            (acquire: TaskFlow<'env, 'error, 'resource>)
            (release: 'resource -> CancellationToken -> Task)
            (useResource: 'resource -> TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            bind
                (fun resource ->
                    TaskFlow(fun environment cancellationToken ->
                        async {
                            let! result =
                                run environment cancellationToken (useResource resource)
                                |> Async.AwaitTask
                                |> Async.Catch

                            do! release resource cancellationToken |> Async.AwaitTask

                            match result with
                            | Choice1Of2 (Ok value) -> return Ok value
                            | Choice1Of2 (Error error) -> return Error error
                            | Choice2Of2 error -> return raise error
                        }
                        |> fun computation -> Async.StartAsTask(computation, cancellationToken = cancellationToken)))
                acquire

        /// <summary>Fails with the supplied error when the flow does not complete before the timeout.</summary>
        let timeout
            (after: TimeSpan)
            (timeoutError: 'error)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return Error timeoutError
                    else
                        return! operation
                })

        /// <summary>Returns the supplied success value when the flow times out.</summary>
        let timeoutToOk
            (after: TimeSpan)
            (value: 'value)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return Ok value
                    else
                        return! operation
                })

        /// <summary>Forwards to <see cref="timeout" /> for a typed failure on timeout.</summary>
        let timeoutToError
            (after: TimeSpan)
            (error: 'error)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            timeout after error flow

        /// <summary>Runs a fallback flow when the original flow times out.</summary>
        let timeoutWith
            (after: TimeSpan)
            (fallback: unit -> TaskFlow<'env, 'error, 'value>)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return! run environment cancellationToken (fallback ())
                    else
                        return! operation
                })

        /// <summary>Retries a flow according to the supplied policy.</summary>
        let retry
            (policy: RetryPolicy<'error>)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            if policy.MaxAttempts < 1 then
                invalidArg (nameof policy.MaxAttempts) "RetryPolicy.MaxAttempts must be at least 1."

            let rec loop attempt =
                TaskFlow(fun environment cancellationToken ->
                    task {
                        let! result = run environment cancellationToken flow

                        match result with
                        | Ok value -> return Ok value
                        | Error error when attempt < policy.MaxAttempts && policy.ShouldRetry error ->
                            let delay = policy.Delay attempt

                            if delay > TimeSpan.Zero then
                                do! Task.Delay(delay, cancellationToken)

                            return! run environment cancellationToken (loop (attempt + 1))
                        | Error error ->
                            return Error error
                    })

            loop 1

/// <summary>
/// Describes a task-flow program that is built against a runtime context and later executed with a cancellation token.
/// </summary>
/// <typeparam name="runtime">The runtime service type captured by the spec.</typeparam>
/// <typeparam name="env">The application environment type captured by the spec.</typeparam>
/// <typeparam name="error">The error type produced by the task flow.</typeparam>
/// <typeparam name="value">The success type produced by the task flow.</typeparam>
type TaskFlowSpec<'runtime, 'env, 'error, 'value> =
    {
        /// <summary>Runtime services to supply when the spec is run.</summary>
        Runtime: 'runtime

        /// <summary>Application dependencies to supply when the spec is run.</summary>
        Environment: 'env

        /// <summary>Builds the task flow that should run against the runtime context.</summary>
        Build: unit -> TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>
    }

/// <summary>Helpers for creating and running <see cref="TaskFlowSpec{runtime, env, error, value}" /> values.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module TaskFlowSpec =
    /// <summary>Creates a task-flow spec from runtime services, application dependencies, and a build function.</summary>
    let create
        (runtime: 'runtime)
        (environment: 'env)
        (build: unit -> TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>)
        : TaskFlowSpec<'runtime, 'env, 'error, 'value> =
        {
            Runtime = runtime
            Environment = environment
            Build = build
        }

    /// <summary>Runs the spec with the supplied cancellation token.</summary>
    let run
        (cancellationToken: CancellationToken)
        (spec: TaskFlowSpec<'runtime, 'env, 'error, 'value>)
        : Task<Result<'value, 'error>> =
        let context = RuntimeContext.create spec.Runtime spec.Environment cancellationToken

        spec.Build ()
        |> TaskFlow.run context cancellationToken

/// <summary>Capability helpers for record-based environments and .NET service-provider interop.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Capability =
    /// <summary>Reads a capability from a record-based environment projection.</summary>
    let inline service (projection: 'env -> 'service) : ^flow
        when ^flow : (static member CapabilityService : ('env -> 'service) -> ^flow) =
        (^flow : (static member CapabilityService : ('env -> 'service) -> ^flow) projection)

    /// <summary>Reads a capability from the runtime half of a two-context runtime environment.</summary>
    let runtime
        (projection: 'runtime -> 'service)
        : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'service> =
        TaskFlow.read (fun context -> projection context.Runtime)

    /// <summary>Reads a capability from the application half of a two-context runtime environment.</summary>
    let environment
        (projection: 'env -> 'service)
        : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'service> =
        TaskFlow.read (fun context -> projection context.Environment)

    /// <summary>Reads a service from <see cref="IServiceProvider" /> and fails when it is not registered.</summary>
    let serviceFromProvider<'service> : TaskFlow<IServiceProvider, MissingCapability, 'service> =
        TaskFlow(fun provider _ ->
            match provider.GetService typeof<'service> with
            | null ->
                Task.FromResult(
                    Error
                        {
                            CapabilityType = typeof<'service>
                        })
            | value -> Task.FromResult(Ok(unbox<'service> value)))

/// <summary>Helpers for deriving an environment in one flow and consuming it in another.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Layer =
    /// <summary>Provides a derived environment from a layer flow to a downstream flow.</summary>
    let inline provideLayer (layer: ^layer) (flow: ^flow) : ^flow
        when ^flow : (static member ProvideLayer : ^layer * ^flow -> ^flow) =
        (^flow : (static member ProvideLayer : ^layer * ^flow -> ^flow) (layer, flow))

/// [omit]
/// <exclude/>
type TaskFlowBuilder() =
    member _.Return(value: 'value) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.ok value

    member _.Yield(value: obj) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.ok (unbox<'value> value)

    member _.Yield(project: 'env -> 'value) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.read project

    member _.YieldFrom(flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(operation: Async<'value>) : TaskFlow<'env, 'error, 'value> =
        operation
        |> AsyncFlow.fromAsync
        |> TaskFlow.fromAsyncFlow

    member _.ReturnFrom(operation: Task<Result<'value, 'error>>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ -> operation)

    member _.ReturnFrom(operation: ValueTask<Result<'value, 'error>>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ -> operation.AsTask())

    member _.ReturnFrom(operation: ColdTask<Result<'value, 'error>>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromTaskResult operation

    member _.ReturnFrom(operation: Async<Result<'value, 'error>>) : TaskFlow<'env, 'error, 'value> =
        operation
        |> AsyncFlow.fromAsyncResult
        |> TaskFlow.fromAsyncFlow

    member _.ReturnFrom(flow: AsyncFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromAsyncFlow flow

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromFlow flow

    member _.ReturnFrom(result: Result<'value, 'error>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromResult result

    member _.ReturnFrom(option: 'value option) : TaskFlow<'env, unit, 'value> =
        match option with
        | Some value -> TaskFlow.ok value
        | None -> TaskFlow.error ()

    member _.ReturnFrom(option: 'value voption) : TaskFlow<'env, unit, 'value> =
        match option with
        | ValueSome value -> TaskFlow.ok value
        | ValueNone -> TaskFlow.error ()

    member _.Zero() : TaskFlow<'env, 'error, unit> =
        TaskFlow.ok ()

    member _.Bind
        (
            flow: TaskFlow<'env, 'error, 'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        TaskFlow.bind binder flow

    member _.Bind
        (
            flow: AsyncFlow<'env, 'error, 'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        flow
        |> TaskFlow.fromAsyncFlow
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: Async<'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        operation
        |> AsyncFlow.fromAsync
        |> TaskFlow.fromAsyncFlow
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: Async<Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        operation
        |> AsyncFlow.fromAsyncResult
        |> TaskFlow.fromAsyncFlow
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: Task<Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        TaskFlow(fun _ _ -> operation)
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: ValueTask<Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        TaskFlow(fun _ _ -> operation.AsTask())
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: ColdTask<Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        operation
        |> TaskFlow.fromTaskResult
        |> TaskFlow.bind binder

    member _.Bind
        (
            flow: Flow<'env, 'error, 'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        flow
        |> TaskFlow.fromFlow
        |> TaskFlow.bind binder

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        result
        |> TaskFlow.fromResult
        |> TaskFlow.bind binder

    member _.Bind
        (
            option: 'value option,
            binder: 'value -> TaskFlow<'env, unit, 'next>
        ) : TaskFlow<'env, unit, 'next> =
        match option with
        | Some value -> binder value
        | None -> TaskFlow.error ()

    member _.Bind
        (
            option: 'value voption,
            binder: 'value -> TaskFlow<'env, unit, 'next>
        ) : TaskFlow<'env, unit, 'next> =
        match option with
        | ValueSome value -> binder value
        | ValueNone -> TaskFlow.error ()

    member _.Delay(factory: unit -> TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.delay factory

    member _.Run(flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        flow

    member _.Combine
        (
            first: TaskFlow<'env, 'error, unit>,
            second: TaskFlow<'env, 'error, 'value>
        ) : TaskFlow<'env, 'error, 'value> =
        first
        |> TaskFlow.bind (fun () -> second)

    member _.TryWith
        (
            flow: TaskFlow<'env, 'error, 'value>,
            handler: exn -> TaskFlow<'env, 'error, 'value>
        ) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                try
                    return! TaskFlow.run environment cancellationToken flow
                with error ->
                    return! TaskFlow.run environment cancellationToken (handler error)
            })

    member _.TryFinally
        (
            flow: TaskFlow<'env, 'error, 'value>,
            compensation: unit -> unit
        ) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                try
                    return! TaskFlow.run environment cancellationToken flow
                finally
                    compensation ()
            })

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> TaskFlow<'env, 'error, 'value>
        ) : TaskFlow<'env, 'error, 'value>
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
            body: TaskFlow<'env, 'error, unit>
        ) : TaskFlow<'env, 'error, unit> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> TaskFlow<'env, 'error, unit>
        ) : TaskFlow<'env, 'error, unit> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

[<AutoOpen>]
module TaskFlowBuilderExtensions =
    type TaskFlowBuilder with
        member this.ReturnFrom(operation: ValueTask) : TaskFlow<'env, 'error, unit> =
            operation.AsTask()
            |> this.ReturnFrom

        member this.ReturnFrom(operation: ValueTask<'value>) : TaskFlow<'env, 'error, 'value> =
            operation.AsTask()
            |> this.ReturnFrom

        member _.ReturnFrom(operation: Task) : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun _ _ ->
                task {
                    do! operation
                    return Ok()
                })

        member _.ReturnFrom(operation: Task<'value>) : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun _ _ ->
                task {
                    let! value = operation
                    return Ok value
                })

        member this.Bind
            (
                operation: Task,
                binder: unit -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member this.Bind
            (
                operation: Task<'value>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member this.Bind
            (
                operation: ValueTask,
                binder: unit -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member this.Bind
            (
                operation: ValueTask<'value>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member _.ReturnFrom(operation: ColdTask<'value>) : TaskFlow<'env, 'error, 'value> =
            TaskFlow.fromTask operation

        member this.Bind
            (
                operation: ColdTask<'value>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

/// [omit]
[<AutoOpen>]
module AsyncFlowBuilderExtensions =
    type AsyncFlowBuilder with
        member this.ReturnFrom(operation: ValueTask) : AsyncFlow<'env, 'error, unit> =
            operation.AsTask()
            |> this.ReturnFrom

        member this.ReturnFrom(operation: ValueTask<'value>) : AsyncFlow<'env, 'error, 'value> =
            operation.AsTask()
            |> this.ReturnFrom

        member _.ReturnFrom(operation: Task) : AsyncFlow<'env, 'error, unit> =
            operation
            |> Async.AwaitTask
            |> AsyncFlow.fromAsync

        member _.ReturnFrom(operation: Task<'value>) : AsyncFlow<'env, 'error, 'value> =
            operation
            |> Async.AwaitTask
            |> AsyncFlow.fromAsync

        member _.ReturnFrom(operation: ColdTask<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value> =
            async {
                let! cancellationToken = Async.CancellationToken
                return! ColdTask.run cancellationToken operation |> Async.AwaitTask
            }
            |> AsyncFlow.fromAsyncResult

        member this.Bind
            (
                operation: Task,
                binder: unit -> AsyncFlow<'env, 'error, 'next>
            ) : AsyncFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncFlow.bind binder

        member this.Bind
            (
                operation: Task<'value>,
                binder: 'value -> AsyncFlow<'env, 'error, 'next>
            ) : AsyncFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncFlow.bind binder

        member this.Bind
            (
                operation: ValueTask,
                binder: unit -> AsyncFlow<'env, 'error, 'next>
            ) : AsyncFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncFlow.bind binder

        member this.Bind
            (
                operation: ValueTask<'value>,
                binder: 'value -> AsyncFlow<'env, 'error, 'next>
            ) : AsyncFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncFlow.bind binder

        member _.ReturnFrom(operation: ColdTask<'value>) : AsyncFlow<'env, 'error, 'value> =
            async {
                let! cancellationToken = Async.CancellationToken
                let! value = ColdTask.run cancellationToken operation |> Async.AwaitTask
                return value
            }
            |> AsyncFlow.fromAsync

        member this.Bind
            (
                operation: ColdTask<'value>,
                binder: 'value -> AsyncFlow<'env, 'error, 'next>
            ) : AsyncFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncFlow.bind binder

        member this.Bind
            (
                operation: ColdTask<Result<'value, 'error>>,
                binder: 'value -> AsyncFlow<'env, 'error, 'next>
            ) : AsyncFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> AsyncFlow.bind binder

[<AutoOpen>]
module TaskBuilders =
    /// <summary>
    /// The .NET <c>taskFlow { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This builder enables using <c>let!</c>, <c>do!</c>, and other standard computation expression 
    /// features with <see cref="T:FsFlow.TaskFlow`3" />.
    /// </para>
    /// <para>
    /// It supports seamless binding to many types:
    /// <list type="bullet">
    /// <item><description><see cref="T:FsFlow.TaskFlow`3" /> (standard flow)</description></item>
    /// <item><description><see cref="T:FsFlow.AsyncFlow`3" /> (lifts to task-based flow)</description></item>
    /// <item><description><see cref="T:FsFlow.Flow`3" /> (lifts synchronous to task-based)</description></item>
    /// <item><description><see cref="T:System.Threading.Tasks.Task`1" /> and <see cref="T:System.Threading.Tasks.Task" /> (auto-wraps in Ok)</description></item>
    /// <item><description><see cref="T:System.Threading.Tasks.ValueTask`1" /> and <see cref="T:System.Threading.Tasks.ValueTask" /> (auto-wraps in Ok)</description></item>
    /// <item><description><see cref="T:System.Result`2" /> (lifts pure result to task-based flow)</description></item>
    /// <item><description><see cref="T:Microsoft.FSharp.Control.FSharpAsync`1" /> (auto-wraps in Ok)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// It also supports <c>Guard.Of</c> and <c>Guard.MapError</c> for inline
    /// check-like sources and existing-error remapping before binding into the flow.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let getUser (id: int) = taskFlow {
    ///     let! db = TaskFlow.read (fun env -> env.Db)
    ///     let! user = db.FindUserAsync(id) |> Guard.Of (UserNotFound id)
    ///     do! Task.Delay(100) // Bind to Task
    ///     return user
    /// }
    /// ```
    /// </example>
    let taskFlow = TaskFlowBuilder()
