namespace Axial.Tests

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Configuration
open Axial.Console
open Axial.PlatformService
open Axial.FileSystem
open Axial.HttpClient
open Axial.Process
open Microsoft.Extensions.DependencyInjection
open Reified
open Swensen.Unquote
open Xunit

module CoreClock = Axial.PlatformService.Clock
module CoreGuid = Axial.PlatformService.Guid
module CoreLog = Axial.PlatformService.Log
module CoreRandom = Axial.PlatformService.Random
module ConsoleService = Axial.Console.Console
module FileSystemService = Axial.FileSystem.FileSystem
module HttpService = Axial.HttpClient.Http
module ProcessService = Axial.Process.Process

type ServicePackageLayerServices =
    {
        Console: IConsole
        FileSystem: IFileSystem
        Http: IHttp
        Process: IProcess
    }

    interface IHasConsole with
        member this.Console = this.Console

    interface IHasFileSystem with
        member this.FileSystem = this.FileSystem

    interface IHasHttp with
        member this.Http = this.Http

    interface IHasProcess with
        member this.Process = this.Process

module DependencyInjectionIntegrationTests =
    type StaticHttpHandler(responseText: string) =
        inherit HttpMessageHandler()

        override _.SendAsync(_: HttpRequestMessage, _: CancellationToken) =
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(responseText)))

    type ConfigEnvironment = { Config: IConfig } with
        interface IHasConfig with member this.Config = this.Config

    [<Fact>]
    let ``DI supplies authority while platform overrides are installed at the edge`` () =
        let clock = CoreClock.fromValue (DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero))
        let log = CoreLog.memory ()
        use provider =
            ServiceCollection()
                .AddSingleton<IConfig>(Config.ofData (Data.Object [ "Axial", Data.Object [ "Di", Data.Text "registered" ] ]))
                .BuildServiceProvider()
        let workflow : Flow<ConfigEnvironment, ConfigError, string> = flow {
            let! now = CoreClock.now
            let formattedNow = now.ToString("HH:mm")
            do! CoreLog.info $"di-now={formattedNow}"
            let! next = CoreRandom.nextInt 1 100
            let! id = CoreGuid.newGuid
            let! value = Config.required "Axial.Di"
            return $"{next}:{id}:{value}"
        }
        let result =
            workflow
            |> Flow.withClock clock
            |> Flow.withLogSink log
            |> Flow.withRandom (CoreRandom.fromValue 17)
            |> Flow.withGuid (CoreGuid.fromValue (System.Guid.Parse "cccccccc-cccc-cccc-cccc-cccccccccccc"))
            |> Flow.runSync { Config = provider.GetRequiredService<IConfig>() }
        test <@ result = Exit.Success "17:cccccccc-cccc-cccc-cccc-cccccccccccc:registered" @>
        test <@ log.Entries |> Seq.map (fun entry -> entry.Level, entry.Message) |> Seq.toList = [ LogLevel.Information, "di-now=09:30" ] @>

    [<Fact>]
    let ``service resolve treats missing provider registrations as defects`` () =
        use provider = ServiceCollection().BuildServiceProvider()

        let result =
            ServiceProvider.get<IClock, IServiceProvider, unit>()
            |> Flow.runSync (provider :> IServiceProvider)

        match result with
        | Exit.Failure(Cause.Die error) ->
            test <@ error.Message = "Service IClock was not registered in the IServiceProvider." @>
        | other -> failwith $"expected missing service defect, got {other}"

    [<Fact>]
    let ``service package layers compose into one explicit service environment`` () =
        use httpClient = new HttpClient(new StaticHttpHandler("http-body"))
        let tempFile = Path.Combine(Path.GetTempPath(), $"axial-service-layer-{System.Guid.NewGuid():N}.txt")

        let serviceLayer : Layer<unit, FileSystemError, ServicePackageLayerServices> =
            layer {
                let! console = Layer.succeed ConsoleService.live
                and! fileSystem = Layer.succeed FileSystemService.live
                and! http = Layer.succeed (HttpService.live CoreClock.live httpClient)

                let! processService = Layer.succeed (ProcessService.live CoreClock.live fileSystem console)

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
                let! httpBody =
                    HttpService.getString<ServicePackageLayerServices> "https://example.test/"
                    |> Flow.mapError (fun error -> FileSystemError.Unexpected(None, $"http failed: {error}"))
                let! processResult =
                    ProcessService.commandArgs "dotnet" [ "--version" ]
                    |> ProcessService.toFlow
                    |> Flow.mapError (fun error -> FileSystemError.Unexpected(None, $"process failed: {error}"))
                return fileBody, httpBody, processResult.ExitCode
            }

        try
            let result =
                workflow
                |> Layer.provide serviceLayer
                |> Flow.runSync ()

            test <@ result = Exit.Success("file-body", "http-body", 0) @>
        finally
            if File.Exists tempFile then
                File.Delete tempFile
