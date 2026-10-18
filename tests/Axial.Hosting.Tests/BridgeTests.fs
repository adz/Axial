namespace Axial.Tests

open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open Axial
open Axial.Hosting
open Axial.HttpClient
open Axial.PlatformService
open Swensen.Unquote
open Xunit

module BridgeTests =
    [<Fact>]
    let ``Configuration environmentVariables reads configuration values`` () =
        let configuration =
            ConfigurationBuilder()
                .AddInMemoryCollection(dict [ "App:Name", "orders"; "App:Port", "8080" ])
                .Build()

        let environment = Configuration.environmentVariables configuration

        test <@ environment.TryGet "App:Name" = Some "orders" @>
        test <@ environment.TryGet "App:Port" = Some "8080" @>
        test <@ environment.TryGet "App:Missing" = None @>
        test <@ environment.Expand "%App:Port%" = "8080" @>
        test <@ environment.GetAll().Count = 2 @>

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
