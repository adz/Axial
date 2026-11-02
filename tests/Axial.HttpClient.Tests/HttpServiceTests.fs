namespace Axial.Tests

open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.HttpClient
open Axial.HttpClient.DSL
open Axial
open Swensen.Unquote
open Xunit

type HttpTestEnv =
    { Http: IHttp; Clock: IClock }

    interface IHasHttp with
        member this.Http = this.Http

module HttpServiceTests =
    let private syntheticTime = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) // axial-allow-AXG004: immutable value is safe to share between tests
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
                member _.Send(request) =
                    Flow.delay (fun () ->
                        sent.Add request
                        Flow.fromResult (if queue.Count > 1 then queue.Dequeue() else queue.Peek())) }
        sent, { Http = service; Clock = Clock.live }

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
    let ``send preserves a service HttpError as a typed failure`` () =
        let expected = HttpError.ConnectionFailed("GET https://api.example.test/", "offline")
        let _, env = fake [ Error expected ]
        let actual = Http.get "https://api.example.test/" |> Http.send |> runSync env |> requireError
        test <@ actual = expected @>

    [<Fact>]
    let ``interrupting an active send remains an interruption`` () =
        use cancellation = new CancellationTokenSource()
        use started = new ManualResetEventSlim(false)
        let service =
            { new IHttp with
                member _.Send(_) =
                    flow {
                        let! token = Flow.cancellationToken
                        return! Flow.fromTask (fun _ ->
                            started.Set()
                            task {
                                do! Task.Delay(Timeout.Infinite, token)
                                return Response.create syntheticTime 200 "late"
                            })
                    } }
        let env = { Http = service; Clock = Clock.live }
        let running = Task.Run(fun () -> (Http.get "https://api.example.test/" |> Http.send).RunSynchronously(env, cancellationToken = cancellation.Token))
        Assert.True(started.Wait(TimeSpan.FromSeconds 5.0))
        cancellation.Cancel()
        test <@ match running.GetAwaiter().GetResult() with Exit.Failure Cause.Interrupt -> true | _ -> false @>

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
        { Http = Http.live client; Clock = Clock.live }

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
    let ``live service timestamps transcripts through the execution clock`` () =
        let fixedTime = DateTimeOffset(2030, 4, 5, 6, 7, 8, TimeSpan.Zero)
        withServer
            (fun context -> context.Response.StatusCode <- 200)
            (fun root ->
                use client = new HttpClient()
                let fixedClock = Clock.fromValue fixedTime
                let env = { Http = Http.live client; Clock = fixedClock }
                let response =
                    Http.get root
                    |> Http.send
                    |> Flow.withClock fixedClock
                    |> runSync env
                    |> requireSuccess
                test <@ response.StartedAt = fixedTime @>
                test <@ response.Duration = TimeSpan.Zero @>)

    [<Fact>]
    let ``live HTTP uses execution clock not construction clock`` () =
        let construction = Clock.fromValue(DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero))
        let execution = Clock.fromValue(DateTimeOffset(2025, 6, 7, 8, 9, 10, TimeSpan.Zero))
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(_, _) =
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent("ok"))) }
        use client = new HttpClient(handler)
        let service = Http.live client
        let env = { Http = service; Clock = execution }
        let first =
            Http.get "https://example.test/"
            |> Http.send
            |> Flow.withClock execution
            |> runSync env
            |> requireSuccess
        test <@ first.StartedAt = DateTimeOffset(2025, 6, 7, 8, 9, 10, TimeSpan.Zero) @>
        let secondClock = Clock.fromValue(DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero))
        let second =
            Http.get "https://example.test/"
            |> Http.send
            |> Flow.withClock secondClock
            |> runSync { env with Clock = secondClock }
            |> requireSuccess
        test <@ second.StartedAt = DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero) @>

    [<Fact>]
    let ``request timeout advances only with the runtime clock`` () = task {
        use entered = new ManualResetEventSlim(false)
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(_, cancellationToken) =
                    task {
                        entered.Set()
                        do! Task.Delay(Timeout.Infinite, cancellationToken)
                        return new HttpResponseMessage(HttpStatusCode.OK)
                    } }
        use client = new HttpClient(handler, Timeout = Timeout.InfiniteTimeSpan)
        let clock = Clock.fromValue(DateTimeOffset.UnixEpoch)
        let service = Http.live client
        let env = { Http = service; Clock = clock }
        let running =
            Http.get "https://example.test/"
            |> Request.timeout (TimeSpan.FromSeconds 5.)
            |> Http.send
            |> Flow.withClock clock
            |> fun source -> source.StartAsTask(env)
        Assert.True(entered.Wait(TimeSpan.FromSeconds 5.0))
        clock.AdvanceBy(TimeSpan.FromSeconds 4.)
        Assert.False(running.IsCompleted)
        clock.AdvanceBy(TimeSpan.FromSeconds 1.)
        let! exit = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        match exit with
        | Exit.Failure(Cause.Fail(HttpError.TimedOut(_, timeout))) -> Assert.Equal(TimeSpan.FromSeconds 5., timeout)
        | other -> failwithf "Expected request timeout, got %A" other
    }

    type private BlockingBodyStream() =
        inherit IO.Stream()

        member val Disposed = false with get, set

        override _.CanRead = true
        override _.CanSeek = false
        override _.CanWrite = false
        override _.Length = raise (NotSupportedException())
        override _.Position
            with get () = raise (NotSupportedException())
            and set _ = raise (NotSupportedException())
        override _.Flush() = ()
        override _.Seek(_, _) = raise (NotSupportedException())
        override _.SetLength _ = raise (NotSupportedException())
        override _.Read(_, _, _) = raise (NotSupportedException())
        override _.Write(_, _, _) = raise (NotSupportedException())

        override _.ReadAsync(_, _, _, cancellationToken) =
            Task.Delay(Timeout.Infinite, cancellationToken) |> fun delay -> task {
                do! delay
                return 0
            }

        override this.Dispose(disposing) =
            this.Disposed <- true
            base.Dispose disposing

    [<Fact>]
    let ``live HTTP samples duration from the execution clock`` () = task {
        use entered = new ManualResetEventSlim(false)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(_, _) =
                    entered.Set()
                    task {
                        do! release.Task
                        return new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent("ok"))
                    } }
        use client = new HttpClient(handler, Timeout = Timeout.InfiniteTimeSpan)
        let epoch = DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero)
        let clock = ManualClock(epoch)
        let env = { Http = Http.live client; Clock = clock }
        let running =
            Http.get "https://example.test/"
            |> Http.send
            |> Flow.withClock clock
            |> fun source -> source.StartAsTask env
        Assert.True(entered.Wait(TimeSpan.FromSeconds 5.0))
        clock.AdvanceBy(TimeSpan.FromSeconds 7.)
        release.TrySetResult() |> ignore
        let! exit = running.WaitAsync(TimeSpan.FromSeconds 10.0)
        match exit with
        | Exit.Success response ->
            Assert.Equal(epoch, response.StartedAt)
            Assert.Equal(TimeSpan.FromSeconds 7., response.Duration)
        | other -> failwithf "Expected success, got %A" other
    }

    [<Fact>]
    let ``same HTTP service isolates simultaneous roots and their durations`` () = task {
        let entered = [| for _ in 0..1 -> TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously) |]
        let release = [| for _ in 0..1 -> TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously) |]
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(request, token) = task {
                    let index = if request.RequestUri.AbsolutePath = "/a" then 0 else 1
                    entered[index].TrySetResult() |> ignore
                    do! release[index].Task.WaitAsync token
                    return new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(string index))
                } }
        use client = new HttpClient(handler, Timeout = Timeout.InfiniteTimeSpan)
        let service = Http.live client
        let first = ManualClock(DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero))
        let second = ManualClock(DateTimeOffset(2032, 1, 1, 0, 0, 0, TimeSpan.Zero))
        let run clock path =
            Http.get ("https://example.test/" + path) |> Http.send |> Flow.withClock clock
            |> fun source -> source.StartAsTask { Http = service; Clock = clock }
        let a = run first "a"
        let b = run second "b"
        try
            let! _ = Task.WhenAll(entered |> Array.map _.Task).WaitAsync(TimeSpan.FromSeconds 10.)
            Assert.False(a.IsCompleted)
            Assert.False(b.IsCompleted)
            first.AdvanceBy(TimeSpan.FromSeconds 7.)
            second.AdvanceBy(TimeSpan.FromSeconds 13.)
        finally
            for gate in release do gate.TrySetResult() |> ignore
        let! exits = Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds 10.)
        let responseA = requireSuccess exits[0]
        let responseB = requireSuccess exits[1]
        Assert.Equal(DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero), responseA.StartedAt)
        Assert.Equal(DateTimeOffset(2032, 1, 1, 0, 0, 0, TimeSpan.Zero), responseB.StartedAt)
        Assert.Equal(TimeSpan.FromSeconds 7., responseA.Duration)
        Assert.Equal(TimeSpan.FromSeconds 13., responseB.Duration)
        Assert.Equal("0", responseA.Text)
        Assert.Equal("1", responseB.Text)
    }

    [<Fact>]
    let ``request timeout covers a stalled response body and disposes it`` () = task {
        use entered = new ManualResetEventSlim(false)
        let stream = new BlockingBodyStream()
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(_, _) =
                    entered.Set()
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK, Content = new StreamContent(stream))) }
        use client = new HttpClient(handler, Timeout = Timeout.InfiniteTimeSpan)
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        let env = { Http = Http.live client; Clock = clock }
        let running =
            Http.get "https://example.test/"
            |> Request.timeout (TimeSpan.FromSeconds 5.)
            |> Http.send
            |> Flow.withClock clock
            |> fun source -> source.StartAsTask env
        Assert.True(entered.Wait(TimeSpan.FromSeconds 5.0))
        clock.AdvanceBy(TimeSpan.FromSeconds 5.)
        let! exit = running.WaitAsync(TimeSpan.FromSeconds 10.0)
        match exit with
        | Exit.Failure cause ->
            Assert.Contains(Cause.failures cause, fun error -> match error with HttpError.TimedOut _ -> true | _ -> false)
        | other -> failwithf "Expected request timeout, got %A" other
        Assert.True(stream.Disposed, "the canceled response body must be disposed")
    }

    [<Fact>]
    let ``finite native client timeout is recognized with a frozen manual clock`` () = task {
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(_, _) =
                    Task.FromException<HttpResponseMessage>(TaskCanceledException("native", TimeoutException("native timeout"))) }
        use client = new HttpClient(handler, Timeout = TimeSpan.FromSeconds 5.)
        let clock = Clock.fromValue DateTimeOffset.UnixEpoch
        let env = { Http = Http.live client; Clock = clock }
        let! exit = (Http.get "https://example.test/" |> Http.send |> Flow.withClock clock |> fun source -> source.StartAsTask env).WaitAsync(TimeSpan.FromSeconds 10.0)
        match exit with
        | Exit.Failure(Cause.Fail(HttpError.TimedOut(_, timeout))) -> Assert.Equal(TimeSpan.FromSeconds 5., timeout)
        | other -> failwithf "Expected native timeout, got %A" other
    }

    [<Fact>]
    let ``real native client timeout is reported as a typed timeout`` () = task {
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(_, cancellationToken) =
                    task {
                        do! Task.Delay(Timeout.Infinite, cancellationToken)
                        return new HttpResponseMessage(HttpStatusCode.OK)
                    } }
        use client = new HttpClient(handler, Timeout = TimeSpan.FromMilliseconds 100.)
        let env = { Http = Http.live client; Clock = Clock.live }
        let! exit = (Http.get "https://example.test/" |> Http.send |> fun source -> source.StartAsTask env).WaitAsync(TimeSpan.FromSeconds 10.0)
        match exit with
        | Exit.Failure(Cause.Fail(HttpError.TimedOut(_, timeout))) -> Assert.Equal(TimeSpan.FromMilliseconds 100., timeout)
        | other -> failwithf "Expected native timeout, got %A" other
    }

    [<Fact>]
    let ``invalid URL construction remains a typed request failure`` () =
        use client = new HttpClient()
        let env = { Http = Http.live client; Clock = Clock.live }
        let error = Http.get "http://[" |> Http.send |> runSync env |> requireError
        Assert.True(match error with HttpError.InvalidRequest _ -> true | _ -> false)

    type private ThrowingContent(marker: string, disposed: unit -> unit) =
        inherit ByteArrayContent([| 1uy |])
        override _.Dispose(disposing) =
            disposed ()
            base.Dispose disposing
            raise (InvalidOperationException marker)

    [<Fact>]
    let ``response and request disposals both run and retain both defects`` () =
        let mutable requestDisposed = false
        let mutable responseDisposed = false
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(message, _) =
                    message.Content <- new ThrowingContent("request-disposal-marker", fun () -> requestDisposed <- true)
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK,
                        Content = new ThrowingContent("response-disposal-marker", fun () -> responseDisposed <- true))) }
        use client = new HttpClient(handler)
        let env = { Http = Http.live client; Clock = Clock.live }
        let exit = Http.get "https://example.test/" |> Http.send |> runSync env
        Assert.True(responseDisposed)
        Assert.True(requestDisposed, "Request disposal was skipped after response disposal threw")
        match exit with
        | Exit.Failure cause ->
            let rec messages (error: exn) =
                match error with
                | :? AggregateException as aggregate -> aggregate.InnerExceptions |> Seq.collect messages
                | _ -> Seq.singleton error.Message
            let defects = Cause.defects cause |> Seq.collect messages |> Seq.toList
            Assert.Contains("response-disposal-marker", defects)
            Assert.Contains("request-disposal-marker", defects)
            Assert.Empty(Cause.failures cause)
        | other -> failwithf "Expected disposal defects, got %A" other

    type private CancelThenThrowOnDisposeStream() =
        inherit IO.Stream()

        override _.CanRead = true
        override _.CanSeek = false
        override _.CanWrite = false
        override _.Length = raise (NotSupportedException())
        override _.Position
            with get () = raise (NotSupportedException())
            and set _ = raise (NotSupportedException())
        override _.Flush() = ()
        override _.Seek(_, _) = raise (NotSupportedException())
        override _.SetLength _ = raise (NotSupportedException())
        override _.Read(_, _, _) = raise (NotSupportedException())
        override _.Write(_, _, _) = raise (NotSupportedException())

        override _.ReadAsync(_, _, _, cancellationToken) =
            let pending = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
            cancellationToken.Register(fun () -> pending.TrySetCanceled cancellationToken |> ignore) |> ignore
            pending.Task

        override _.Dispose(disposing) =
            base.Dispose disposing
            raise (InvalidOperationException "cleanup-defect-marker")

    [<Fact>]
    let ``request timeout retains a response disposal defect`` () = task {
        use entered = new ManualResetEventSlim(false)
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(_, _) =
                    entered.Set()
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK, Content = new StreamContent(new CancelThenThrowOnDisposeStream()))) }
        use client = new HttpClient(handler, Timeout = Timeout.InfiniteTimeSpan)
        let clock = Clock.fromValue(DateTimeOffset.UnixEpoch)
        let env = { Http = Http.live client; Clock = clock }
        let running =
            Http.get "https://example.test/"
            |> Request.timeout (TimeSpan.FromSeconds 5.)
            |> Http.send
            |> Flow.withClock clock
            |> fun source -> source.StartAsTask env
        Assert.True(entered.Wait(TimeSpan.FromSeconds 5.0))
        clock.AdvanceBy(TimeSpan.FromSeconds 5.)
        let! exit = running.WaitAsync(TimeSpan.FromSeconds 10.0)
        match exit with
        | Exit.Failure cause ->
            Assert.Contains(Cause.failures cause, fun error -> match error with HttpError.TimedOut _ -> true | _ -> false)
            Assert.Contains(Cause.defects cause, fun defect -> defect.Message.Contains "cleanup-defect-marker")
        | other -> failwithf "Expected a timeout with a cleanup defect, got %A" other
    }

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
                TestSupport.occupyThread (TimeSpan.FromSeconds 2.)
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
    let ``live service interruption during a request remains interruption`` () =
        use started = new ManualResetEventSlim(false)
        use release = new ManualResetEventSlim(false)
        withServer
            (fun context ->
                started.Set()
                release.Wait(TimeSpan.FromSeconds 5.0) |> ignore
                context.Response.StatusCode <- 200)
            (fun root ->
                use cancellation = new CancellationTokenSource()
                use client = new HttpClient()
                let environment = { Http = Http.live client; Clock = Clock.live }
                let running = Task.Run(fun () -> (Http.get root |> Http.send).RunSynchronously(environment, cancellationToken = cancellation.Token))
                try
                    Assert.True(started.Wait(TimeSpan.FromSeconds 5.0))
                    cancellation.Cancel()
                    test <@ match running.GetAwaiter().GetResult() with Exit.Failure Cause.Interrupt -> true | _ -> false @>
                finally release.Set())

    [<Fact>]
    let ``live service treats unsolicited handler cancellation as defect`` () =
        use handler =
            { new HttpMessageHandler() with
                member _.SendAsync(_, _) = Task.FromException<HttpResponseMessage>(OperationCanceledException("foreign")) }
        use client = new HttpClient(handler)
        let environment = { Http = Http.live client; Clock = Clock.live }
        match Http.get "https://example.test/" |> Http.send |> runSync environment with
        | Exit.Failure(Cause.Die (:? OperationCanceledException)) -> ()
        | other -> failwithf "Expected cancellation defect, got %A" other

    [<Fact>]
    let ``response headers are readable case-insensitively`` () =
        withServer
            (fun context ->
                context.Response.Headers.Add("X-Custom", "value-1")
                context.Response.StatusCode <- 200)
            (fun root ->
                let response = Http.get root |> Http.send |> runSync (liveEnv ()) |> requireSuccess
                test <@ Response.tryHeader "x-custom" response = Some "value-1" @>)

    [<Fact>]
    let ``live service rejects a declared body larger than the limit`` () =
        withServer
            (fun context ->
                let bytes = Array.create 4096 (byte 'a')
                context.Response.StatusCode <- 200
                context.Response.ContentLength64 <- int64 bytes.Length
                context.Response.OutputStream.Write(bytes, 0, bytes.Length))
            (fun root ->
                let error =
                    Http.get root
                    |> Request.maxResponseBytes 1024L
                    |> Http.send
                    |> runSync (liveEnv ())
                    |> requireError

                test <@ match error with HttpError.ResponseTooLarge(_, 1024L) -> true | _ -> false @>)

    [<Fact>]
    let ``live service stops reading a streamed body at the limit`` () =
        withServer
            (fun context ->
                context.Response.StatusCode <- 200
                context.Response.SendChunked <- true
                let chunk = Array.create 1024 (byte 'a')
                for _ in 1..8 do
                    context.Response.OutputStream.Write(chunk, 0, chunk.Length))
            (fun root ->
                let limited =
                    Http.get root
                    |> Request.maxResponseBytes 2048L
                    |> Http.send
                    |> runSync (liveEnv ())
                    |> requireError

                let unlimited = Http.get root |> Http.send |> runSync (liveEnv ()) |> requireSuccess

                test <@ match limited with HttpError.ResponseTooLarge(_, 2048L) -> true | _ -> false @>
                test <@ unlimited.Body.Length = 8192 @>)

    [<Fact>]
    let ``the response limit also applies to any IHttp implementation`` () =
        let _, env = fake [ okResponse 200 (String('x', 100)) ]

        let error =
            Http.get "https://api.example.test/"
            |> Request.maxResponseBytes 10L
            |> Http.send
            |> runSync env
            |> requireError

        test <@ match error with HttpError.ResponseTooLarge(_, 10L) -> true | _ -> false @>
        test <@ (Http.get "https://api.example.test/" |> Request.maxResponseBytes 10L |> Request.plan).MaxResponseBytes = Some 10L @>
