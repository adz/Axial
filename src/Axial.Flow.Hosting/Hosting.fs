namespace Axial.Flow.Hosting

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Axial.Flow
open Axial.Flow.PlatformService

/// <summary>Adapts Microsoft.Extensions.Logging loggers to Axial's explicit logging service.</summary>
[<RequireQualifiedAccess>]
module MicrosoftLogging =
    let private write (logger: ILogger) (level: LogLevel) (error: exn option) (message: string) =
        match level, error with
        | LogLevel.Trace, None -> logger.LogTrace message
        | LogLevel.Debug, None -> logger.LogDebug message
        | LogLevel.Information, None -> logger.LogInformation message
        | LogLevel.Warning, None -> logger.LogWarning message
        | LogLevel.Error, None -> logger.LogError message
        | LogLevel.Critical, None -> logger.LogCritical message
        | LogLevel.Trace, Some error -> logger.LogTrace(error, message)
        | LogLevel.Debug, Some error -> logger.LogDebug(error, message)
        | LogLevel.Information, Some error -> logger.LogInformation(error, message)
        | LogLevel.Warning, Some error -> logger.LogWarning(error, message)
        | LogLevel.Error, Some error -> logger.LogError(error, message)
        | LogLevel.Critical, Some error -> logger.LogCritical(error, message)

    /// <summary>Creates an Axial logger backed by a supplied Microsoft logger.</summary>
    let create (logger: ILogger) : ILog =
        if isNull logger then nullArg (nameof logger)

        { new ILog with
            member _.Log level message = write logger level None message
            member _.LogException level error message = write logger level (Some error) message }

    /// <summary>Creates an Axial logger with an explicit Microsoft logging category.</summary>
    let fromFactory (categoryName: string) (loggerFactory: ILoggerFactory) : ILog =
        if String.IsNullOrWhiteSpace categoryName then invalidArg (nameof categoryName) "A logging category is required."
        if isNull loggerFactory then nullArg (nameof loggerFactory)
        create (loggerFactory.CreateLogger categoryName)

    /// <summary>Builds an Axial logger from a Microsoft logger factory supplied in the layer input.</summary>
    let layer (categoryName: string) : Layer<ILoggerFactory, Never, ILog> =
        Layer.fromValueTask(fun (loggerFactory, _) _ ->
            ValueTask<Exit<ILog, Never>>(Exit.Success(fromFactory categoryName loggerFactory)))

/// <summary>Fiber-lifecycle logging through Microsoft.Extensions.Logging.</summary>
[<RequireQualifiedAccess>]
module FiberLogging =
    /// <summary>Logs fiber defects as errors and unobserved defects as critical entries.</summary>
    let observer (logger: ILogger) : FiberObserver =
        if isNull logger then nullArg (nameof logger)

        { FiberObserver.none with
            OnEnd =
                fun metadata defect ->
                    defect
                    |> Option.iter (fun error ->
                        logger.LogError(error, "Fiber {FiberId} died with a defect", metadata.Id.Value))
            OnUnobservedDefect =
                fun metadata defect ->
                    match metadata with
                    | Some fiber ->
                        logger.LogCritical(defect, "Unobserved fiber defect (fiber {FiberId})", fiber.Id.Value)
                    | None ->
                        logger.LogCritical(defect, "Unobserved defect from a discarded race/timeout loser") }

    /// <summary>Installs fiber defect logging at the root application edge.</summary>
    let observe (logger: ILogger) (application: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        application |> Flow.withFiberObserver (observer logger)

/// <summary>Controls how a root Flow application participates in Generic Host lifetime.</summary>
type HostedAppOptions =
    { /// Request host shutdown when the root application completes for any reason.
      StopHostOnCompletion: bool }

    /// <summary>Options for a root application that owns the host lifetime.</summary>
    static member Default = { StopHostOnCompletion = true }

/// <summary>Runs one root Flow application as a Microsoft Generic Host hosted service.</summary>
type FlowHostedService<'env, 'error>
    (
        services: IServiceProvider,
        environmentFactory: IServiceProvider -> 'env,
        describeError: 'error -> string,
        application: Flow<'env, 'error, unit>,
        logger: ILogger<FlowHostedService<'env, 'error>>,
        lifetime: IHostApplicationLifetime,
        options: HostedAppOptions
    ) =

    let mutable running: AppHandle<'error, unit> option = None
    let mutable monitor: Task = Task.CompletedTask

    let report exit =
        match exit with
        | Exit.Success () ->
            logger.LogInformation("Axial root application completed")
        | Exit.Failure cause when Cause.isInterrupted cause ->
            logger.LogInformation("Axial root application was interrupted")
        | Exit.Failure cause ->
            let rendered = Cause.prettyPrint describeError cause
            let defects = Cause.defects cause

            if List.isEmpty defects then
                logger.LogError("Axial root application failed: {Cause}", rendered)
            else
                logger.LogCritical(defects.Head, "Axial root application died: {Cause}", rendered)

    interface IHostedService with
        member _.StartAsync(cancellationToken: CancellationToken) =
            try
                let environment = environmentFactory services
                let handle = App.startWithCancellation lifetime.ApplicationStopping environment application
                running <- Some handle

                monitor <-
                    async {
                        let! exit = handle.Completion
                        report exit
                        if options.StopHostOnCompletion then lifetime.StopApplication()
                    }
                    |> Async.StartAsTask
                    :> Task

                Task.CompletedTask
            with error ->
                Task.FromException error

        member _.StopAsync(cancellationToken: CancellationToken) =
            match running with
            | None -> monitor
            | Some handle ->
                async {
                    let! _ = handle.Stop()
                    do! Async.AwaitTask monitor
                }
                |> fun work -> Async.StartAsTask(work, cancellationToken = cancellationToken)
                :> Task

/// <summary>Microsoft Generic Host registration helpers for root Flow applications.</summary>
[<RequireQualifiedAccess>]
module Hosting =
    /// <summary>Registers a root application that owns the Generic Host lifetime.</summary>
    let addApp
        (environmentFactory: IServiceProvider -> 'env)
        (describeError: 'error -> string)
        (application: Flow<'env, 'error, unit>)
        (services: IServiceCollection)
        : IServiceCollection =
        if isNull services then nullArg (nameof services)

        services.AddSingleton<IHostedService>(Func<IServiceProvider, IHostedService>(fun provider ->
            new FlowHostedService<'env, 'error>(
                provider,
                environmentFactory,
                describeError,
                application,
                provider.GetRequiredService<ILogger<FlowHostedService<'env, 'error>>>(),
                provider.GetRequiredService<IHostApplicationLifetime>(),
                HostedAppOptions.Default) :> IHostedService))

    /// <summary>Registers a root application with explicit Generic Host completion options.</summary>
    let addAppWith
        (options: HostedAppOptions)
        (environmentFactory: IServiceProvider -> 'env)
        (describeError: 'error -> string)
        (application: Flow<'env, 'error, unit>)
        (services: IServiceCollection)
        : IServiceCollection =
        if isNull services then nullArg (nameof services)

        services.AddSingleton<IHostedService>(Func<IServiceProvider, IHostedService>(fun provider ->
            new FlowHostedService<'env, 'error>(
                provider,
                environmentFactory,
                describeError,
                application,
                provider.GetRequiredService<ILogger<FlowHostedService<'env, 'error>>>(),
                provider.GetRequiredService<IHostApplicationLifetime>(),
                options) :> IHostedService))

/// <summary>Standalone .NET process integration for Flow applications that do not use Generic Host.</summary>
[<RequireQualifiedAccess>]
module DotNetApp =
    /// <summary>Maps a final application exit to the conventional standalone process exit codes.</summary>
    let exitCode (exit: Exit<'value, 'error>) : int =
        match exit with
        | Exit.Success _ -> 0
        | Exit.Failure cause when Cause.defects cause |> List.isEmpty |> not -> 2
        | Exit.Failure cause when Cause.isInterrupted cause -> 130
        | Exit.Failure _ -> 1

    /// <summary>
    /// Runs a standalone application, translating Ctrl+C into coordinated stop and returning a process exit code.
    /// </summary>
    let run
        (describeError: 'error -> string)
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : Task<int> =
        task {
            let running = App.start environment application

            let handler =
                ConsoleCancelEventHandler(fun _ event ->
                    event.Cancel <- true
                    async {
                        let! _ = running.Stop()
                        return ()
                    }
                    |> Async.StartImmediate)

            Console.CancelKeyPress.AddHandler handler

            try
                let! exit = running.Completion |> Async.StartAsTask

                match exit with
                | Exit.Success _ -> ()
                | Exit.Failure cause when Cause.isInterrupted cause -> ()
                | Exit.Failure cause -> Console.Error.WriteLine(Cause.prettyPrint describeError cause)

                return exitCode exit
            finally
                Console.CancelKeyPress.RemoveHandler handler
        }
