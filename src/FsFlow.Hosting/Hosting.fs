namespace FsFlow.Hosting

open System
open System.Threading
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open FsFlow
open FsFlow.Capabilities.Core

/// <summary>
/// Adapts a standard <see cref="T:Microsoft.Extensions.Logging.ILogger" /> to the FsFlow log entry sink.
/// </summary>
type FsFlowLogger(logger: ILogger) =
    member _.Log(entry: LogEntry) =
        let level =
            match entry.Level with
            | LogLevel.Trace -> Microsoft.Extensions.Logging.LogLevel.Trace
            | LogLevel.Debug -> Microsoft.Extensions.Logging.LogLevel.Debug
            | LogLevel.Information -> Microsoft.Extensions.Logging.LogLevel.Information
            | LogLevel.Warning -> Microsoft.Extensions.Logging.LogLevel.Warning
            | LogLevel.Error -> Microsoft.Extensions.Logging.LogLevel.Error
            | LogLevel.Critical -> Microsoft.Extensions.Logging.LogLevel.Critical
        
        logger.Log(level, entry.Message)

/// <summary>
/// A live clock implementation that uses <see cref="P:System.DateTimeOffset.UtcNow" />.
/// </summary>
type LiveClock() =
    interface IClock with
        member _.UtcNow() = DateTimeOffset.UtcNow

/// <summary>
/// A standard host that carries common operational services.
/// </summary>
type DefaultHost =
    {
        Clock: IClock
        Logger: LogEntry -> unit
    }

[<RequireQualifiedAccess>]
module Hosting =
    /// <summary>Creates a default host from an <see cref="T:System.IServiceProvider" />.</summary>
    let createHost (sp: IServiceProvider) : DefaultHost =
        let loggerFactory = sp.GetRequiredService<ILoggerFactory>()
        let logger = loggerFactory.CreateLogger("FsFlow")
        let fsLogger = FsFlowLogger(logger)
        
        {
            Clock = LiveClock()
            Logger = fsLogger.Log
        }

    /// <summary>Executes a flow using services from the provided <see cref="T:System.IServiceProvider" />.</summary>
    let run (sp: IServiceProvider) (appEnv: 'appEnv) (flow: Flow<HostContext<DefaultHost, 'appEnv>, 'error, 'value>) : Effect<'value, 'error> =
        let host = createHost sp
        let context = HostContext.create host appEnv CancellationToken.None
        Flow.runFull context CancellationToken.None flow

[<RequireQualifiedAccess>]
module Startup =
    /// <summary>Validates that all required environment variables are present and valid using the process environment.</summary>
    let validateEnvironment (flow: Flow<IEnvironmentVariables, EnvironmentVariableError, 'v>) : Result<'v, string list> =
        let envVars = EnvironmentVariables.live
        match (Flow.run envVars flow).AsTask().GetAwaiter().GetResult() with
        | Exit.Success v -> Ok v
        | Exit.Failure (Cause.Fail e) -> Error [ EnvironmentVariableErrors.describe e ]
        | Exit.Failure Cause.Interrupt -> Error [ "Validation was interrupted" ]
        | Exit.Failure (Cause.Die ex) -> Error [ ex.Message ]
