namespace Axial.Hosting

open System
open System.Threading
open System.Collections.Generic
open System.Threading.Tasks
open System.Net.Sockets
open System.Text
open System.Globalization
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Axial
open Axial.HttpClient
open Axial.PlatformService

module private ScopedLogging =
    let withScope (logger: ILogger) (action: unit -> unit) =
        match FiberLocal.current () with
        | [] -> action ()
        | locals ->
            let scope = Dictionary<string, obj>()
            for name, value in locals do scope[name] <- value
            use _scope = logger.BeginScope(scope)
            action ()

/// <summary>Adapts Microsoft.Extensions.Logging loggers to Axial's explicit logging service.</summary>
[<RequireQualifiedAccess>]
module MicrosoftLogging =
    let private write (logger: ILogger) (level: LogLevel) (error: exn option) (message: string) =
        ScopedLogging.withScope logger (fun () ->
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
            | LogLevel.Critical, Some error -> logger.LogCritical(error, message))

    /// <summary>Creates an Axial logger backed by a supplied Microsoft logger.</summary>
    let sink (logger: ILogger) : ILogSink =
        if isNull logger then nullArg (nameof logger)
        { new ILogSink with
            member _.IsEnabled level =
                logger.IsEnabled(
                    match level with
                    | LogLevel.Trace -> Microsoft.Extensions.Logging.LogLevel.Trace
                    | LogLevel.Debug -> Microsoft.Extensions.Logging.LogLevel.Debug
                    | LogLevel.Information -> Microsoft.Extensions.Logging.LogLevel.Information
                    | LogLevel.Warning -> Microsoft.Extensions.Logging.LogLevel.Warning
                    | LogLevel.Error -> Microsoft.Extensions.Logging.LogLevel.Error
                    | LogLevel.Critical -> Microsoft.Extensions.Logging.LogLevel.Critical)
            member _.Write entry =
                let scope = Dictionary<string, obj>()
                for name, value in entry.Locals do scope[name] <- value
                for KeyValue(name, value) in entry.Annotations do scope[name] <- box value
                use _scope = logger.BeginScope(scope)
                write logger entry.Level entry.Error entry.Message }

    /// <summary>Creates an Axial logger with an explicit Microsoft logging category.</summary>
    let fromFactory (categoryName: string) (loggerFactory: ILoggerFactory) : ILogSink =
        if String.IsNullOrWhiteSpace categoryName then invalidArg (nameof categoryName) "A logging category is required."
        if isNull loggerFactory then nullArg (nameof loggerFactory)
        sink (loggerFactory.CreateLogger categoryName)

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
                        ScopedLogging.withScope logger (fun () ->
                            logger.LogError(error, "Fiber {FiberId} died with a defect", metadata.Id.Value)))
            OnUnobservedDefect =
                fun metadata defect ->
                    ScopedLogging.withScope logger (fun () ->
                        match metadata with
                        | Some fiber ->
                            logger.LogCritical(defect, "Unobserved fiber defect (fiber {FiberId})", fiber.Id.Value)
                        | None ->
                            logger.LogCritical(defect, "Unobserved defect from a discarded race/timeout loser")) }

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

        // Waits for the root application and every finalizer. The host's token ends the wait only when the host's
        // own shutdown timeout expires; cleanup still running then is reported, never silently abandoned.
        member _.StopAsync(cancellationToken: CancellationToken) =
            match running with
            | None -> monitor
            | Some handle ->
                let stopped =
                    async {
                        let! _ = handle.Stop()
                        do! Async.AwaitTask monitor
                    }
                    |> Async.StartAsTask

                let wait : Flow<unit, Never, unit> =
                    flow {
                        let! abandoned = Deferred.make ()
                        use registration = cancellationToken.Register(fun () -> Deferred.succeedNow () abandoned |> ignore)
                        // Only this wait is cancellable; the already-started stop retains ownership of cleanup.
                        let! stoppedSignal = Deferred.fromTask stopped
                        let completed =
                            Deferred.await stoppedSignal
                            |> Flow.recover Flow.die
                            |> Flow.map (fun () -> true)
                        let! completedFirst = Flow.race completed (Deferred.await abandoned |> Flow.map (fun () -> false))
                        if not completedFirst then
                            logger.LogWarning(
                                "Axial root application cleanup was still running when the host shutdown timeout expired; the process may exit before its finalizers finish"
                            )
                    }

                async {
                    let! exit = Flow.toAsync () wait
                    return exit |> Exit.toResult |> Result.defaultWith Never.absurd
                }
                |> Async.StartAsTask
                :> Task

/// <summary>Transport for systemd notification datagrams.</summary>
type ISystemdNotifier =
    /// <summary>Sends a notification payload to the service manager.</summary>
    /// <param name="message">The systemd notification payload.</param>
    abstract Notify: message: string -> unit

/// <summary>Systemd notification transport with an explicitly supplied socket address.</summary>
[<RequireQualifiedAccess>]
module SystemdNotify =
    /// <summary>Creates a notifier for a filesystem or abstract (@-prefixed) Unix datagram socket.</summary>
    let live (socketPath: string) : ISystemdNotifier =
        if String.IsNullOrEmpty socketPath then invalidArg (nameof socketPath) "A notification socket is required."
        let address = if socketPath[0] = '@' then "\u0000" + socketPath.Substring(1) else socketPath
        { new ISystemdNotifier with
            member _.Notify message =
                // This live transport is the explicit socket-effect boundary for ISystemdNotifier.
                use socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified)
                let payload = Encoding.UTF8.GetBytes message
                socket.Blocking <- false
                try
                    socket.SendTo(payload, UnixDomainSocketEndPoint(address)) |> ignore
                with :? SocketException as error when error.SocketErrorCode = SocketError.WouldBlock ->
                    // A full notify queue drops this heartbeat; the next interval retries.
                    () }

/// <summary>Microsoft Generic Host registration helpers for root Flow applications.</summary>
[<RequireQualifiedAccess>]
module Hosting =
    /// <summary>Runs a scheduler heartbeat gated by an optional application health flow. Requires a clock and logger in the environment.</summary>
    let watchdogWithHeartbeat<'env>
        (heartbeatGate: Flow<'env, Never, unit>)
        (variables: IEnvironmentVariables)
        (processId: int)
        (notifier: ISystemdNotifier)
        (isHealthy: Flow<'env, Never, bool>) : Flow<'env, Never, unit> =
        flow {
            match variables.TryGet "WATCHDOG_USEC" with
            | None -> return ()
            | Some value ->
                match UInt64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture) with
                | false, _
                | true, 0UL ->
                    do! Log.warning "Invalid WATCHDOG_USEC; watchdog disabled"
                | true, microseconds when microseconds > uint64 TimeSpan.MaxValue.Ticks / 5UL ->
                    do! Log.warning "WATCHDOG_USEC exceeds TimeSpan range; watchdog disabled"
                | true, microseconds ->
                    let enabled =
                        match variables.TryGet "WATCHDOG_PID" with
                        | None -> true
                        | Some pid -> pid = string processId
                    if enabled then
                        let interval = TimeSpan.FromTicks(max 1L (int64 microseconds * 5L))
                        let heartbeatInterval = TimeSpan.FromTicks(max 1L (interval.Ticks / 2L))
                        let mutable progress = 0L
                        let rec heartbeat () = flow {
                            do! Flow.sleep heartbeatInterval
                            do! heartbeatGate
                            Interlocked.Increment(&progress) |> ignore
                            return! heartbeat ()
                        }
                        // The enclosing Flow scope owns and interrupts the heartbeat on cancellation.
                        let! _ = Flow.fork (heartbeat ())
                        let rec ping last = flow {
                            do! Flow.sleep interval
                            let current = Interlocked.Read(&progress)
                            if current <> last then
                                let! healthy = isHealthy
                                if healthy then notifier.Notify "WATCHDOG=1"
                            return! ping current
                        }
                        return! ping (Interlocked.Read(&progress))
        }

    /// <summary>Runs the scheduler heartbeat with an application-owned progress check; use this to detect stalled work.</summary>
    let watchdogWith<'env>
        (variables: IEnvironmentVariables) (processId: int) (notifier: ISystemdNotifier)
        (isHealthy: Flow<'env, Never, bool>) : Flow<'env, Never, unit> =
        watchdogWithHeartbeat (Flow.succeed ()) variables processId notifier isHealthy

    /// <summary>Detects a wedged Axial scheduler or starved thread pool; supply watchdogWith isHealthy to tie the watchdog to your application's progress.</summary>
    let watchdog<'env>
        (variables: IEnvironmentVariables) (processId: int) (notifier: ISystemdNotifier) : Flow<'env, Never, unit> =
        watchdogWith variables processId notifier (Flow.succeed true)

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

            // The console host's Ctrl+C wiring and final-error report are the OS-process entry point
            // boundary itself, not application logic; there is no environment to route them through.
            Console.CancelKeyPress.AddHandler handler // axial-allow-AXG001: intentional console host or live-service boundary

            try
                let! exit = running.Completion |> Async.StartAsTask

                match exit with
                | Exit.Success _ -> ()
                | Exit.Failure cause when Cause.isInterrupted cause -> ()
                | Exit.Failure cause -> Console.Error.WriteLine(Cause.prettyPrint describeError cause) // axial-allow-AXG001: intentional console host or live-service boundary

                return exitCode exit
            finally
                Console.CancelKeyPress.RemoveHandler handler // axial-allow-AXG001: intentional console host or live-service boundary
        }

/// <summary>Adapts Microsoft configuration to Axial's explicit environment-variable service.</summary>
[<RequireQualifiedAccess>]
module Configuration =
    let private flatten (configuration: IConfiguration) : (string * string) list =
        let rec walk (prefix: string) (section: IConfiguration) =
            [ for child in section.GetChildren() do
                  let key = if prefix = "" then child.Key else prefix + ":" + child.Key
                  let children = child.GetChildren()

                  if Seq.isEmpty children then
                      yield key, (if isNull child.Value then "" else child.Value)
                  else
                      yield! walk key child ]

        walk "" configuration

    /// <summary>Creates an explicit environment-variable service backed by a Microsoft configuration.</summary>
    let environmentVariables (configuration: IConfiguration) : IEnvironmentVariables =
        if isNull configuration then nullArg (nameof configuration)

        let lookup =
            let dictionary = System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

            for key, value in flatten configuration do
                dictionary[key] <- value

            dictionary

        { new IEnvironmentVariables with
            member _.TryGet name = configuration[name] |> Option.ofObj

            member _.Set(_, _) =
                raise (NotSupportedException("Configuration is read-only; provide environment variables explicitly."))

            member _.Expand text =
                lookup
                |> Seq.fold (fun (result: string) pair -> result.Replace("%" + pair.Key + "%", pair.Value)) text

            member _.GetAll() = lookup :> System.Collections.Generic.IReadOnlyDictionary<string, string> }

    /// <summary>Builds the environment-variable service from a Microsoft configuration supplied at composition.</summary>
    let layer (configuration: IConfiguration) : Layer<unit, Never, IEnvironmentVariables> =
        Layer.succeed (environmentVariables configuration)

/// <summary>Adapts <see cref="T:Microsoft.Extensions.Http.IHttpClientFactory" /> to Axial's HTTP service.</summary>
[<RequireQualifiedAccess>]
module HttpFactory =
    /// <summary>Creates an Axial HTTP service that obtains a client per send from the factory.</summary>
    let live (clock: IClock) (factory: System.Net.Http.IHttpClientFactory) : IHttp =
        if isNull factory then nullArg (nameof factory)

        { new IHttp with
            member _.Send(request) =
                (Axial.HttpClient.Http.live clock (factory.CreateClient(""))).Send(request) }
