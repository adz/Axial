namespace Axial.Flow.Hosting

open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Axial.Flow
open Axial.Flow.PlatformService

/// <summary>
/// A live clock implementation that uses <see cref="P:System.DateTimeOffset.UtcNow" />.
/// </summary>
type LiveClock() =
    interface IClock with
        member _.UtcNow() = DateTimeOffset.UtcNow

[<RequireQualifiedAccess>]
module Hosting =
    /// <summary>Creates the standard service bundle for host execution.</summary>
    let createBaseRuntime (sp: IServiceProvider) : BaseRuntime =
        let logger =
            match sp.GetService(typeof<ILoggerFactory>) with
            | :? ILoggerFactory as loggerFactory ->
                let inner = loggerFactory.CreateLogger("Axial.Flow")

                { new ILog with
                    member _.Log level message =
                        match level with
                        | LogLevel.Trace -> inner.LogTrace message
                        | LogLevel.Debug -> inner.LogDebug message
                        | LogLevel.Information -> inner.LogInformation message
                        | LogLevel.Warning -> inner.LogWarning message
                        | LogLevel.Error -> inner.LogError message
                        | LogLevel.Critical -> inner.LogCritical message

                    member _.LogException level error message =
                        match level with
                        | LogLevel.Trace -> inner.LogTrace(error, message)
                        | LogLevel.Debug -> inner.LogDebug(error, message)
                        | LogLevel.Information -> inner.LogInformation(error, message)
                        | LogLevel.Warning -> inner.LogWarning(error, message)
                        | LogLevel.Error -> inner.LogError(error, message)
                        | LogLevel.Critical -> inner.LogCritical(error, message) }
            | _ ->
                Log.live

        {
            Clock = LiveClock() :> IClock
            Log = logger
            Random = Random.live
            Guid = Guid.live
            EnvironmentVariables = EnvironmentVariables.live
        }

/// <summary>Fiber-lifecycle logging through <c>Microsoft.Extensions.Logging</c>.</summary>
[<RequireQualifiedAccess>]
module FiberLogging =
    /// <summary>
    /// A fiber observer that logs fiber defects as errors and unobserved defects as critical entries through
    /// the supplied logger. Stack with telemetry observers via <c>FiberObserver.compose</c>.
    /// </summary>
    /// <param name="logger">The host logger to write through.</param>
    let observer (logger: ILogger) : FiberObserver =
        { FiberObserver.none with
            OnEnd =
                fun metadata defect ->
                    match defect with
                    | Some error ->
                        logger.LogError(error, "Fiber {FiberId} died with a defect", metadata.Id.Value)
                    | None -> ()
            OnUnobservedDefect =
                fun metadata defect ->
                    match metadata with
                    | Some m ->
                        logger.LogCritical(defect, "Unobserved fiber defect (fiber {FiberId})", m.Id.Value)
                    | None ->
                        logger.LogCritical(defect, "Unobserved defect from a discarded race/timeout loser") }

    /// <summary>Installs the fiber-logging observer on a flow, typically once at the application edge.</summary>
    /// <param name="logger">The host logger to write through.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose fiber defects are written through the host logger.</returns>
    let observe (logger: ILogger) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow.withFiberObserver (observer logger) flow

[<RequireQualifiedAccess>]
module Startup =
    /// <summary>Validates that all required environment variables are present and valid using the live base runtime.</summary>
    let validateEnvironment (flow: Flow<BaseRuntime, EnvironmentVariableError, 'v>) : Result<'v, string list> =
        match flow.RunSynchronously(BaseRuntime.liveValue) with
        | Exit.Success v -> Ok v
        | Exit.Failure (Cause.Fail e) -> Error [ EnvironmentVariableErrors.describe e ]
        | Exit.Failure Cause.Interrupt -> Error [ "Validation was interrupted" ]
        | Exit.Failure (Cause.Die ex) -> Error [ ex.Message ]
        | Exit.Failure cause -> Error [ Cause.prettyPrint EnvironmentVariableErrors.describe cause ]
