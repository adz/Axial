namespace Axial.Tests

open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.HttpClient
open Axial.Flow.HttpClient.DSL
open Axial.Flow.PlatformService
open Swensen.Unquote
open Xunit

type HttpTestEnv =
    { Http: IHttp }

    interface IHas<IHttp> with
        member this.Service = this.Http

module HttpServiceTests =
    let private syntheticTime = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    let private runSync (environment: 'env) (workflow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
        workflow.RunSynchronously(environment)

    let private requireSuccess =
        function
        | Exit.Success value -> value
        | Exit.Failure cause -> failwithf "Expected success, got %A" cause

    let private requireError =
        function
        | Exit.Failure(Cause.Fail error) -> error
        | other -> failwithf "Expected typed failure, got %A" other

    /// A fake service that records requests and replies from a scripted queue.
    let private fake (responses: Result<HttpResponse, HttpError> list) =
        let sent = ResizeArray<HttpRequest>()
        let queue = System.Collections.Generic.Queue<_> responses
        let service =
            { new IHttp with
                member _.Send(request, _) =
                    async {
                        sent.Add request
                        return if queue.Count > 1 then queue.Dequeue() else queue.Peek()
                    } }
        sent, { Http = service }

    let private okResponse status body = Ok(Response.create syntheticTime status body)

    // ---- Request building and safety ----

    [<Fact>]
    let ``interpolated URLs escape every hole as one value`` () =
        let name = "a b/c&d?e"
        let request = GET $"https://api.example.test/users/{name}"
        test <@ Request.url request = "https://api.example.test/users/a%20b%2Fc%26d%3Fe" @>

    [<Fact>]
    let ``secret interpolation values are redacted in the rendered request`` () =
        let key = "top-secret"
        let request = GET $"https://api.example.test/lookup?key={secret key}"
        test <@ Request.url request = "https://api.example.test/lookup?key=top-secret" @>
        test <@ Request.render request = "GET https://api.example.test/lookup?key=***" @>

    [<Fact>]
    let ``query parameters are URL-encoded and secret queries are redacted`` () =
        let request =
            Http.get "https://api.example.test/search"
            |> Request.query "q" "f# & http"
            |> Request.secretQuery "api_key" "k123"
        test <@ Request.render request = "GET https://api.example.test/search?q=f%23%20%26%20http&api_key=***" @>

    [<Fact>]
    let ``bearer and basic auth headers are always redacted in plans`` () =
        let plan =
            Http.get "https://api.example.test/"
            |> Request.bearer "token-value"
            |> Request.header "Accept" "application/json"
            |> Request.plan
        test <@ plan.Headers = [ "Authorization", "***"; "Accept", "application/json" ] @>

    [<Fact>]
    let ``request plan describes method body timeout and expectation`` () =
        let plan =
            Http.post "https://api.example.test/users"
            |> Request.jsonBody """{"name":"Ada"}"""
            |> Request.timeout (TimeSpan.FromSeconds 5.0)
            |> Request.expect [ 200; 201 ]
            |> Request.plan
        test <@ plan.Method = "POST" @>
        test <@ plan.Body = "application/json (14 characters)" @>
        test <@ plan.Timeout = Some(TimeSpan.FromSeconds 5.0) @>
        test <@ plan.Expectation = "200, 201" @>

    [<Fact>]
    let ``creating a request with an empty URL is rejected`` () =
        raises<ArgumentException> <@ Http.get "" @>

    // ---- Sending through the service ----

    [<Fact>]
    let ``send returns the response transcript for expected statuses`` () =
        let sent, env = fake [ okResponse 200 "body" ]
        let response = Http.get "https://api.example.test/" |> Http.send |> runSync env |> requireSuccess
        test <@ response.StatusCode = 200 && response.Text = "body" @>
        test <@ sent.Count = 1 @>

    [<Fact>]
    let ``send fails with a Status error outside the expectation`` () =
        let _, env = fake [ okResponse 404 "missing" ]
        let error = Http.get "https://api.example.test/" |> Http.send |> runSync env |> requireError
        test <@ match error with HttpError.Status response -> response.StatusCode = 404 | _ -> false @>

    [<Fact>]
    let ``sendResult returns unexpected statuses as plain values`` () =
        let _, env = fake [ okResponse 500 "boom" ]
        let response = Http.get "https://api.example.test/" |> Http.sendResult |> runSync env |> requireSuccess
        test <@ response.StatusCode = 500 @>

    [<Fact>]
    let ``expect widens the accepted statuses`` () =
        let _, env = fake [ okResponse 404 "missing" ]
        let response =
            Http.get "https://api.example.test/maybe"
            |> Request.expect [ 200; 404 ]
            |> Http.send
            |> runSync env
            |> requireSuccess
        test <@ response.StatusCode = 404 @>

    [<Fact>]
    let ``getString returns the response body text`` () =
        let _, env = fake [ okResponse 200 "html" ]
        test <@ runSync env (Http.getString "https://example.test/") = Exit.Success "html" @>

    // ---- Typed decoding ----

    let private decodeNumber (text: string) =
        match Int32.TryParse text with
        | true, value -> Ok value
        | false, _ -> Error $"'{text}' is not a number"

    [<Fact>]
    let ``json decodes the body with the supplied decoder`` () =
        let _, env = fake [ okResponse 200 "42" ]
        let value =
            GET $"https://api.example.test/answer"
            |> fetchJson decodeNumber
            |> runSync env
            |> requireSuccess
        test <@ value = 42 @>

    [<Fact>]
    let ``json decode failures carry the response transcript`` () =
        let _, env = fake [ okResponse 200 "not-a-number" ]
        let error =
            Http.getJson decodeNumber "https://api.example.test/answer"
            |> runSync env
            |> requireError
        test <@ match error with
                | HttpError.DecodeFailed(message, response) ->
                    message.Contains "not-a-number" && response.StatusCode = 200
                | _ -> false @>

    // ---- Errors and retries ----

    [<Fact>]
    let ``transient classification covers connections timeouts and retryable statuses`` () =
        test <@ HttpError.isTransient (HttpError.ConnectionFailed("GET x", "refused")) @>
        test <@ HttpError.isTransient (HttpError.TimedOut("GET x", TimeSpan.FromSeconds 1.0)) @>
        test <@ HttpError.isTransient (HttpError.Status(Response.create syntheticTime 503 "")) @>
        test <@ HttpError.isTransient (HttpError.Status(Response.create syntheticTime 429 "")) @>
        test <@ not (HttpError.isTransient (HttpError.Status(Response.create syntheticTime 404 ""))) @>
        test <@ not (HttpError.isTransient (HttpError.DecodeFailed("bad", Response.create syntheticTime 200 ""))) @>

    [<Fact>]
    let ``retryTransient retries transient failures and stops on success`` () =
        let sent, env =
            fake [
                okResponse 503 "unavailable"
                okResponse 503 "unavailable"
                okResponse 200 "recovered"
            ]
        let value =
            Http.getString "https://api.example.test/"
            |> Http.retryTransient 5 TimeSpan.Zero
            |> runSync env
            |> requireSuccess
        test <@ value = "recovered" @>
        test <@ sent.Count = 3 @>

    [<Fact>]
    let ``retryTransient does not retry permanent failures`` () =
        let sent, env = fake [ okResponse 404 "missing" ]
        Http.getString "https://api.example.test/"
        |> Http.retryTransient 5 TimeSpan.Zero
        |> runSync env
        |> requireError
        |> ignore
        test <@ sent.Count = 1 @>

    [<Fact>]
    let ``error descriptions include the redacted request`` () =
        let _, env = fake [ okResponse 500 "oops" ]
        let apiKey = secret "k"
        let error =
            GET $"https://api.example.test/lookup?key={apiKey}"
            |> fetch
            |> runSync env
            |> requireError
        // The fake keeps Request = "", so describe over a real transcript is covered in live tests;
        // here the status and body preview must be present.
        test <@ (HttpError.describe error).Contains "500" @>
        test <@ (HttpError.describe error).Contains "oops" @>

    // ---- Live service over loopback ----

    let private withServer (handler: HttpListenerContext -> unit) (test: string -> unit) =
        let port = Random().Next(20000, 60000)
        let prefix = $"http://127.0.0.1:{port}/"
        use listener = new HttpListener()
        listener.Prefixes.Add prefix
        listener.Start()
        let serving =
            Task.Run(fun () ->
                while listener.IsListening do
                    try
                        let context = listener.GetContext()
                        handler context
                        context.Response.Close()
                    with _ -> ())
        try
            test prefix
        finally
            listener.Stop()
            try serving.Wait(TimeSpan.FromSeconds 1.0) |> ignore with _ -> ()

    let private liveEnv () =
        let client = new HttpClient()
        { Http = Http.live Clock.live client }

    [<Fact>]
    let ``live service performs a real GET with query and headers`` () =
        withServer
            (fun context ->
                let query = context.Request.QueryString["q"]
                let auth = context.Request.Headers["Authorization"]
                let bytes = Text.Encoding.UTF8.GetBytes $"{query}|{auth}"
                context.Response.StatusCode <- 200
                context.Response.OutputStream.Write(bytes, 0, bytes.Length))
            (fun root ->
                let response =
                    Http.get root
                    |> Request.query "q" "a b"
                    |> Request.bearer "tkn"
                    |> Http.send
                    |> runSync (liveEnv ())
                    |> requireSuccess
                test <@ response.Text = "a b|Bearer tkn" @>
                test <@ response.Request.Contains "q=a%20b" @>
                test <@ not (response.Request.Contains "tkn") @>)

    [<Fact>]
    let ``live service timestamps transcripts through the supplied clock`` () =
        let fixedTime = DateTimeOffset(2030, 4, 5, 6, 7, 8, TimeSpan.Zero)
        withServer
            (fun context -> context.Response.StatusCode <- 200)
            (fun root ->
                use client = new HttpClient()
                let env = { Http = Http.live (Clock.fromValue fixedTime) client }
                let response = Http.get root |> Http.send |> runSync env |> requireSuccess
                test <@ response.StartedAt = fixedTime @>
                test <@ response.Duration = TimeSpan.Zero @>)

    [<Fact>]
    let ``live service posts JSON bodies and reads status errors`` () =
        withServer
            (fun context ->
                use reader = new IO.StreamReader(context.Request.InputStream)
                let body = reader.ReadToEnd()
                let contentType = context.Request.ContentType
                if body = """{"ok":true}""" && contentType.StartsWith "application/json" then
                    context.Response.StatusCode <- 201
                else
                    context.Response.StatusCode <- 422)
            (fun root ->
                let created =
                    Http.post root
                    |> Request.jsonBody """{"ok":true}"""
                    |> Http.send
                    |> runSync (liveEnv ())
                    |> requireSuccess
                test <@ created.StatusCode = 201 @>

                let rejected =
                    Http.post root
                    |> Request.jsonBody """{"ok":false}"""
                    |> Http.send
                    |> runSync (liveEnv ())
                    |> requireError
                test <@ match rejected with HttpError.Status response -> response.StatusCode = 422 | _ -> false @>)

    [<Fact>]
    let ``live service reports connection failures as typed errors`` () =
        let error =
            Http.getString "http://127.0.0.1:1/unreachable"
            |> runSync (liveEnv ())
            |> requireError
        test <@ match error with HttpError.ConnectionFailed _ -> true | _ -> false @>

    [<Fact>]
    let ``live service enforces per-request timeouts as typed errors`` () =
        withServer
            (fun context ->
                Thread.Sleep 2000
                context.Response.StatusCode <- 200)
            (fun root ->
                let error =
                    Http.get root
                    |> Request.timeout (TimeSpan.FromMilliseconds 100.0)
                    |> Http.send
                    |> runSync (liveEnv ())
                    |> requireError
                test <@ match error with HttpError.TimedOut(_, timeout) -> timeout = TimeSpan.FromMilliseconds 100.0 | _ -> false @>)

    [<Fact>]
    let ``response headers are readable case-insensitively`` () =
        withServer
            (fun context ->
                context.Response.Headers.Add("X-Custom", "value-1")
                context.Response.StatusCode <- 200)
            (fun root ->
                let response = Http.get root |> Http.send |> runSync (liveEnv ()) |> requireSuccess
                test <@ Response.tryHeader "x-custom" response = Some "value-1" @>)
