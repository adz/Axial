namespace Axial.Tests

open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Axial.Flow
open Axial.Flow.Hosting
open Axial.Flow.PlatformService
open Swensen.Unquote
open Xunit

type RecordingLogger() =
    let entries = ResizeArray<Microsoft.Extensions.Logging.LogLevel * string>()
    member _.Entries = entries |> Seq.toList
    interface ILogger with
        member _.Log(level, _, state, _, _) = entries.Add(level, string state)
        member _.IsEnabled(_) = true
        member _.BeginScope(_) = { new IDisposable with member _.Dispose() = () }

type RecordingLoggerFactory(logger: RecordingLogger) =
    interface ILoggerFactory with
        member _.AddProvider(_) = ()
        member _.CreateLogger(_) = logger
        member _.Dispose() = ()

module HostingTests =
    [<Fact>]
    let ``BaseRuntime fromServiceProvider provides the standard base runtime from IServiceProvider`` () =
        let innerLogger = RecordingLogger()
        let loggerFactory = new RecordingLoggerFactory(innerLogger) :> ILoggerFactory
        let sp =
            { new IServiceProvider with
                member _.GetService(requestedType) =
                    if requestedType = typeof<ILoggerFactory> then loggerFactory :> obj else null }

        let flow : Flow<BaseRuntime, string, string> =
            flow {
                let! now = Clock.now<BaseRuntime, string>
                do! Log.info<BaseRuntime, string> "Hello"
                return now.ToString("HH:mm")
            }

        let result =
            flow.RunSynchronously(Hosting.createBaseRuntime sp)

        match result with
        | Exit.Success _ -> ()
        | _ -> failwithf "Expected success, got %A" result
        test <@ innerLogger.Entries |> List.exists (fun (l, m) -> l = Microsoft.Extensions.Logging.LogLevel.Information && m.Contains("Hello")) @>

    [<Fact>]
    let ``Startup: validateEnvironment detects missing variables`` () =
        let flow : Flow<BaseRuntime, EnvironmentVariableError, string> =
            EnvironmentVariable.get "AXIAL_HOSTING_MISSING"
        let result = Startup.validateEnvironment flow
        
        match result with
        | Error [ message ] -> test <@ message.Contains("AXIAL_HOSTING_MISSING") && message.Contains("Missing required environment variable") @>
        | _ -> failwithf "Expected missing variable error, got %A" result

type ExceptionRecordingLogger() =
    let entries = ResizeArray<Microsoft.Extensions.Logging.LogLevel * string * exn option>()
    member _.Entries = entries |> Seq.toList
    interface ILogger with
        member _.Log(level, _, state, error, _) =
            entries.Add(level, string state, Option.ofObj error)
        member _.IsEnabled(_) = true
        member _.BeginScope(_) = { new IDisposable with member _.Dispose() = () }

module FiberLoggingTests =
    /// Waits for a fiber to settle without consuming its outcome, so it stays unobserved.
    /// Deterministic replacement for fixed sleeps, which race the thread pool under load.
    let rec private waitForSettled (fiber: Fiber<'error, 'value>) : Flow<unit, 'testError, unit> =
        flow {
            if fiber.Metadata.Status = FiberStatus.Running then
                do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 5.0)
                return! waitForSettled fiber
        }

    [<Fact>]
    let ``FiberLogging.observe logs fiber defects and unobserved defects with their exceptions`` () =
        let logger = ExceptionRecordingLogger()

        let result =
            flow {
                let! fiber = Flow.fork (Flow.die (InvalidOperationException "silent crash") : Flow<unit, string, int>)
                do! waitForSettled fiber
                return "done"
            }
            |> FiberLogging.observe (logger :> ILogger)
            |> fun workflow -> workflow.RunSynchronously(())

        test <@ result = Exit.Success "done" @>

        let errors =
            logger.Entries
            |> List.filter (fun (level, _, _) -> level = Microsoft.Extensions.Logging.LogLevel.Error)

        let criticals =
            logger.Entries
            |> List.filter (fun (level, _, _) -> level = Microsoft.Extensions.Logging.LogLevel.Critical)

        test <@ List.length errors = 1 @>
        test <@ List.length criticals = 1 @>

        match criticals with
        | [ _, message, Some error ] ->
            test <@ message.Contains "Unobserved fiber defect" @>
            test <@ error.Message = "silent crash" @>
        | other -> failwithf "Expected one critical entry with an exception, got %A" other

    [<Fact>]
    let ``FiberObserver.compose runs both observers and guards each hook`` () =
        let logger = ExceptionRecordingLogger()
        let seen = ResizeArray<string>()

        let throwing =
            { FiberObserver.none with
                OnEnd = fun _ _ -> failwith "observer bug" }

        let recording =
            { FiberObserver.none with
                OnEnd = fun metadata _ -> lock seen (fun () -> seen.Add(string metadata.Id.Value)) }

        let composed = FiberObserver.compose throwing (FiberObserver.compose recording (FiberLogging.observer logger))

        let result =
            flow {
                let! fiber = Flow.fork (Flow.die (InvalidOperationException "boom") : Flow<unit, string, int>)
                do! waitForSettled fiber
                let! _exit = Flow.interrupt fiber
                return 1
            }
            |> Flow.withFiberObserver composed
            |> fun workflow -> workflow.RunSynchronously(())

        test <@ result = Exit.Success 1 @>
        test <@ seen.Count = 1 @>

        let errors =
            logger.Entries
            |> List.filter (fun (level, _, _) -> level = Microsoft.Extensions.Logging.LogLevel.Error)

        test <@ List.length errors = 1 @>
