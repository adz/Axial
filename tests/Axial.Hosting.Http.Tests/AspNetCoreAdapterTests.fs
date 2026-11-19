module Reified.Schema.Http.Tests.AspNetCoreAdapterTests

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
open Reified
open Axial
open Reified.Schema.Http
open Axial.Hosting.AspNetCore
open Reified.Schema.Http.Tests.Fixtures

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

    let ageSchema = Schema.int

    let ageEndpoint : Flow<HttpEndpointEnv<string>, EndpointError<string>, IResult> =
        flow {
            let! age = Request.route "age" ageSchema
            return Response.text 200 (string age)
        }

    app.MapGet("/ages/{age}", endpoint ageEndpoint)
    |> ignore

    let signupPage : Flow<HttpEndpointEnv<string>, EndpointError<string>, IResult> =
        flow {
            match! Request.parseForm schema with
            | { Result = Ok signup } -> return Response.redirect 303 $"/signups/{signup.Age}"
            | invalid ->
                let street = Data.redisplayPath "address.street" invalid.Input
                let cityErrors = invalid.ErrorsFor "address.city" |> List.length

                return
                    Response.html 422 $"<p>{street}</p><p>{cityErrors}</p>"
                    |> Response.withHeader "HX-Trigger" "invalid"
        }

    let strictForm : Flow<HttpEndpointEnv<string>, EndpointError<string>, IResult> =
        flow {
            let! signup = Request.form schema
            return Response.text 200 signup.Address.City
        }

    let addressSearch : Flow<HttpEndpointEnv<string>, EndpointError<string>, IResult> =
        flow {
            match! Request.parseQuery (addressSchema ()) with
            | { Result = Ok address } -> return Response.text 200 address.City
            | invalid ->
                let street = Data.redisplayPath "street" invalid.Input
                let cityErrors = invalid.ErrorsFor "city" |> List.length
                return Response.html 422 $"<p>{street}</p><p>{cityErrors}</p>"
        }

    let moved : Flow<HttpEndpointEnv<string>, EndpointError<string>, IResult> =
        flow { return Response.redirect 302 "/old" |> Response.withHeader "Location" "/new" }

    let plain : Flow<HttpEndpointEnv<string>, EndpointError<string>, IResult> =
        flow {
            return
                Response.html 200 "<p>plain</p>"
                |> Response.withHeader "Content-Type" "text/plain; charset=utf-8"
        }

    let retriggered : Flow<HttpEndpointEnv<string>, EndpointError<string>, IResult> =
        flow {
            return
                Response.text 200 "saved"
                |> Response.withHeader "HX-Trigger" "first"
                |> Response.withHeader "HX-Trigger" "second"
        }

    app.MapPost("/signup-page", endpoint signupPage) |> ignore
    app.MapPost("/strict-form", endpoint strictForm) |> ignore
    let smallBody =
        flowEndpointWithOptions
            { EndpointOptions.Default with MaxRequestBodySize = Some 64L }
            (fun context -> context.RequestServices.GetRequiredService<string>())
            (fun error -> Results.BadRequest error)

    app.MapPost("/small-signups", smallBody signupEndpoint) |> ignore
    app.MapPost("/small-form", smallBody strictForm) |> ignore
    app.MapGet("/address", endpoint addressSearch) |> ignore
    app.MapGet("/moved", endpoint moved) |> ignore
    app.MapGet("/plain", endpoint plain) |> ignore
    app.MapGet("/retriggered", endpoint retriggered) |> ignore

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

let private withServer (run: HttpClient -> Task) =
    task {
        let app = buildApp ()
        do! app.StartAsync()

        try
            use handler = new HttpClientHandler(AllowAutoRedirect = false)
            use client = new HttpClient(handler, BaseAddress = Uri(Seq.head app.Urls))
            do! run client
        finally
            app.StopAsync().GetAwaiter().GetResult()
    }

let private validForm () =
    new FormUrlEncodedContent(
        [ Collections.Generic.KeyValuePair("name", "Ada Lovelace")
          Collections.Generic.KeyValuePair("age", "36")
          Collections.Generic.KeyValuePair("address.street", "12 Analytical Way")
          Collections.Generic.KeyValuePair("address.city", "London")
          Collections.Generic.KeyValuePair("tags.0", "vip") ]
    )

let private formMissingCity () =
    new FormUrlEncodedContent(
        [ Collections.Generic.KeyValuePair("name", "Ada Lovelace")
          Collections.Generic.KeyValuePair("age", "36")
          Collections.Generic.KeyValuePair("address.street", "12 Analytical Way")
          Collections.Generic.KeyValuePair("address.city", "") ]
    )

[<Fact>]
let ``a valid page form redirects after the post`` () =
    withServer (fun client ->
        task {
            use content = validForm ()
            let! response = client.PostAsync("/signup-page", content)

            Assert.Equal(303, int response.StatusCode)
            Assert.Equal("/signups/36", string response.Headers.Location)
        })

[<Fact>]
let ``an invalid page form shows again with what was typed and its errors`` () =
    withServer (fun client ->
        task {
            use content = formMissingCity ()
            let! response = client.PostAsync("/signup-page", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(422, int response.StatusCode)
            Assert.Equal("text/html", response.Content.Headers.ContentType.MediaType)
            Assert.Equal("<p>12 Analytical Way</p><p>1</p>", body)
            Assert.Equal<string seq>([ "invalid" ], response.Headers.GetValues "HX-Trigger")
        })

[<Fact>]
let ``a form endpoint parses the posted form`` () =
    withServer (fun client ->
        task {
            use content = validForm ()
            let! response = client.PostAsync("/strict-form", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(200, int response.StatusCode)
            Assert.Equal("London", body)
        })

[<Fact>]
let ``a form endpoint refuses a body that is not a form`` () =
    withServer (fun client ->
        task {
            use content = new StringContent(validJson, Encoding.UTF8, "application/json")
            let! response = client.PostAsync("/strict-form", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(400, int response.StatusCode)
            Assert.Contains("not a form", body)
        })

[<Fact>]
let ``a valid query parses to the trusted model`` () =
    withServer (fun client ->
        task {
            let! response = client.GetAsync("/address?street=12%20Analytical%20Way&city=London")
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(200, int response.StatusCode)
            Assert.Equal("London", body)
        })

[<Fact>]
let ``an invalid query shows again with what was typed and its errors`` () =
    withServer (fun client ->
        task {
            let! response = client.GetAsync("/address?street=12%20Analytical%20Way&city=")
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(422, int response.StatusCode)
            Assert.Equal("<p>12 Analytical Way</p><p>1</p>", body)
        })

[<Fact>]
let ``withHeader replaces the location a redirect set`` () =
    withServer (fun client ->
        task {
            let! response = client.GetAsync("/moved")

            Assert.Equal(302, int response.StatusCode)
            Assert.Equal("/new", string response.Headers.Location)
        })

[<Fact>]
let ``withHeader replaces the content type a response set`` () =
    withServer (fun client ->
        task {
            let! response = client.GetAsync("/plain")

            Assert.Equal("text/plain", response.Content.Headers.ContentType.MediaType)
        })

[<Fact>]
let ``the last withHeader for a name wins`` () =
    withServer (fun client ->
        task {
            let! response = client.GetAsync("/retriggered")

            Assert.Equal<string seq>([ "second" ], response.Headers.GetValues "HX-Trigger")
        })

[<Fact>]
let ``a form value past the form reader's limit is refused as too large`` () =
    withServer (fun client ->
        task {
            use content =
                new FormUrlEncodedContent(
                    [ Collections.Generic.KeyValuePair("name", String('a', Microsoft.AspNetCore.Http.Features.FormOptions().ValueLengthLimit + 1)) ]
                )

            let! response = client.PostAsync("/strict-form", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(413, int response.StatusCode)
            Assert.Contains("too large", body)
        })

[<Fact>]
let ``a json body past the endpoint's limit is refused as too large`` () =
    withServer (fun client ->
        task {
            use content = new StringContent(validJson, Encoding.UTF8, "application/json")
            let! response = client.PostAsync("/small-signups", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(413, int response.StatusCode)
            Assert.Contains("too large", body)
        })

[<Fact>]
let ``a form body past the endpoint's limit is refused as too large`` () =
    withServer (fun client ->
        task {
            use content = validForm ()
            let! response = client.PostAsync("/small-form", content)
            let! body = response.Content.ReadAsStringAsync()

            Assert.Equal(413, int response.StatusCode)
            Assert.Contains("too large", body)
        })
