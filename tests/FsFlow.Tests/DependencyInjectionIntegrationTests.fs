namespace FsFlow.Tests

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Services.Console
open FsFlow.Services.Core
open FsFlow.Services.FileSystem
open FsFlow.Services.Http
open FsFlow.Services.Process
open Microsoft.Extensions.DependencyInjection
open Swensen.Unquote
open Xunit

module CoreClock = FsFlow.Services.Core.Clock
module CoreEnvironmentVariables = FsFlow.Services.Core.EnvironmentVariables
module CoreGuid = FsFlow.Services.Core.Guid
module CoreLog = FsFlow.Services.Core.Log
module CoreRandom = FsFlow.Services.Core.Random
module ConsoleService = FsFlow.Services.Console.Console
module FileSystemService = FsFlow.Services.FileSystem.FileSystem
module HttpService = FsFlow.Services.Http.Http
module ProcessService = FsFlow.Services.Process.Process

type ServicePackageLayerServices =
    {
        Console: IConsole
        FileSystem: IFileSystem
        Http: IHttp
        Process: IProcess
    }

    interface IHas<IConsole> with
        member this.Service = this.Console

    interface IHas<IFileSystem> with
        member this.Service = this.FileSystem

    interface IHas<IHttp> with
        member this.Service = this.Http

    interface IHas<IProcess> with
        member this.Service = this.Process

module DependencyInjectionIntegrationTests =
    type StaticHttpHandler(responseText: string) =
        inherit HttpMessageHandler()

        override _.SendAsync(_: HttpRequestMessage, _: CancellationToken) =
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(responseText)))

    let private serviceProviderWithBaseRuntimeServices () =
        let clock = CoreClock.fromValue (DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero))
        let logMessages = ResizeArray<LogLevel * string>()
        let logger = CoreLog.fromSink (fun level message -> logMessages.Add(level, message))
        let random = CoreRandom.fromValue 17
        let guid = CoreGuid.fromValue (System.Guid.Parse "cccccccc-cccc-cccc-cccc-cccccccccccc")
        let envVars = CoreEnvironmentVariables.fromPairs [ "FSFLOW_DI", "registered" ]

        let provider =
            ServiceCollection()
                .AddSingleton<IClock>(clock)
                .AddSingleton<ILog>(logger)
                .AddSingleton<IRandom>(random)
                .AddSingleton<IGuid>(guid)
                .AddSingleton<IEnvironmentVariables>(envVars)
                .BuildServiceProvider()

        provider, logMessages

    [<Fact>]
    let ``base runtime layer provisions services from Microsoft DI`` () =
        let provider, logMessages = serviceProviderWithBaseRuntimeServices ()
        use provider = provider

        let workflow : Flow<BaseRuntime, BaseRuntimeError, string> =
            flow {
                let! now = CoreClock.now<BaseRuntime, BaseRuntimeError>
                let formattedNow = now.ToString("HH:mm")
                do! CoreLog.info<BaseRuntime, BaseRuntimeError> $"di-now={formattedNow}"
                let! next = CoreRandom.nextInt<BaseRuntime, BaseRuntimeError> 1 100
                let! id = CoreGuid.newGuid<BaseRuntime, BaseRuntimeError>
                let! value = CoreEnvironmentVariables.tryGet<BaseRuntime, BaseRuntimeError> "FSFLOW_DI"
                let environmentValue = defaultArg value "<missing>"
                return $"{next}:{id}:{environmentValue}"
            }

        let result =
            workflow
            |> Flow.provide BaseRuntime.fromServiceProvider
            |> Flow.runSync (provider :> IServiceProvider)

        test <@ result = Exit.Success "17:cccccccc-cccc-cccc-cccc-cccccccccccc:registered" @>
        test <@ List.ofSeq logMessages = [ LogLevel.Information, "di-now=09:30" ] @>

    [<Fact>]
    let ``base runtime layer reports missing Microsoft DI registrations as typed failures`` () =
        use provider =
            ServiceCollection()
                .AddSingleton<IClock>(CoreClock.live)
                .BuildServiceProvider()

        let result =
            Flow.env<BaseRuntime, BaseRuntimeError>
            |> Flow.provide BaseRuntime.fromServiceProvider
            |> Flow.runSync (provider :> IServiceProvider)

        test <@ result = Exit.Failure(Cause.Fail(BaseRuntimeError.MissingService "ILog")) @>

    [<Fact>]
    let ``service resolve treats missing provider registrations as defects`` () =
        use provider = ServiceCollection().BuildServiceProvider()

        let result =
            Service<IClock>.resolve<IServiceProvider, unit>()
            |> Flow.runSync (provider :> IServiceProvider)

        match result with
        | Exit.Failure(Cause.Die error) ->
            test <@ error.Message = "Service IClock was not registered in the IServiceProvider." @>
        | other -> failwith $"expected missing service defect, got {other}"

    [<Fact>]
    let ``service package layers compose into one explicit service environment`` () =
        use httpClient = new HttpClient(new StaticHttpHandler("http-body"))
        let tempFile = Path.Combine(Path.GetTempPath(), $"fsflow-service-layer-{System.Guid.NewGuid():N}.txt")

        let serviceLayer : Layer<unit, FileSystemError, ServicePackageLayerServices> =
            layer {
                let! console = ConsoleService.layer
                and! fileSystem = FileSystemService.layer
                and! http = HttpService.layer httpClient
                and! processService = ProcessService.layer

                return
                    {
                        Console = console
                        FileSystem = fileSystem
                        Http = http
                        Process = processService
                    }
            }
            |> Layer.mapError (fun _ -> FileSystemError.Unexpected(None, "service layer failed unexpectedly"))

        let workflow : Flow<ServicePackageLayerServices, FileSystemError, string * string * int> =
            flow {
                do! ConsoleService.writeLine<ServicePackageLayerServices, FileSystemError> "service-package-layer"
                do! FileSystemService.writeAllText tempFile "file-body"
                let! fileBody = FileSystemService.readAllText tempFile
                let! httpBody = HttpService.getString<ServicePackageLayerServices, FileSystemError> "https://example.test/"
                let! processResult = ProcessService.execute<ServicePackageLayerServices, FileSystemError> "dotnet" "--version"
                return fileBody, httpBody, processResult.ExitCode
            }

        try
            let result =
                workflow
                |> Flow.provide serviceLayer
                |> Flow.runSync ()

            test <@ result = Exit.Success("file-body", "http-body", 0) @>
        finally
            if File.Exists tempFile then
                File.Delete tempFile
