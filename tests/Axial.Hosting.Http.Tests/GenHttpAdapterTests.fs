module Reified.Schema.Http.Tests.GenHttpAdapterTests

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
open Reified
open Axial
open Reified.Schema.Http
open Axial.Hosting.GenHttp
open Reified.Schema.Http.Tests.Fixtures

let private freePort () =
    let listener = new TcpListener(IPAddress.Loopback, 0)
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

    let signupPage : Flow<HttpEndpointEnv<string>, EndpointError<string>, HttpResponse> =
        flow {
            match! Request.parseForm schema with
            | { Result = Ok signup } -> return Response.redirect ResponseStatus.SeeOther $"/signups/{signup.Age}"
            | invalid ->
                let street = Data.redisplayPath "address.street" invalid.Input
                let cityErrors = invalid.ErrorsFor "address.city" |> List.length

                return
                    Response.html ResponseStatus.UnprocessableEntity $"<p>{street}</p><p>{cityErrors}</p>"
                    |> Response.withHeader "HX-Trigger" "invalid"
        }

    let strictForm : Flow<HttpEndpointEnv<string>, EndpointError<string>, HttpResponse> =
        flow {
            let! signup = Request.form schema
            return Response.text ResponseStatus.Ok signup.Address.City
        }

    let addressSearch : Flow<HttpEndpointEnv<string>, EndpointError<string>, HttpResponse> =
        flow {
            match! Request.parseQuery (addressSchema ()) with
            | { Result = Ok address } -> return Response.text ResponseStatus.Ok address.City
            | invalid ->
                let street = Data.redisplayPath "street" invalid.Input
                let cityErrors = invalid.ErrorsFor "city" |> List.length
                return Response.html ResponseStatus.UnprocessableEntity $"<p>{street}</p><p>{cityErrors}</p>"
        }

    let moved : Flow<HttpEndpointEnv<string>, EndpointError<string>, HttpResponse> =
        flow { return Response.redirect ResponseStatus.Found "/old" |> Response.withHeader "Location" "/new" }

    let plain : Flow<HttpEndpointEnv<string>, EndpointError<string>, HttpResponse> =
        flow {
            return
                Response.html ResponseStatus.Ok "<p>plain</p>"
                |> Response.withHeader "Content-Type" "text/plain; charset=utf-8"
        }

    let retriggered : Flow<HttpEndpointEnv<string>, EndpointError<string>, HttpResponse> =
        flow {
            return
                Response.text ResponseStatus.Ok "saved"
                |> Response.withHeader "HX-Trigger" "first"
                |> Response.withHeader "HX-Trigger" "second"
        }

    Inline
        .Create()
        .Get("/address", endpoint addressSearch)
        .Get("/moved", endpoint moved)
        .Get("/plain", endpoint plain)
        .Get("/retriggered", endpoint retriggered)
        .Post("/signups", endpoint signupEndpoint)
        .Post("/signup-page", endpoint signupPage)
        .Post("/strict-form", endpoint strictForm)
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
            use handler = new HttpClientHandler(AllowAutoRedirect = false)
            use client = new HttpClient(handler, BaseAddress = Uri $"http://127.0.0.1:{int port}")
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
