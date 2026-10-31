namespace Axial.Tests

open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open Axial
open Axial.Hosting
open Axial.Configuration
open Axial.HttpClient
open Axial.PlatformService
open Swensen.Unquote
open Xunit

module BridgeTests =
    [<Fact>]
    let ``HostConfiguration source reads configuration values as data`` () =
        let configuration =
            ConfigurationBuilder()
                .AddInMemoryCollection(dict [ "App:Name", "orders"; "App:Port", "8080" ])
                .Build()

        let config =
            match Flow.run () (Config.loadData [ HostConfiguration.source "host" configuration ]) with
            | Exit.Success config -> config
            | Exit.Failure cause -> failwithf "Expected configuration, got %A" cause

        test <@ Config.tryFind "app.name" config = Ok(Some "orders") @>
        test <@ Config.tryFind "App.Port" config = Ok(Some "8080") @>
        test <@ Config.tryFind "App.Missing" config = Ok None @>
        test <@ ConfigError.describe config (ConfigError.Missing "App.Host") = "App.Host was omitted (set one of: host App.Host)" @>

    [<Fact>]
    let ``HttpFactory live sends through a factory-created client`` () =
        let handler =
            { new HttpMessageHandler() with
                member _.SendAsync(request, cancellationToken) =
                    Task.FromResult(
                        new HttpResponseMessage(
                            HttpStatusCode.Created,
                            Content = new StringContent("created"))) }

        let client = new HttpClient(handler)

        let factory =
            { new System.Net.Http.IHttpClientFactory with
                member _.CreateClient(name) = client }

        let http = HttpFactory.live Clock.live factory

        let response =
            http.Send(Http.get "https://example.test/items").RunSynchronously(())

        test <@ match response with
                | Exit.Success response -> response.StatusCode = 201 && response.Text = "created"
                | Exit.Failure error -> failwithf "Expected Ok, got %A" error @>
