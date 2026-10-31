namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Hosting
open Axial.PlatformService
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Xunit

module HostLogTests =
    [<Fact>]
    let ``Microsoft sink skips empty scopes but maps annotations when present`` () =
        let scopes = ResizeArray<IDictionary<string, obj>>()
        let mutable writes = 0
        let logger =
            { new ILogger with
                member _.IsEnabled _ = true
                member _.Log(_, _, _, _, _) = writes <- writes + 1
                member _.BeginScope state =
                    scopes.Add(unbox<IDictionary<string, obj>> state)
                    { new IDisposable with member _.Dispose() = () } }
        let sink = MicrosoftLogging.sink logger
        let bare: Flow<unit, Never, unit> = Log.info "bare"
        bare |> Flow.withLogSink sink |> Flow.run () |> ignore
        Assert.Empty(scopes)
        bare |> Flow.annotate "trace_id" "trace-7" |> Flow.withLogSink sink |> Flow.run () |> ignore
        Assert.Equal(2, writes)
        Assert.Equal(box "trace-7", (Assert.Single scopes).["trace_id"])

    [<Fact>]
    let ``Generic Host installs its category sink for logs and forked logs`` () =
        let provider = new HostLogProvider()
        use factory = LoggerFactory.Create(fun builder ->
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace).AddProvider(provider) |> ignore)
        use services = ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider()
        let local = FiberLocal.named "job" "unset"
        let error = InvalidOperationException("host-error")
        let finished = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let notifier = { new ISystemdNotifier with member _.Notify _ = failwith "invalid watchdog must not notify" }
        let source: Flow<unit, string, unit> =
            (flow {
                do! Log.debug "disabled"
                do! Log.info "root-message"
                let! child = Log.errorExn error "child-message" |> Flow.forkNamed "host-log-child"
                do! Fiber.join child
                do! Hosting.watchdog (function "WATCHDOG_USEC" -> Some "invalid" | _ -> None) 1 notifier |> Flow.widenError
                finished.TrySetResult() |> ignore
            }) |> Flow.annotate "request" "req-1" |> FiberLocal.locally "job-1" local
        let lifetime = RecordingLifetime()
        let service =
            new FlowHostedService<unit, string>(services, (fun _ -> ()), id, source,
                factory.CreateLogger<FlowHostedService<unit, string>>(), lifetime, HostedAppOptions.Default)
            :> IHostedService
        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult()
        Assert.True(finished.Task.Wait(TimeSpan.FromSeconds 5.), "Hosted logging did not complete")
        service.StopAsync(CancellationToken.None).GetAwaiter().GetResult()
        let entries = provider.Entries |> Array.filter (fun entry -> entry.Category = "Axial")
        Assert.Equal(3, entries.Length)
        Assert.Equal("root-message", entries.[0].Message)
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, entries.[0].Level)
        Assert.Equal("child-message", entries.[1].Message)
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entries.[1].Level)
        Assert.Same(error, entries.[1].Error.Value)
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, entries.[2].Level)
        Assert.Contains("Invalid WATCHDOG_USEC", entries.[2].Message)
        for entry in entries do
            Assert.Equal(box "req-1", entry.Scope.["request"])
            Assert.Equal(box "job-1", entry.Scope.["job"])
        Assert.False((MicrosoftLogging.fromFactory "Axial" factory).IsEnabled Axial.LogLevel.Debug)

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``Generic Host sink options replace or silence and inner scopes win`` silent =
        use factory = LoggerFactory.Create(fun _ -> ())
        use services = ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider()
        let outer = Log.memory ()
        let inner = Log.memory ()
        let installed = if silent then LogSink.none else LogSink.minLevel Axial.LogLevel.Information outer
        let finished = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let source: Flow<unit, string, unit> = flow {
            do! Log.debug "disabled"
            do! Log.info "outer"
            let! child = Log.info "inner" |> Flow.withLogSink inner |> Flow.fork
            do! Fiber.join child
            finished.TrySetResult() |> ignore
        }
        let options =
            { HostedAppOptions.Default with
                StopHostOnCompletion = false
                LogSink = Some(fun supplied -> Assert.Same(factory, supplied); installed) }
        let service = new FlowHostedService<unit, string>(services, (fun _ -> ()), id, source,
                            factory.CreateLogger<FlowHostedService<unit, string>>(), RecordingLifetime(), options) :> IHostedService
        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult()
        Assert.True(finished.Task.Wait(TimeSpan.FromSeconds 5.), "Hosted logging did not complete")
        service.StopAsync(CancellationToken.None).GetAwaiter().GetResult()
        Assert.Equal((if silent then 0 else 1), outer.Entries.Count)
        Assert.Equal("inner", Assert.Single(inner.Entries).Message)

    [<Fact>]
    let ``Generic Host no installation option needs no logger factory`` () =
        let memory = Log.memory ()
        let finished = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let source: Flow<unit, string, unit> =
            (flow {
                do! Log.info "manual"
                finished.TrySetResult() |> ignore
            }) |> Flow.withLogSink memory
        let logger = RecordingLogger()
        let service = new FlowHostedService<unit, string>(null, (fun _ -> ()), id, source,
                            TypedLogger<FlowHostedService<unit, string>>(logger), RecordingLifetime(),
                            { HostedAppOptions.Default with LogSink = None }) :> IHostedService
        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult()
        Assert.True(finished.Task.Wait(TimeSpan.FromSeconds 5.), "Hosted logging did not complete")
        service.StopAsync(CancellationToken.None).GetAwaiter().GetResult()
        Assert.Equal("manual", Assert.Single(memory.Entries).Message)
