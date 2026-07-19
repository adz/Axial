/// The GenHTTP twin of examples/Axial.Api: the same schema-driven boundary contract served by a
/// different host. One schema declaration drives request parsing, problem-details 400s, the
/// compiled response codec, and the published OpenAPI document — and none of that code changes
/// when the host is GenHTTP instead of ASP.NET Core.
///
/// Run it directly (`dotnet run --project examples/Axial.Api.GenHttp/Axial.Api.GenHttp.fsproj`)
/// or run the self-contained smoke pass with `AXIAL_EXAMPLE=smoke`.
module Axial.Api.GenHttp.Program

open System
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Text
open System.Threading.Tasks
open GenHTTP.Api.Protocol
open GenHTTP.Engine.Internal
open GenHTTP.Modules.Functional
open Axial.Codec
open Axial.Flow
open Axial.Schema
open Axial.Schema.Http
open Axial.Schema.Http.GenHttp

// ---------------------------------------------------------------------------
// The declaration: identical in spirit to examples/Axial.Api. Host-neutral.
// ---------------------------------------------------------------------------

type Signup = { Name: string; Email: string; Age: int }

module Signup =
    open Axial.Schema.DSL

    let schema =
        recordFor<Signup, _> (fun name email age -> { Name = name; Email = email; Age = age })
        |> field "name" _.Name (text |> constrainAll [ required; maxLength 80 ])
        |> field "email" _.Email (text |> constrainAll [ required; email ])
        |> field "age" _.Age (int |> constrain (between 13 120))
        |> build

module Boundary =
    let codec = Json.compile Signup.schema

    let openApiDocument =
        OpenApi.document
            (OpenApi.info "Axial signup sample (GenHTTP)" "1.0.0")
            [ Endpoint.post "/signups"
              |> Endpoint.summary "Create a signup"
              |> Endpoint.accepts Signup.schema
              |> Endpoint.returnsJson 201 "The trusted signup that was parsed." Signup.schema
              |> Endpoint.returnsProblemDetails ]

// ---------------------------------------------------------------------------
// Application service: an ordinary Flow, unaware of any HTTP host.
// ---------------------------------------------------------------------------

type AppEnv = { Greeting: string }

let createSignup (signup: Signup) : Flow<AppEnv, string, Signup> =
    flow {
        let! env = Flow.env
        return { signup with Name = $"{env.Greeting} {signup.Name}" }
    }

// ---------------------------------------------------------------------------
// GenHTTP wiring: request -> schema parse -> Flow -> codec response.
// ---------------------------------------------------------------------------

let buildHandler () =
    // GenHTTP's functional module requires delegates with a live target, so the wiring
    // lives in locals (the lambdas then compile to instance closures, as in a C# host).
    let codec = Boundary.codec
    let openApiDocument = Boundary.openApiDocument

    let signupEndpoint =
        flow {
            let! signup = Request.json Signup.schema
            let! created = EndpointFlow.run createSignup signup
            return Response.json ResponseStatus.Created codec created
        }

    let endpoint =
        flowEndpoint (fun _ -> { Greeting = "Welcome," }) (Response.text ResponseStatus.BadRequest)

    Inline
        .Create()
        .Post("/signups", endpoint signupEndpoint)
        .Get("/openapi.json", Func<IRequest, IResponse>(fun request -> SchemaResponse.openApi request openApiDocument))

let private freePort () =
    let listener = new TcpListener(IPAddress.Loopback, 0)
    listener.Start()
    let port = (listener.LocalEndpoint :?> IPEndPoint).Port
    listener.Stop()
    uint16 port

let private smoke () : Task =
    task {
        let port = freePort ()
        let host = Host.Create().Handler(buildHandler ()).Port(port)
        let! _ = host.StartAsync()

        try
            use client = new HttpClient(BaseAddress = Uri $"http://127.0.0.1:{int port}")

            use valid = new StringContent("""{"name":"Ada","email":"ada@example.org","age":36}""", Encoding.UTF8, "application/json")
            let! created = client.PostAsync("/signups", valid)
            let! createdBody = created.Content.ReadAsStringAsync()
            printfn "POST /signups (valid) -> %d %s" (int created.StatusCode) createdBody

            use invalid = new StringContent("""{"name":"","email":"nope","age":9}""", Encoding.UTF8, "application/json")
            let! rejected = client.PostAsync("/signups", invalid)
            let! rejectedBody = rejected.Content.ReadAsStringAsync()
            printfn "POST /signups (invalid) -> %d %s" (int rejected.StatusCode) rejectedBody

            let! openApi = client.GetStringAsync "/openapi.json"
            printfn "GET /openapi.json -> %d chars" openApi.Length
        finally
            host.StopAsync().GetAwaiter().GetResult() |> ignore
    }

[<EntryPoint>]
let main _ =
    if Environment.GetEnvironmentVariable "AXIAL_EXAMPLE" = "smoke" then
        smoke().GetAwaiter().GetResult()
        0
    else
        let host = Host.Create().Handler(buildHandler ()).Port(uint16 8080)
        printfn "Listening on http://localhost:8080 (POST /signups, GET /openapi.json). Ctrl+C to stop."
        host.RunAsync().GetAwaiter().GetResult()
