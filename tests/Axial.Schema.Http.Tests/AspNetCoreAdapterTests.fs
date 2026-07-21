module Axial.Schema.Http.Tests.AspNetCoreAdapterTests

open System
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Xunit
open Swensen.Unquote
open Axial.Schema.Json
open Axial.Flow
open Axial.Schema.Http
open Axial.Schema.Http.AspNetCore
open Axial.Schema.Http.Tests.Fixtures

let private buildApp () =
    let builder = WebApplication.CreateBuilder [| "--urls"; "http://127.0.0.1:0" |]
    builder.Logging.ClearProviders() |> ignore
    builder.Services.AddSingleton<string>("!") |> ignore
    let app = builder.Build()

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
            return Response.json 201 codec created
        }

    let endpoint =
        flowEndpoint
            (fun context -> context.RequestServices.GetRequiredService<string>())
            (fun error -> Results.BadRequest error)

    app.MapPost("/signups", endpoint signupEndpoint)
    |> ignore

    let rejectSignup (_: Signup) : Flow<string, string, Signup> =
        Flow.fail "signups are closed"

    let rejectedEndpoint =
        flow {
            let! signup = Request.json schema
            let! rejected = EndpointFlow.run rejectSignup signup
            return Response.json 201 codec rejected
        }

    app.MapPost("/rejected-signups", endpoint rejectedEndpoint)
    |> ignore

    let ageSchema = Axial.Schema.Schema.int

    let ageEndpoint : Flow<HttpEndpointEnv<string>, EndpointError<string>, IResult> =
        flow {
            let! age = Request.route "age" ageSchema
            return Response.text 200 (string age)
        }

    app.MapGet("/ages/{age}", endpoint ageEndpoint)
    |> ignore

    app.MapGet("/openapi.json", Func<IResult>(fun () -> SchemaResult.openApi openApiDocument))
    |> ignore

    app

[<Fact>]
let ``valid json parses and round-trips through the codec`` () =
    task {
        let app = buildApp ()
        do! app.StartAsync()
        use client = new HttpClient(BaseAddress = Uri(Seq.head app.Urls))

        use content = new StringContent(validJson, Encoding.UTF8, "application/json")
        let! response = client.PostAsync("/signups", content)
        let! body = response.Content.ReadAsStringAsync()

        Assert.Equal(201, int response.StatusCode)
        use document = JsonDocument.Parse body
        Assert.Equal("Ada Lovelace!", document.RootElement.GetProperty("name").GetString())

        do! app.StopAsync()
    }

[<Fact>]
let ``invalid json gets a problem details response with pointers`` () =
    task {
        let app = buildApp ()
        do! app.StartAsync()
        use client = new HttpClient(BaseAddress = Uri(Seq.head app.Urls))

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

        do! app.StopAsync()
    }

[<Fact>]
let ``malformed json gets a request problem instead of becoming a defect`` () =
    task {
        let app = buildApp ()
        do! app.StartAsync()
        use client = new HttpClient(BaseAddress = Uri(Seq.head app.Urls))

        use content = new StringContent("{", Encoding.UTF8, "application/json")
        let! response = client.PostAsync("/signups", content)
        let! body = response.Content.ReadAsStringAsync()

        Assert.Equal(400, int response.StatusCode)
        Assert.Contains("not valid JSON", body)

        do! app.StopAsync()
    }

[<Fact>]
let ``application failures use the configured HTTP error mapping`` () =
    task {
        let app = buildApp ()
        do! app.StartAsync()
        use client = new HttpClient(BaseAddress = Uri(Seq.head app.Urls))

        use content = new StringContent(validJson, Encoding.UTF8, "application/json")
        let! response = client.PostAsync("/rejected-signups", content)
        let! body = response.Content.ReadAsStringAsync()

        Assert.Equal(400, int response.StatusCode)
        Assert.Contains("signups are closed", body)

        do! app.StopAsync()
    }

[<Fact>]
let ``route values parse through schemas inside the endpoint Flow`` () =
    task {
        let app = buildApp ()
        do! app.StartAsync()
        use client = new HttpClient(BaseAddress = Uri(Seq.head app.Urls))

        let! response = client.GetAsync "/ages/42"
        let! body = response.Content.ReadAsStringAsync()

        Assert.Equal(200, int response.StatusCode)
        Assert.Equal("42", body)

        do! app.StopAsync()
    }

[<Fact>]
let ``the openapi document is served from the assembled specs`` () =
    task {
        let app = buildApp ()
        do! app.StartAsync()
        use client = new HttpClient(BaseAddress = Uri(Seq.head app.Urls))

        let! body = client.GetStringAsync "/openapi.json"
        use document = JsonDocument.Parse body

        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/signups") |> fst)

        do! app.StopAsync()
    }
