namespace FsFlow.Hosting

open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open FsFlow
open FsFlow.Services.Core

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
                let inner = loggerFactory.CreateLogger("FsFlow")

                { new ILog with
                    member _.Log level message =
                        match level with
                        | LogLevel.Trace -> inner.LogTrace message
                        | LogLevel.Debug -> inner.LogDebug message
                        | LogLevel.Information -> inner.LogInformation message
                        | LogLevel.Warning -> inner.LogWarning message
                        | LogLevel.Error -> inner.LogError message
                        | LogLevel.Critical -> inner.LogCritical message }
            | _ ->
                Log.live

        {
            Clock = LiveClock() :> IClock
            Log = logger
            Random = Random.live
            Guid = Guid.live
            EnvironmentVariables = EnvironmentVariables.live
        }

    /// <summary>Executes a flow that depends on the standard base runtime using services from the provided <see cref="T:System.IServiceProvider" />.</summary>
    let run (sp: IServiceProvider) (flow: Flow<BaseRuntime, 'error, 'value>) : Effect<'value, 'error> =
        Flow.run (createBaseRuntime sp) flow

[<RequireQualifiedAccess>]
module Startup =
    /// <summary>Validates that all required environment variables are present and valid using the live base runtime.</summary>
    let validateEnvironment (flow: Flow<BaseRuntime, EnvironmentVariableError, 'v>) : Result<'v, string list> =
        match (Flow.run BaseRuntime.liveValue flow).AsTask().GetAwaiter().GetResult() with
        | Exit.Success v -> Ok v
        | Exit.Failure (Cause.Fail e) -> Error [ EnvironmentVariableErrors.describe e ]
        | Exit.Failure Cause.Interrupt -> Error [ "Validation was interrupted" ]
        | Exit.Failure (Cause.Die ex) -> Error [ ex.Message ]
