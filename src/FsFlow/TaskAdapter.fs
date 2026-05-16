namespace FsFlow

#if !FABLE_COMPILER

open System
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Core functions for creating, composing, executing, and adapting task adapter flows.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal TaskAdapter =
    /// <summary>Executes a task adapter flow with the provided environment and cancellation token.</summary>
    /// <remarks>Uncaught exceptions become <c>Cause.Die</c>; cancellation becomes <c>Cause.Interrupt</c>.</remarks>
    /// <param name="environment">The environment of type <c>'env</c>.</param>
    /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
    /// <param name="flow">The <see cref="T:FsFlow.TaskAdapterFlow`3" /> to execute.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing the <see cref="T:FsFlow.Exit`2" />.</returns>
    let run
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (TaskAdapterFlow operation: TaskAdapterFlow<'env, 'error, 'value>)
        : Task<Exit<'value, 'error>> =
        task {
            try
                let! exit = operation environment cancellationToken
                return exit
            with error ->
                return Exit.Failure (EffectFlow.causeOfException error)
        }

    /// <summary>Converts a task adapter flow into a hot <see cref="T:System.Threading.Tasks.Task`1" />.</summary>
    /// <remarks>
    /// This is an alias for <see cref="run" /> that emphasizes the conversion to a standard .NET Task.
    /// </remarks>
    /// <param name="environment">The environment of type <c>'env</c>.</param>
    /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
    /// <param name="flow">The task adapter flow to convert.</param>
    /// <returns>A started task.</returns>
    let toTask
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : Task<Exit<'value, 'error>> =
        run environment cancellationToken flow

    /// <summary>Creates a successful task adapter flow.</summary>
    /// <param name="value">The success value of type <c>'value</c>.</param>
    /// <returns>A <see cref="T:FsFlow.TaskAdapterFlow`3" /> that always succeeds.</returns>
    let ok (value: 'value) : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun _ _ -> Task.FromResult(Exit.Success value))

    /// <summary>Alias for <c>ok</c> that reads well in some call sites.</summary>
    let succeed (value: 'value) : TaskAdapterFlow<'env, 'error, 'value> =
        ok value

    /// <summary>Alias for <c>ok</c> that reads well in some call sites.</summary>
    let value (item: 'value) : TaskAdapterFlow<'env, 'error, 'value> =
        succeed item

    /// <summary>Creates a failing task adapter flow.</summary>
    /// <param name="error">The failure value of type <c>'error</c>.</param>
    /// <returns>A <see cref="T:FsFlow.TaskAdapterFlow`3" /> that always fails.</returns>
    let error (failure: 'error) : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun _ _ -> Task.FromResult(Exit.Failure (Cause.Fail failure)))

    /// <summary>Alias for <c>error</c> that reads well in some call sites.</summary>
    let fail (failure: 'error) : TaskAdapterFlow<'env, 'error, 'value> =
        error failure

    /// <summary>Lifts a standard <see cref="T:System.Result`2" /> into a task adapter flow.</summary>
    /// <param name="result">The result to lift.</param>
    /// <returns>A task adapter flow mirroring the result.</returns>
    let fromResult (result: Result<'value, 'error>) : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun _ _ -> Task.FromResult (Exit.fromResult result))

    /// <summary>Executes a task adapter flow and converts the final <see cref="T:FsFlow.Exit`2" /> into a standard <see cref="T:System.Result`2" />.</summary>
    /// <remarks>
    /// Interruption signals and defects are raised as exceptions in the caller's context.
    /// </remarks>
    let toResult (environment: 'env) (cancellationToken: CancellationToken) (flow: TaskAdapterFlow<'env, 'error, 'value>) : Task<Result<'value, 'error>> =
        task {
            let! exit = run environment cancellationToken flow
            return Exit.toResult exit
        }

    /// <summary>Lifts an option into a task adapter flow with the supplied error.</summary>
    /// <param name="error">The error to return if the option is <see cref="T:Microsoft.FSharp.Core.FSharpOption`1.None" />.</param>
    /// <param name="value">The option to lift.</param>
    /// <returns>A task adapter flow succeeding with the option's value or failing.</returns>
    let fromOption (error: 'error) (value: 'value option) : TaskAdapterFlow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResult error
        |> fromResult

    /// <summary>Lifts a value option into a task adapter flow with the supplied error.</summary>
    /// <param name="error">The error of type <c>'error</c> to return if the value option is <see cref="T:Microsoft.FSharp.Core.FSharpValueOption`1.ValueNone" />.</param>
    /// <param name="value">The value option to lift.</param>
    /// <returns>A task adapter flow succeeding with the option's value or failing.</returns>
    let fromValueOption (error: 'error) (value: 'value voption) : TaskAdapterFlow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResultValueOption error
        |> fromResult

    let orElseTask
        (errorTask: Task<'error>)
        (result: Result<'value, unit>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun _ _ ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! error = errorTask
                    return Exit.Failure (Cause.Fail error)
            })

    /// <summary>Turns a pure validation result into a task adapter flow with task-provided failure.</summary>
    /// <returns>A <see cref="T:FsFlow.TaskAdapterFlow`3" /> that mirrors the result or produces the task error.</returns>
    let orElseAsync
        (errorAsync: Async<'error>)
        (result: Result<'value, unit>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun _ _ ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! error = errorAsync |> Async.StartAsTask
                    return Exit.Failure (Cause.Fail error)
            })

    let orElseFlow
        (errorFlow: Flow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    match FlowInternal.run environment errorFlow with
                    | Exit.Success error -> return Exit.Failure (Cause.Fail error)
                    | Exit.Failure cause -> return Exit.Failure cause
            })

    let orElseAsyncFlow
        (errorFlow: AsyncAdapterFlow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! outcome =
                        AsyncAdapter.run environment errorFlow
                        |> fun operation -> Async.StartAsTask(operation, cancellationToken = cancellationToken)

                    match outcome with
                    | Exit.Success error -> return Exit.Failure (Cause.Fail error)
                    | Exit.Failure cause -> return Exit.Failure cause
            })

    let orElseTaskFlow
        (errorFlow: TaskAdapterFlow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! outcome = run environment cancellationToken errorFlow

                    match outcome with
                    | Exit.Success error -> return Exit.Failure (Cause.Fail error)
                    | Exit.Failure cause -> return Exit.Failure cause
            })

    let fromFlow (flow: Flow<'env, 'error, 'value>) : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment _ -> Task.FromResult(FlowInternal.run environment flow))

    let fromAsyncAdapter (flow: AsyncAdapterFlow<'env, 'error, 'value>) : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            AsyncAdapter.run environment flow
            |> fun operation -> Async.StartAsTask(operation, cancellationToken = cancellationToken))

    let fromTask (coldTask: ColdTask<'value>) : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun _ cancellationToken ->
            task {
                let! value = ColdTask.run cancellationToken coldTask
                return Exit.Success value
            })

    let fromTaskResult
        (coldTask: ColdTask<Result<'value, 'error>>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun _ cancellationToken ->
            task {
                let! result = ColdTask.run cancellationToken coldTask
                return match result with Ok v -> Exit.Success v | Error e -> Exit.Failure (Cause.Fail e)
            })

    let env<'env, 'error> : TaskAdapterFlow<'env, 'error, 'env> =
        TaskAdapterFlow(fun environment _ -> Task.FromResult(Exit.Success environment))

    let read (projection: 'env -> 'value) : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment _ -> Task.FromResult(Exit.Success(projection environment)))

    /// <summary>Extracts a specific service from an environment that implements <c>IHas&lt;'service&gt;</c>.</summary>
    let inline service<'service, 'env, 'error when 'env :> IHas<'service>> () : TaskAdapterFlow<'env, 'error, 'service> =
        read (fun (env: 'env) -> env.Service)

    /// <summary>Injects a service from a dynamic IServiceProvider environment.</summary>
    let inline inject<'service, 'env, 'error when 'env :> IServiceProvider> () : TaskAdapterFlow<'env, 'error, 'service> =
        read (fun (env: 'env) ->
            let svc = env.GetService(typeof<'service>)
            if isNull (box svc) then
                failwith $"Service {typeof<'service>.Name} was not registered in the IServiceProvider."
            else
                unbox<'service> svc
        )

    /// <summary>Maps the successful value of a task adapter flow.</summary>
    /// <param name="mapper">A function of type <c>'value -> 'next</c> to transform the success value.</param>
    /// <param name="flow">The source task adapter flow of type <see cref="T:FsFlow.TaskAdapterFlow`3" />.</param>
    /// <returns>A new <see cref="T:FsFlow.TaskAdapterFlow`3" /> with the transformed success value.</returns>
    let map
        (mapper: 'value -> 'next)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'next> =
        TaskAdapterFlow(fun environment cancellationToken ->
            InternalCombinatorCore.mapWith
                (fun mapOutcome operation ->
                    task {
                        let! result = operation
                        return mapOutcome result
                    })
                mapper
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Maps the successful value of a task adapter flow to <c>unit</c>.</summary>
    let ignore (flow: TaskAdapterFlow<'env, 'error, 'value>) : TaskAdapterFlow<'env, 'error, unit> =
        map (fun _ -> ()) flow

    /// <summary>Sequences a task-flow-producing continuation after a successful value.</summary>
    /// <remarks>
    /// This is the "flatmap" operation for <see cref="T:FsFlow.TaskAdapterFlow`3" />. It allows for dependent
    /// asynchronous steps where the second flow depends on the value produced by the first.
    /// </remarks>
    /// <param name="binder">A function that takes the successful value and returns a new task adapter flow.</param>
    /// <param name="flow">The source task adapter flow.</param>
    /// <returns>A new task adapter flow representing the combined workflow.</returns>
    let bind
        (binder: 'value -> TaskAdapterFlow<'env, 'error, 'next>)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'next> =
        TaskAdapterFlow(fun environment cancellationToken ->
            InternalCombinatorCore.bindWith
                (fun operation onSuccess onFailure ->
                    task {
                        let! exit = operation

                        match exit with
                        | Exit.Success value -> return! onSuccess value
                        | Exit.Failure cause -> return! onFailure cause
                    })
                (fun (environment, cancellationToken) value -> binder value |> run environment cancellationToken)
                (Exit.Failure >> Task.FromResult)
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Sequences a task-flow-producing continuation after a successful value.</summary>
    let (>>=)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        (binder: 'value -> TaskAdapterFlow<'env, 'error, 'next>)
        : TaskAdapterFlow<'env, 'error, 'next> =
        bind binder flow

    /// <summary>Runs a task-based side effect on success and preserves the original value.</summary>
    /// <param name="binder">A function that produces a side-effect task adapter flow from the successful value.</param>
    /// <param name="flow">The source task adapter flow.</param>
    /// <returns>A task adapter flow that preserves the original success value after the side effect.</returns>
    let tap
        (binder: 'value -> TaskAdapterFlow<'env, 'error, unit>)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        bind
            (fun value ->
                binder value
                |> map (fun () -> value))
            flow

    /// <summary>Runs a task-based side effect on failure and preserves the original error.</summary>
    /// <param name="binder">A function that produces a side-effect task adapter flow from the error value.</param>
    /// <param name="flow">The source task adapter flow.</param>
    /// <returns>A task adapter flow that preserves the original error after the side effect.</returns>
    let tapError
        (binder: 'error -> TaskAdapterFlow<'env, 'error, unit>)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            task {
                let! exit = run environment cancellationToken flow

                match exit with
                | Exit.Success value -> return Exit.Success value
                | Exit.Failure cause ->
                    match cause with
                    | Cause.Fail error ->
                        let! tapExit = binder error |> run environment cancellationToken

                        match tapExit with
                        | Exit.Success () -> return Exit.Failure cause
                        | Exit.Failure tapCause -> return Exit.Failure tapCause
                    | _ -> return Exit.Failure cause
            })

    /// <summary>Maps the error value of a task adapter flow.</summary>
    /// <param name="mapper">A function of type <c>'error -> 'nextError</c>.</param>
    /// <param name="flow">The source task adapter flow.</param>
    /// <returns> a new <see cref="T:FsFlow.TaskAdapterFlow`3" /> with the transformed error type.</returns>
    let mapError
        (mapper: 'error -> 'nextError)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'nextError, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            InternalCombinatorCore.mapErrorWith
                (fun mapOutcome operation ->
                    task {
                        let! exit = operation
                        return mapOutcome exit
                    })
                mapper
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Catches exceptions raised during execution and maps them to a typed error.</summary>
    /// <param name="handler">A function of type <c>exn -> 'error</c> to map the exception.</param>
    /// <param name="flow">The source task adapter flow.</param>
    /// <returns>A task adapter flow that converts exceptions into success-path errors.</returns>
    let catch
        (handler: exn -> 'error)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            task {
                try
                    return! run environment cancellationToken flow
                with error ->
                    return Exit.Failure (Cause.Fail (handler error))
            })

    /// <summary>Falls back to another task adapter flow when the source flow fails.</summary>
    /// <param name="fallback">The fallback flow of type <see cref="T:FsFlow.TaskAdapterFlow`3" />.</param>
    /// <param name="flow">The primary task adapter flow.</param>
    /// <returns>A task adapter flow that tries the primary first, then the fallback.</returns>
    /// <summary>Computes a fallback task adapter flow from the source error when the source flow fails.</summary>
    let orElseWith
        (fallback: 'error -> TaskAdapterFlow<'env, 'error, 'value>)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            task {
                let! exit = run environment cancellationToken flow

                match exit with
                | Exit.Success value -> return Exit.Success value
                | Exit.Failure (Cause.Fail error) -> return! run environment cancellationToken (fallback error)
                | Exit.Failure cause -> return Exit.Failure cause
            })

    /// <summary>Falls back to another task adapter flow when the source flow fails.</summary>
    let orElse
        (fallback: TaskAdapterFlow<'env, 'error, 'value>)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        orElseWith (fun _ -> fallback) flow

    /// <summary>Combines two task adapter flows into a tuple of their values.</summary>
    /// <param name="left">The first task adapter flow.</param>
    /// <param name="right">The second task adapter flow.</param>
    /// <returns>A task adapter flow containing a tuple of results.</returns>
    let zip
        (left: TaskAdapterFlow<'env, 'error, 'left>)
        (right: TaskAdapterFlow<'env, 'error, 'right>)
        : TaskAdapterFlow<'env, 'error, 'left * 'right> =
        bind
            (fun leftValue ->
                right
                |> map (fun rightValue -> leftValue, rightValue))
            left

    /// <summary>Combines two task adapter flows with a mapping function.</summary>
    /// <param name="mapper">A function of type <c>'left -> 'right -> 'value</c>.</param>
    /// <param name="left">The first task adapter flow.</param>
    /// <param name="right">The second task adapter flow.</param>
    /// <returns>A task adapter flow with the combined value.</returns>
    let map2
        (mapper: 'left -> 'right -> 'value)
        (left: TaskAdapterFlow<'env, 'error, 'left>)
        (right: TaskAdapterFlow<'env, 'error, 'right>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        zip left right
        |> map (fun (leftValue, rightValue) -> mapper leftValue rightValue)

    /// <summary>Applies a task-flow-wrapped function to a task-flow-wrapped value.</summary>
    let apply
        (flow: TaskAdapterFlow<'env, 'error, 'value -> 'next>)
        (value: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'next> =
        map2 (fun mapper input -> mapper input) flow value

    /// <summary>Combines three task adapter flows with a mapping function.</summary>
    let map3
        (mapper: 'left -> 'middle -> 'right -> 'value)
        (left: TaskAdapterFlow<'env, 'error, 'left>)
        (middle: TaskAdapterFlow<'env, 'error, 'middle>)
        (right: TaskAdapterFlow<'env, 'error, 'right>)
        : TaskAdapterFlow<'env, 'error, 'value> =
        apply
            (map2 (fun leftValue middleValue -> fun rightValue -> mapper leftValue middleValue rightValue) left middle)
            right

    /// <summary>Maps the successful value of a task adapter flow.</summary>
    let (<!>)
        (mapper: 'value -> 'next)
        (flow: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'next> =
        map mapper flow

    /// <summary>Applies a task-flow-wrapped function to a task-flow-wrapped value.</summary>
    let (<*>)
        (flow: TaskAdapterFlow<'env, 'error, 'value -> 'next>)
        (value: TaskAdapterFlow<'env, 'error, 'value>)
        : TaskAdapterFlow<'env, 'error, 'next> =
        apply flow value

    /// <summary>Transforms the environment before running a task adapter flow.</summary>
    /// <param name="mapping">A function of type <c>'outerEnvironment -> 'innerEnvironment</c>.</param>
    /// <param name="flow">The task adapter flow expecting <c>'innerEnvironment</c>.</param>
    /// <returns>A task adapter flow that accepts <c>'outerEnvironment</c>.</returns>
    let localEnv
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: TaskAdapterFlow<'innerEnvironment, 'error, 'value>)
        : TaskAdapterFlow<'outerEnvironment, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            InternalCombinatorCore.localEnvWith
                (fun (environment, cancellationToken) innerFlow -> run environment cancellationToken innerFlow)
                (fun (environment, cancellationToken) -> mapping environment, cancellationToken)
                flow
                (environment, cancellationToken))

    /// <summary>Defers task adapter flow construction until execution time.</summary>
    /// <param name="factory">A function of type <c>unit -> TaskAdapterFlow&lt;'env, 'error, 'value&gt;</c>.</param>
    /// <returns>A task adapter flow that evaluates the factory only when executed.</returns>
    let delay (factory: unit -> TaskAdapterFlow<'env, 'error, 'value>) : TaskAdapterFlow<'env, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            InternalCombinatorCore.delayWith
                (fun (environment, cancellationToken) delayedFlow -> run environment cancellationToken delayedFlow)
                factory
                (environment, cancellationToken))

    /// <summary>Transforms a sequence of values into a task adapter flow and stops at the first failure.</summary>
    /// <param name="mapping">A function of type <c>'value -> TaskAdapterFlow&lt;'env, 'error, 'next&gt;</c>.</param>
    /// <param name="values">The input sequence.</param>
    /// <returns>A task adapter flow containing the list of successful results.</returns>
    let traverse
        (mapping: 'value -> TaskAdapterFlow<'env, 'error, 'next>)
        (values: seq<'value>)
        : TaskAdapterFlow<'env, 'error, 'next list> =
        TaskAdapterFlow(fun environment cancellationToken ->
            task {
                let results = ResizeArray()
                let mutable currentFailure = None
                use enumerator = values.GetEnumerator()

                while currentFailure.IsNone && enumerator.MoveNext() do
                    let! outcome = mapping enumerator.Current |> run environment cancellationToken

                    match outcome with
                    | Exit.Success value -> results.Add value
                    | Exit.Failure cause -> currentFailure <- Some cause

                match currentFailure with
                | Some cause -> return Exit.Failure cause
                | None -> return Exit.Success(List.ofSeq results)
            })

    /// <summary>Transforms a sequence of task adapter flows into a task adapter flow of a sequence and stops at the first failure.</summary>
    /// <param name="flows">A sequence of task adapter flows.</param>
    /// <returns>A task adapter flow containing the list of successful results.</returns>
    let sequence (flows: seq<TaskAdapterFlow<'env, 'error, 'value>>) : TaskAdapterFlow<'env, 'error, 'value list> =
        traverse id flows

    /// <summary>Provides a derived environment from a layer flow to a downstream task adapter flow.</summary>
    let provideLayer
        (layer: TaskAdapterFlow<'input, 'error, 'environment>)
        (flow: TaskAdapterFlow<'environment, 'error, 'value>)
        : TaskAdapterFlow<'input, 'error, 'value> =
        TaskAdapterFlow(fun environment cancellationToken ->
            task {
                let! outcome = run environment cancellationToken layer

                match outcome with
                | Exit.Success environment -> return! run environment cancellationToken (flow |> localEnv (fun _ -> environment))
                | Exit.Failure cause -> return Exit.Failure cause
            })

    /// <summary>
    /// Task-native runtime helpers for operational concerns like logging, timeout, retry, and scoped cleanup.
    /// </summary>
    [<RequireQualifiedAccess>]
    module Runtime =
        /// <summary>Reads the current runtime cancellation token.</summary>
        let cancellationToken<'env, 'error> : TaskAdapterFlow<'env, 'error, CancellationToken> =
            TaskAdapterFlow(fun _environment cancellationToken -> Task.FromResult(Exit.Success cancellationToken))

        /// <summary>Converts an <see cref="OperationCanceledException" /> into a typed error.</summary>
        let catchCancellation
            (handler: OperationCanceledException -> 'error)
            (flow: TaskAdapterFlow<'env, 'error, 'value>)
            : TaskAdapterFlow<'env, 'error, 'value> =
            TaskAdapterFlow(fun environment cancellationToken ->
                task {
                    try
                        return! run environment cancellationToken flow
                    with :? OperationCanceledException as error ->
                        return Exit.Failure (Cause.Fail (handler error))
                })

        /// <summary>Returns a typed error immediately when the runtime token is already canceled.</summary>
        let ensureNotCanceled<'env, 'error> (canceledError: 'error) : TaskAdapterFlow<'env, 'error, unit> =
            TaskAdapterFlow(fun _environment cancellationToken ->
                if cancellationToken.IsCancellationRequested then
                    Task.FromResult(Exit.Failure (Cause.Fail canceledError))
                else
                    Task.FromResult(Exit.Success ()))

        /// <summary>Suspends the flow for the specified duration while observing cancellation.</summary>
        let sleep<'env, 'error> (delay: TimeSpan) : TaskAdapterFlow<'env, 'error, unit> =
            TaskAdapterFlow(fun _environment cancellationToken ->
                task {
                    do! Task.Delay(delay, cancellationToken)
                    return Exit.Success ()
                })

        /// <summary>Writes a fixed log message through the environment-provided logger.</summary>
        let log
            (writer: 'env -> LogEntry -> unit)
            (level: LogLevel)
            (message: string)
            : TaskAdapterFlow<'env, 'error, unit> =
            TaskAdapterFlow(fun environment _ ->
                writer
                    environment
                    { Level = level
                      Message = message
                      TimestampUtc = DateTimeOffset.UtcNow }

                Task.FromResult(Exit.Success ()))

        /// <summary>Writes a log message computed from the current environment.</summary>
        let logWith
            (writer: 'env -> LogEntry -> unit)
            (level: LogLevel)
            (messageFactory: 'env -> string)
            : TaskAdapterFlow<'env, 'error, unit> =
            TaskAdapterFlow(fun environment _ ->
                writer
                    environment
                    { Level = level
                      Message = messageFactory environment
                      TimestampUtc = DateTimeOffset.UtcNow }

                Task.FromResult(Exit.Success ()))

        /// <summary>Acquires a resource, uses it, and always runs the release action.</summary>
        let useWithAcquireRelease
            (acquire: TaskAdapterFlow<'env, 'error, 'resource>)
            (release: 'resource -> CancellationToken -> Task)
            (useResource: 'resource -> TaskAdapterFlow<'env, 'error, 'value>)
            : TaskAdapterFlow<'env, 'error, 'value> =
            bind
                (fun resource ->
                    TaskAdapterFlow(fun environment cancellationToken ->
                        async {
                            let! result =
                                run environment cancellationToken (useResource resource)
                                |> Async.AwaitTask
                                |> Async.Catch

                            do! release resource cancellationToken |> Async.AwaitTask

                            match result with
                            | Choice1Of2 exit -> return exit
                            | Choice2Of2 error -> return raise error
                        }
                        |> fun computation -> Async.StartAsTask(computation, cancellationToken = cancellationToken)))
                acquire

        /// <summary>Fails with the supplied error when the flow does not complete before the timeout.</summary>
        let timeout
            (after: TimeSpan)
            (timeoutError: 'error)
            (flow: TaskAdapterFlow<'env, 'error, 'value>)
            : TaskAdapterFlow<'env, 'error, 'value> =
            TaskAdapterFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return Exit.Failure (Cause.Fail timeoutError)
                    else
                        return! operation
                })

        /// <summary>Returns the supplied success value when the flow times out.</summary>
        let timeoutToOk
            (after: TimeSpan)
            (value: 'value)
            (flow: TaskAdapterFlow<'env, 'error, 'value>)
            : TaskAdapterFlow<'env, 'error, 'value> =
            TaskAdapterFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return Exit.Success value
                    else
                        return! operation
                })

        /// <summary>Forwards to <see cref="timeout" /> for a typed failure on timeout.</summary>
        let timeoutToError
            (after: TimeSpan)
            (error: 'error)
            (flow: TaskAdapterFlow<'env, 'error, 'value>)
            : TaskAdapterFlow<'env, 'error, 'value> =
            timeout after error flow

        /// <summary>Runs a fallback flow when the original flow times out.</summary>
        let timeoutWith
            (after: TimeSpan)
            (fallback: unit -> TaskAdapterFlow<'env, 'error, 'value>)
            (flow: TaskAdapterFlow<'env, 'error, 'value>)
            : TaskAdapterFlow<'env, 'error, 'value> =
            TaskAdapterFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return! run environment cancellationToken (fallback ())
                    else
                        return! operation
                })

        /// <summary>Retries a flow according to the specified policy.</summary>
        let retry
            (policy: RetryPolicy<'error>)
            (flow: TaskAdapterFlow<'env, 'error, 'value>)
            : TaskAdapterFlow<'env, 'error, 'value> =
            if policy.MaxAttempts < 1 then
                invalidArg (nameof policy.MaxAttempts) "RetryPolicy.MaxAttempts must be at least 1."

            let rec loop attempt =
                TaskAdapterFlow(fun environment cancellationToken ->
                    task {
                        let! exit = run environment cancellationToken flow

                        match exit with
                        | Exit.Success value -> return Exit.Success value
                        | Exit.Failure (Cause.Fail error) when attempt < policy.MaxAttempts && policy.ShouldRetry error ->
                            let delay = policy.Delay attempt

                            if delay > TimeSpan.Zero then
                                do! Task.Delay(delay, cancellationToken)

                            return! run environment cancellationToken (loop (attempt + 1))
                        | _ ->
                            return exit
                    })

            loop 1
#endif
