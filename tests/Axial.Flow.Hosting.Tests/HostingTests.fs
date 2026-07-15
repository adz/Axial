namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Axial.Flow
open Axial.Flow.Hosting
open Axial.Flow.PlatformService
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

module HostingTests =
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

        let application : Flow<unit, string, unit> =
            flow {
                do! Flow.addFinalizer(fun _ -> finalized.TrySetResult() |> ignore; Task.CompletedTask)
                started.TrySetResult() |> ignore
                do! Flow.Runtime.sleep(TimeSpan.FromSeconds 30.0)
            }

        let hosted =
            new FlowHostedService<unit, string>(
                null,
                (fun _ -> ()),
                id,
                application,
                TypedLogger<FlowHostedService<unit, string>>(logger) :> ILogger<_>,
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
        let metadata =
            { Id = FiberId 9L
              Name = None
              ParentId = None
              Annotations = Map.empty
              StartedAt = DateTimeOffset.UtcNow
              SettledAt = None
              Status = FiberStatus.Failed
              Observed = false }

        observer.OnEnd metadata (Some defect)
        observer.OnUnobservedDefect (Some metadata) defect

        test <@ logger.Entries |> List.exists (fun (level, _, error) -> level = Microsoft.Extensions.Logging.LogLevel.Error && error = Some defect) @>
        test <@ logger.Entries |> List.exists (fun (level, _, error) -> level = Microsoft.Extensions.Logging.LogLevel.Critical && error = Some defect) @>
