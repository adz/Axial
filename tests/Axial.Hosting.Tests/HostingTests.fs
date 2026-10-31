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
    let private startWatchdog clock sink source =
        source |> Flow.withClock clock |> Flow.withLogSink sink |> App.start ()
    let private runWatchdog clock sink source =
        source |> Flow.withClock clock |> Flow.withLogSink sink |> App.run ()
    let private variables (entries: (string * string) list) =
        let values = Dictionary<string, string>(entries |> Seq.map (fun (key, value) -> KeyValuePair(key, value)))
        { new IEnvironmentVariables with
            member _.TryGet key = match values.TryGetValue key with true, value -> Some value | _ -> None
            member _.Set(_, _) = ()
            member _.Expand value = value
            member _.GetAll() = values }

    let private recordingNotifier () =
        let messages = System.Collections.Concurrent.ConcurrentQueue<string>()
        let notifier = { new ISystemdNotifier with member _.Notify message = messages.Enqueue message }
        notifier, messages

    let private advance (clock: ManualClock) sleepers (messages: System.Collections.Concurrent.ConcurrentQueue<string>) expected =
        Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers >= sleepers), 5000), "watchdog did not arm its timers")
        clock.AdvanceBy(TimeSpan.FromMilliseconds 500.0)
        Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers >= sleepers), 5000), "heartbeat did not rearm")
        clock.AdvanceBy(TimeSpan.FromMilliseconds 500.0)
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count >= expected && clock.Sleepers >= sleepers), 5000), $"pings={messages.Count} sleepers={clock.Sleepers}")
        Assert.Equal(expected, messages.Count)

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
        let result = runWatchdog clock LogSink.none (Hosting.watchdog variables 1 notifier) |> Async.RunSynchronously
        test <@ result = Exit.Success () @>
        test <@ count = 0 @>

    [<Fact>]
    let ``missing watchdog interval stays idle for ten virtual seconds`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        let notifier, messages = recordingNotifier ()
        let running = startWatchdog clock LogSink.none (Hosting.watchdog (variables []) 42 notifier)
        clock.AdvanceBy(TimeSpan.FromSeconds 10.0)
        Assert.Equal(Exit.Success (), running.Completion |> Async.RunSynchronously)
        Assert.Empty(messages)

    [<Fact>]
    let ``healthy watchdog pings once per second`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        let notifier, messages = recordingNotifier ()
        let running = startWatchdog clock LogSink.none (Hosting.watchdog (variables ["WATCHDOG_USEC", "2000000"]) 42 notifier)
        try
            for step in 1 .. 5 do advance clock 2 messages step
            Assert.Equal<string>(Array.create 5 "WATCHDOG=1", messages.ToArray())
        finally
            running.Stop() |> Async.RunSynchronously |> ignore

    [<Fact>]
    let ``unhealthy watchdog stops pinging`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        let notifier, messages = recordingNotifier ()
        let mutable checks = 0
        let health : Flow<unit, Never, bool> =
            Flow.delay (fun () -> checks <- checks + 1; Flow.succeed (checks <= 2))
        let running = startWatchdog clock LogSink.none (Hosting.watchdogWith (variables ["WATCHDOG_USEC", "2000000"]) 42 notifier health)
        try
            for step in 1 .. 2 do advance clock 2 messages step
            for _ in 3 .. 5 do
                Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers >= 2), 5000))
                clock.AdvanceBy(TimeSpan.FromMilliseconds 500.0)
                Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers >= 2), 5000))
                clock.AdvanceBy(TimeSpan.FromMilliseconds 500.0)
                Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers >= 2), 5000))
            Assert.Equal(5, checks)
            Assert.Equal(2, messages.Count)
        finally
            running.Stop() |> Async.RunSynchronously |> ignore

    [<Fact>]
    let ``blocked heartbeat suspends pings until released`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        let notifier, messages = recordingNotifier ()
        let gate = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let heartbeat : Flow<unit, Never, unit> = Flow.fromTask (fun token -> gate.Task.WaitAsync token)
        let running = startWatchdog clock LogSink.none (Hosting.watchdogWithHeartbeat heartbeat (variables ["WATCHDOG_USEC", "2000000"]) 42 notifier (Flow.succeed true))
        try
            Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers >= 2), 5000))
            clock.AdvanceBy(TimeSpan.FromMilliseconds 500.0)
            Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers = 1), 5000))
            clock.AdvanceBy(TimeSpan.FromMilliseconds 500.0)
            Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers = 1), 5000))
            Assert.Empty(messages)
            gate.SetResult()
            Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers >= 2), 5000))
            advance clock 2 messages 1
        finally
            running.Stop() |> Async.RunSynchronously |> ignore

    [<Fact>]
    let ``watchdog for another PID never pings`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        let notifier, messages = recordingNotifier ()
        let running = startWatchdog clock LogSink.none (Hosting.watchdog (variables ["WATCHDOG_USEC", "2000000"; "WATCHDOG_PID", "99"]) 42 notifier)
        clock.AdvanceBy(TimeSpan.FromSeconds 10.0)
        Assert.Equal(Exit.Success (), running.Completion |> Async.RunSynchronously)
        Assert.Empty(messages)

    [<Theory>]
    [<InlineData("abc")>]
    [<InlineData("0")>]
    [<InlineData("18446744073709551615")>]
    let ``invalid watchdog interval logs warning and disables watchdog`` value =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        let warnings = ResizeArray<string>()
        let logger =
            LogSink.fromFunction (fun entry -> if entry.Level = Axial.LogLevel.Warning then warnings.Add entry.Message)
        let notifier, messages = recordingNotifier ()
        let result = runWatchdog clock logger (Hosting.watchdog (variables ["WATCHDOG_USEC", value]) 42 notifier)
                     |> Async.RunSynchronously
        Assert.Equal(Exit.Success (), result)
        Assert.Single(warnings) |> ignore
        Assert.Empty(messages)

    [<Fact>]
    let ``full notify queue does not block or throw`` () =
        if OperatingSystem.IsLinux() then
            let path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "axial-notify-" + Guid.NewGuid().ToString("N"))
            use receiver = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Unspecified)
            try
                receiver.Bind(System.Net.Sockets.UnixDomainSocketEndPoint(path))
                let notifier = SystemdNotify.live path
                use sender = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Unspecified)
                sender.Blocking <- false
                let payload = Array.zeroCreate<byte> 64
                let mutable full = false
                while not full do
                    try sender.SendTo(payload, System.Net.Sockets.UnixDomainSocketEndPoint(path)) |> ignore
                    with :? System.Net.Sockets.SocketException as error when error.SocketErrorCode = System.Net.Sockets.SocketError.WouldBlock -> full <- true
                let pending = Task.Run(fun () -> notifier.Notify "WATCHDOG=1")
                Assert.True(pending.Wait(TimeSpan.FromSeconds 2.), "notify blocked on full queue")
            finally
                if System.IO.File.Exists path then System.IO.File.Delete path

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
        let provider = new ScopedProvider()
        use factory = LoggerFactory.Create(fun builder -> builder.AddProvider(provider) |> ignore)
        let logger = MicrosoftLogging.fromFactory "jobs" factory
        let jobId = FiberLocal.named "jobId" "unset"
        let log message : Flow<unit, string, unit> = Log.info message
        let job id =
            flow {
                do! log (id + " start")
                let! child = Flow.fork (log (id + " child"))
                do! Fiber.join child
                do! log (id + " end")
            }
            |> FiberLocal.locally id jobId
        let work = Flow.zipPar (job "job-42") (job "job-7")
        test <@ App.run () (work |> Flow.withLogSink logger) |> Async.RunSynchronously = Exit.Success ((), ()) @>
        let entries = provider.Entries
        test <@ entries.Length = 6 @>
        for message, scope in entries do
            let expected = message.Split(' ')[0]
            test <@ scope = Some expected @>

    [<Fact>]
    let ``Microsoft logging sink uses entry snapshots rather than current locals`` () =
        let provider = new ScopedProvider()
        use factory = LoggerFactory.Create(fun builder -> builder.AddProvider(provider) |> ignore)
        let sink = MicrosoftLogging.fromFactory "snapshots" factory
        let jobId = FiberLocal.named "jobId" "unset"
        let entry =
            { Level = Axial.LogLevel.Information
              Message = "captured"
              Error = None
              Annotations = Map.empty
              Locals = [ "jobId", box "captured-job" ] }
        let source : Flow<unit, Never, unit> =
            Flow.delay (fun () -> sink.Write entry; Flow.ok ())
            |> FiberLocal.locally "different-job" jobId
        source |> App.run () |> Async.RunSynchronously |> ignore
        Assert.Equal(Some "captured-job", (Assert.Single(provider.Entries) |> snd))

    [<Fact>]
    let ``Microsoft logging adapter preserves levels and exceptions`` () =
        let logger = RecordingLogger()
        let log = MicrosoftLogging.sink logger
        let defect = InvalidOperationException "boom"

        (flow {
            do! Log.info "started"
            do! Log.errorExn defect "failed"
        }) |> Flow.withLogSink log |> App.run () |> Async.RunSynchronously |> ignore

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
                { HostedAppOptions.Default with LogSink = None })
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

        let application : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer(Flow.delay (fun () -> finalized.TrySetResult() |> ignore; Flow.succeed ()))
                started.TrySetResult() |> ignore
                do! Flow.sleep(TimeSpan.FromSeconds 30.0)
            }

        let hosted =
            new FlowHostedService<unit, string>(
                null,
                (fun _ -> ()),
                id,
                application,
                TypedLogger<FlowHostedService<unit, string>>(logger) :> ILogger<_>,
                lifetime,
                { StopHostOnCompletion = false; LogSink = None })
            :> IHostedService

        hosted.StartAsync(CancellationToken.None).GetAwaiter().GetResult()
        test <@ started.Task.Wait(TimeSpan.FromSeconds 2.0) @>
        (lifetime :> IHostApplicationLifetime).StopApplication()
        hosted.StopAsync(CancellationToken.None).GetAwaiter().GetResult()
        test <@ finalized.Task.IsCompletedSuccessfully @>

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``Generic Host shutdown waits for cleanup unless its token abandons the wait`` abandon =
        let logger = RecordingLogger()
        let lifetime = RecordingLifetime()
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finalizing = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let releaseFinalizer = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let application : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer(Flow.fromTask (fun _ -> task { finalizing.TrySetResult() |> ignore; do! releaseFinalizer.Task }))
                started.TrySetResult() |> ignore
                do! Flow.sleep(TimeSpan.FromSeconds 30.0)
            }

        let hosted =
            new FlowHostedService<unit, string>(
                null,
                (fun _ -> ()),
                id,
                application,
                TypedLogger<FlowHostedService<unit, string>>(logger) :> ILogger<_>,
                lifetime,
                { StopHostOnCompletion = false; LogSink = None })
            :> IHostedService

        try
            hosted.StartAsync(CancellationToken.None).GetAwaiter().GetResult()
            test <@ started.Task.Wait(TimeSpan.FromSeconds 2.0) @>
            use shutdownTimeout = new CancellationTokenSource()
            let stopping = hosted.StopAsync(shutdownTimeout.Token)
            test <@ finalizing.Task.Wait(TimeSpan.FromSeconds 2.0) @>
            // The finalizer is still running, so stopping waits for it.
            test <@ not stopping.IsCompleted @>

            if abandon then shutdownTimeout.Cancel()
            else releaseFinalizer.TrySetResult() |> ignore
            test <@ stopping.Wait(TimeSpan.FromSeconds 2.0) @>
            let warned = logger.Entries |> List.exists (fun (level, message, _) -> level = Microsoft.Extensions.Logging.LogLevel.Warning && message.Contains "still running")
            test <@ warned = abandon @>
        finally
            releaseFinalizer.TrySetResult() |> ignore

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
