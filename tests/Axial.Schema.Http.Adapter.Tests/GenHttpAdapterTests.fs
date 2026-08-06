module Axial.Schema.Http.Tests.GenHttpAdapterTests

open System
open System.Net.Http
open System.Net.Sockets
open System.Net
open System.Text
open System.Text.Json
open System.Threading.Tasks
open GenHTTP.Api.Protocol
open GenHTTP.Engine.Internal
open GenHTTP.Modules.Functional
open Xunit
open Swensen.Unquote
open Axial.Schema.Json
open Axial.Flow
open Axial.Schema.Http
open Axial.Schema.Http.GenHttp
open Axial.Schema.Http.Tests.Fixtures

let private freePort () =
    let listener = TcpListener(IPAddress.Loopback, 0)
    listener.Start()
    let port = (listener.LocalEndpoint :?> IPEndPoint).Port
    listener.Stop()
    uint16 port

let private buildHandler () =
    let schema = signupSchema ()
    let codec = Json.compile schema

    let openApiDocument =
        OpenApi.document
            (OpenApi.info "Signup API" "1.0.0")
            [ Endpoint.post "/signups"
              |> Endpoint.accepts schema
              |> Endpoint.returnsJson 201 "The trusted signup." schema
              |> Endpoint.returnsProblemDetails ]

    let createSignup (signup: Signup) : Flow<string, string, Signup> =
        flow {
            let! suffix = Flow.env
            return { signup with Name = signup.Name + suffix }
        }

    let signupEndpoint =
        flow {
            let! signup = Request.json schema
            let! created = EndpointFlow.run createSignup signup
            return Response.json ResponseStatus.Created codec created
        }

    let endpoint =
        flowEndpoint
            (fun _ -> "!")
            (Response.text ResponseStatus.BadRequest)

    Inline
        .Create()
        .Post("/signups", endpoint signupEndpoint)
        .Get(
            "/openapi.json",
            Func<IRequest, IResponse>(fun request -> SchemaResponse.openApi request openApiDocument)
        )

let private withServer (run: HttpClient -> Task) =
    task {
        let port = freePort ()
        let host = Host.Create().Handler(buildHandler ()).Port(port)
        let! _ = host.StartAsync()

        try
            use client = new HttpClient(BaseAddress = Uri $"http://127.0.0.1:{int port}")
            do! run client
        finally
            host.StopAsync().GetAwaiter().GetResult() |> ignore
    }

[<Fact>]
let ``valid json parses and round-trips through the codec`` () =
    withServer (fun client ->
        task {
            use content = new StringContent(validJson, Encoding.UTF8, "application/json")
            let! response = client.PostAsync("/signups", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(201, int response.StatusCode)
            use document = JsonDocument.Parse body
            Assert.Equal("Ada Lovelace!", document.RootElement.GetProperty("name").GetString())
        })

[<Fact>]
let ``invalid json gets a problem details response with pointers`` () =
    withServer (fun client ->
        task {
            use content = new StringContent(invalidJson, Encoding.UTF8, "application/json")
            let! response = client.PostAsync("/signups", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(400, int response.StatusCode)
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType.MediaType)
            use document = JsonDocument.Parse body

            let pointers =
                document.RootElement.GetProperty("errors").EnumerateArray()
                |> Seq.map (fun error -> error.GetProperty("pointer").GetString())
                |> List.ofSeq

            Assert.Contains("/address/city", pointers)
        })

[<Fact>]
let ``malformed json gets a request problem instead of becoming a defect`` () =
    withServer (fun client ->
        task {
            use content = new StringContent("{", Encoding.UTF8, "application/json")
            let! response = client.PostAsync("/signups", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(400, int response.StatusCode)
            Assert.Contains("not valid JSON", body)
        })

[<Fact>]
let ``the openapi document is served from the assembled specs`` () =
    withServer (fun client ->
        task {
            let! body = client.GetStringAsync "/openapi.json"
            use document = JsonDocument.Parse body

            Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/signups") |> fst)
        })
