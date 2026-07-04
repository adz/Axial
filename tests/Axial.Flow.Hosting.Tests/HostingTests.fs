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
