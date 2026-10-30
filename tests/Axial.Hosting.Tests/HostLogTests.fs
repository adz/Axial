namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open Axial
open Axial.Hosting
open Axial.PlatformService
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Xunit

module HostLogTests =
    [<Fact>]
    let ``Generic Host installs its category sink for logs and forked logs`` () =
        let provider = new HostLogProvider()
        use factory = LoggerFactory.Create(fun builder ->
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace).AddProvider(provider) |> ignore)
        use services = ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider()
        let local = FiberLocal.named "job" "unset"
        let error = InvalidOperationException("host-error")
        let source: Flow<unit, string, unit> =
            (flow {
                do! Log.debug "disabled"
                do! Log.info "root-message"
                let! child = Log.errorExn error "child-message" |> Flow.forkNamed "host-log-child"
                do! Fiber.join child
            }) |> Flow.annotate "request" "req-1" |> FiberLocal.locally "job-1" local
        let lifetime = RecordingLifetime()
        let service =
            new FlowHostedService<unit, string>(services, (fun _ -> ()), id, source,
                factory.CreateLogger<FlowHostedService<unit, string>>(), lifetime, HostedAppOptions.Default)
            :> IHostedService
        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult()
        service.StopAsync(CancellationToken.None).GetAwaiter().GetResult()
        let entries = provider.Entries |> Array.filter (fun entry -> entry.Category = "Axial")
        Assert.Equal(2, entries.Length)
        Assert.Equal("root-message", entries.[0].Message)
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, entries.[0].Level)
        Assert.Equal("child-message", entries.[1].Message)
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entries.[1].Level)
        Assert.Same(error, entries.[1].Error.Value)
        for entry in entries do
            Assert.Equal(box "req-1", entry.Scope.["request"])
            Assert.Equal(box "job-1", entry.Scope.["job"])
        Assert.False((MicrosoftLogging.fromFactory "Axial" factory).IsEnabled Axial.LogLevel.Debug)
