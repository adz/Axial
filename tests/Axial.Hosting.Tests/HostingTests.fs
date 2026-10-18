namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Axial
open Axial.Hosting
open Axial.PlatformService
open Axial.Layers
open Swensen.Unquote
open Xunit

type RecordingLogger() =
    let entries = ResizeArray<Microsoft.Extensions.Logging.LogLevel * string * exn option>()
    member _.Entries = entries |> Seq.toList

    interface ILogger with
        member _.Log(level, _, state, error, _) = entries.Add(level, string state, Option.ofObj error)
        member _.IsEnabled(_) = true
        member _.BeginScope(_) = { new IDisposable with member _.Dispose() = () }

type RecordingLoggerFactory(logger: RecordingLogger) =
    interface ILoggerFactory with
        member _.AddProvider(_) = ()
        member _.CreateLogger(_) = logger
        member _.Dispose() = ()

type TypedLogger<'category>(logger: ILogger) =
    interface ILogger<'category>
    interface ILogger with
        member _.Log(level, eventId, state, error, formatter) =
            logger.Log(level, eventId, state, error, formatter)
        member _.IsEnabled level = logger.IsEnabled level
        member _.BeginScope state = logger.BeginScope state

type RecordingLifetime() =
    let started = new CancellationTokenSource()
    let stopping = new CancellationTokenSource()
    let stopped = new CancellationTokenSource()
    let mutable stopCalls = 0
    member _.StopCalls = stopCalls
    member _.MarkStarted() = started.Cancel()
    member _.MarkStopped() = stopped.Cancel()

    interface IHostApplicationLifetime with
        member _.ApplicationStarted = started.Token
        member _.ApplicationStopping = stopping.Token
        member _.ApplicationStopped = stopped.Token
        member _.StopApplication() =
            Interlocked.Increment(&stopCalls) |> ignore
            stopping.Cancel()

type ScopedProvider() =
    let entries = System.Collections.Concurrent.ConcurrentBag<string * string option>()
    let mutable scopes: IExternalScopeProvider = LoggerExternalScopeProvider() :> IExternalScopeProvider
    member _.Entries = entries |> Seq.toList
    interface ISupportExternalScope with
        member _.SetScopeProvider provider = scopes <- provider
    interface ILoggerProvider with
        member _.CreateLogger(_) =
            { new ILogger with
                member _.IsEnabled(_) = true
                member _.BeginScope state = scopes.Push state
                member _.Log(_, _, state, _, _) =
                    let mutable jobId = None
                    scopes.ForEachScope(
                        Action<obj, unit>(fun scope _ ->
                            match scope with
                            | :? IDictionary<string, obj> as values when values.ContainsKey "jobId" ->
                                jobId <- Some(string values["jobId"])
                            | _ -> ()), ())
                    entries.Add(string state, jobId) }
        member _.Dispose() = ()

module HostingTests =
    [<Fact>]
    let ``watchdog without interval completes without notifying`` () =
        let variables =
            { new IEnvironmentVariables with
                member _.TryGet _ = None
                member _.Set(_, _) = ()
                member _.Expand value = value
                member _.GetAll() = Dictionary<string, string>() }
        let mutable count = 0
        let notifier = { new ISystemdNotifier with member _.Notify _ = count <- count + 1 }
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        let result = App.run (ClockEnvironment(clock)) (Hosting.watchdog variables 1 notifier) |> Async.RunSynchronously
        test <@ result = Exit.Success () @>
        test <@ count = 0 @>

    [<Fact>]
    let ``live notifier sends unix datagram on Linux`` () =
        if OperatingSystem.IsLinux() then
            let path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "axial-notify-" + Guid.NewGuid().ToString("N"))
            use receiver = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Unspecified)
            try
                receiver.Bind(System.Net.Sockets.UnixDomainSocketEndPoint(path))
                receiver.ReceiveTimeout <- 3000
                SystemdNotify.live path |> fun notifier -> notifier.Notify "WATCHDOG=1"
                let buffer = Array.zeroCreate<byte> 64
                let received = receiver.Receive buffer
                test <@ System.Text.Encoding.UTF8.GetString(buffer, 0, received) = "WATCHDOG=1" @>
            finally
                if System.IO.File.Exists path then System.IO.File.Delete path

    [<Fact>]
    let ``named locals follow concurrent jobs and forked logs`` () =
        let provider = ScopedProvider()
        use factory = LoggerFactory.Create(fun builder -> builder.AddProvider(provider) |> ignore)
        let logger = MicrosoftLogging.fromFactory "jobs" factory
        let jobId = FiberLocal.named "jobId" "unset"
        let log message : Flow<ClockEnvironment, string, unit> =
            Flow.delay (fun () -> logger.Log LogLevel.Information message; Flow.succeed ())
        let job id =
            flow {
                do! log (id + " start")
                let! child = Flow.fork (log (id + " child"))
                do! Fiber.join child
                do! log (id + " end")
            }
            |> FiberLocal.locally id jobId
        let work = Flow.zipPar (job "job-42") (job "job-7")
        test <@ App.run (ClockEnvironment(Clock.live)) work |> Async.RunSynchronously = Exit.Success ((), ()) @>
        let entries = provider.Entries
        test <@ entries.Length = 6 @>
        for message, scope in entries do
            let expected = message.Split(' ')[0]
            test <@ scope = Some expected @>

    [<Fact>]
    let ``Microsoft logging adapter preserves levels and exceptions`` () =
        let logger = RecordingLogger()
        let log = MicrosoftLogging.create logger
        let defect = InvalidOperationException "boom"

        log.Log LogLevel.Information "started"
        log.LogException LogLevel.Error defect "failed"

        match logger.Entries with
        | [ information, first, None; error, second, Some captured ] ->
            test <@ information = Microsoft.Extensions.Logging.LogLevel.Information @>
            test <@ first.Contains "started" @>
            test <@ error = Microsoft.Extensions.Logging.LogLevel.Error @>
            test <@ second.Contains "failed" @>
            test <@ obj.ReferenceEquals(captured, defect) @>
        | other -> failwithf "Unexpected log entries: %A" other

    [<Fact>]
    let ``Generic Host adapter runs root App and requests host stop on completion`` () =
        let logger = RecordingLogger()
        let lifetime = RecordingLifetime()
        let completed = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let application : Flow<string, string, unit> =
            flow {
                let! environment = Flow.env<string, string>
                test <@ environment = "host-environment" @>
                completed.TrySetResult() |> ignore
            }

        let hosted =
            new FlowHostedService<string, string>(
                null,
                (fun _ -> "host-environment"),
                id,
                application,
                TypedLogger<FlowHostedService<string, string>>(logger) :> ILogger<_>,
                lifetime,
                HostedAppOptions.Default)
            :> IHostedService

        hosted.StartAsync(CancellationToken.None).GetAwaiter().GetResult()

        test <@ completed.Task.Wait(TimeSpan.FromSeconds 2.0) @>
        SpinWait.SpinUntil((fun () -> lifetime.StopCalls = 1), TimeSpan.FromSeconds 2.0) |> ignore
        test <@ lifetime.StopCalls = 1 @>
        hosted.StopAsync(CancellationToken.None).GetAwaiter().GetResult()

    [<Fact>]
    let ``Generic Host shutdown interrupts root App and waits for finalizer`` () =
        let logger = RecordingLogger()
        let lifetime = RecordingLifetime()
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finalized = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let application : Flow<ClockEnvironment, string, unit> =
            flow {
                do! Flow.scopeFinalizer(Flow.delay (fun () -> finalized.TrySetResult() |> ignore; Flow.succeed ()))
                started.TrySetResult() |> ignore
                do! Flow.sleep(TimeSpan.FromSeconds 30.0)
            }

        let hosted =
            new FlowHostedService<ClockEnvironment, string>(
                null,
                (fun _ -> ClockEnvironment(Clock.live)),
                id,
                application,
                TypedLogger<FlowHostedService<ClockEnvironment, string>>(logger) :> ILogger<_>,
                lifetime,
                { StopHostOnCompletion = false })
            :> IHostedService

        hosted.StartAsync(CancellationToken.None).GetAwaiter().GetResult()
        test <@ started.Task.Wait(TimeSpan.FromSeconds 2.0) @>
        (lifetime :> IHostApplicationLifetime).StopApplication()
        hosted.StopAsync(CancellationToken.None).GetAwaiter().GetResult()
        test <@ finalized.Task.IsCompletedSuccessfully @>

    [<Fact>]
    let ``standalone exit codes distinguish failure defects and interruption`` () =
        test <@ DotNetApp.exitCode (Exit.Success 1 : Exit<int, string>) = 0 @>
        test <@ DotNetApp.exitCode (Exit.Failure(Cause.Fail "bad") : Exit<int, string>) = 1 @>
        test <@ DotNetApp.exitCode (Exit.Failure(Cause.Die(InvalidOperationException "bad")) : Exit<int, string>) = 2 @>
        test <@ DotNetApp.exitCode (Exit.Failure Cause.Interrupt : Exit<int, string>) = 130 @>

module FiberLoggingTests =
    [<Fact>]
    let ``Fiber logging records defects and unobserved defects`` () =
        let logger = RecordingLogger()
        let observer = FiberLogging.observer logger
        let defect = InvalidOperationException "fiber failed"
        let metadata : FiberMetadata =
            { Id = FiberId 9L
              Name = None
              ParentId = None
              Annotations = Map.empty
              StartedAt = DateTimeOffset.UtcNow
              StartedTick = TimeSpan.Zero
              SettledAt = None
              SettledTick = None
              Status = FiberStatus.Failed }

        observer.OnEnd metadata (Some defect)
        observer.OnUnobservedDefect (Some metadata) defect

        test <@ logger.Entries |> List.exists (fun (level, _, error) -> level = Microsoft.Extensions.Logging.LogLevel.Error && error = Some defect) @>
        test <@ logger.Entries |> List.exists (fun (level, _, error) -> level = Microsoft.Extensions.Logging.LogLevel.Critical && error = Some defect) @>
