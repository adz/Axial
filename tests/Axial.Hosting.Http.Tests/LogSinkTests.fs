module Axial.Tests.HttpLogSinkTests

open System
open System.Threading.Tasks
open Axial
open Axial
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Xunit

[<Fact>]
let ``ASP endpoint installs a per request logger inherited by forks`` () = task {
    let provider = new HostLogProvider()
    use factory = LoggerFactory.Create(fun builder -> builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace).AddProvider(provider) |> ignore)
    use services = ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider()
    let context = DefaultHttpContext()
    context.RequestServices <- services
    use requestScope = factory.CreateLogger("request").BeginScope(dict ["asp.scope", box "ambient"])
    let mutable disabledEnabled = true
    let local = FiberLocal.named "job" "unset"
    let error = InvalidOperationException("endpoint-error")
    let source: Flow<Axial.Hosting.AspNetCore.HttpEndpointEnv<unit>, Axial.Hosting.AspNetCore.EndpointError<string>, IResult> =
        (flow {
            do! Log.debug "disabled"
            do! Log.info "request-message"
            let! child = Log.errorExn error "child-message" |> Flow.forkNamed "request-log-child"
            do! Fiber.join child
            return Results.NoContent()
        }) |> Flow.annotate "request" "req-2" |> FiberLocal.locally "job-2" local
           |> Flow.mapLogSink (fun sink -> disabledEnabled <- sink.IsEnabled Axial.LogLevel.Debug; sink)
    let endpoint = Axial.Hosting.AspNetCore.FlowEndpoint.flowEndpoint (fun _ -> ()) Results.BadRequest source
    let! _ = endpoint.Invoke context
    let entries = provider.Entries
    Assert.Equal(2, entries.Length)
    Assert.False(disabledEnabled)
    Assert.Equal("Axial.Endpoint", entries.[0].Category)
    Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, entries.[0].Level)
    Assert.Equal("request-message", entries.[0].Message)
    Assert.Equal("child-message", entries.[1].Message)
    Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entries.[1].Level)
    Assert.Same(error, entries.[1].Error.Value)
    for entry in entries do
        Assert.Equal(box "req-2", entry.Scope.["request"])
        Assert.Equal(box "job-2", entry.Scope.["job"])
        Assert.Equal(box "ambient", entry.Scope.["asp.scope"])
}

[<Fact>]
let ``ASP endpoint caches one logger and sink per logger factory`` () = task {
    use realFactory = LoggerFactory.Create(fun _ -> ())
    let mutable created = 0

    let countingFactory =
        { new ILoggerFactory with
            member _.CreateLogger category =
                created <- created + 1
                realFactory.CreateLogger category
            member _.AddProvider provider = realFactory.AddProvider provider
            member _.Dispose() = () }

    use services = ServiceCollection().AddSingleton<ILoggerFactory>(countingFactory).BuildServiceProvider()
    let source: Flow<Axial.Hosting.AspNetCore.HttpEndpointEnv<unit>, Axial.Hosting.AspNetCore.EndpointError<string>, IResult> =
        Flow.ok (Results.NoContent())
    let endpoint = Axial.Hosting.AspNetCore.FlowEndpoint.flowEndpoint (fun _ -> ()) Results.BadRequest source
    let invoke () =
        let context = DefaultHttpContext()
        context.RequestServices <- services
        endpoint.Invoke context

    let! _ = invoke ()
    let! _ = invoke ()
    Assert.Equal(1, created)
}

[<Fact>]
let ``ASP custom sink callback runs once per request without sharing request buffers`` () = task {
    use factory = LoggerFactory.Create(fun _ -> ())
    use services = ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider()
    let first = Log.memory ()
    let second = Log.memory ()
    let mutable currentSink: ILogSink = first
    let mutable calls = 0
    let source: Flow<Axial.Hosting.AspNetCore.HttpEndpointEnv<string>, Axial.Hosting.AspNetCore.EndpointError<string>, IResult> = flow {
        let! environment = Flow.env
        do! Log.info environment.App
        return Results.NoContent()
    }
    let endpoint = Axial.Hosting.AspNetCore.FlowEndpoint.flowEndpointWithOptions
                       { LogSink = Some(fun supplied ->
                           Assert.Same(factory, supplied)
                           calls <- calls + 1
                           currentSink) }
                       (fun context -> context.TraceIdentifier) Results.BadRequest source
    let invoke id sink =
        currentSink <- sink
        let context = DefaultHttpContext()
        context.RequestServices <- services
        context.TraceIdentifier <- id
        endpoint.Invoke context
    let! _ = invoke "request-a" first
    let! _ = invoke "request-b" second
    Assert.Equal("request-a", Assert.Single(first.Entries).Message)
    Assert.Equal("request-b", Assert.Single(second.Entries).Message)
    Assert.Equal(2, calls)
}

[<Fact>]
let ``GenHTTP endpoint installs its optional sink and forks inherit it`` () = task {
    let log = Log.memory ()
    let sink = LogSink.minLevel Axial.LogLevel.Information log
    let local = FiberLocal.named "job" "unset"
    let error = InvalidOperationException("child-error")
    let stop = InvalidOperationException("stop-before-materializing-response")
    let source: Flow<Axial.Hosting.GenHttp.HttpEndpointEnv<unit>, Axial.Hosting.GenHttp.EndpointError<string>, Axial.Hosting.GenHttp.HttpResponse> =
        (flow {
            do! Log.debug "disabled"
            do! Log.info "request-message"
            let! child = Log.errorExn error "child-message" |> Flow.forkNamed "genhttp-log-child"
            do! Fiber.join child
            return! Flow.die stop
        }) |> Flow.annotate "request" "req-3" |> FiberLocal.locally "job-3" local
    let endpoint = Axial.Hosting.GenHttp.FlowEndpoint.flowEndpointWithOptions
                       { LogSink = Some sink } (fun _ -> ())
                       (Axial.Hosting.GenHttp.Response.text GenHTTP.Api.Protocol.ResponseStatus.BadRequest) source
    let! thrown = Assert.ThrowsAsync<InvalidOperationException>(Func<Task>(fun () -> endpoint.Invoke(Unchecked.defaultof<_>).AsTask() :> Task))
    Assert.Same(stop, thrown)
    let entries = log.Entries
    Assert.Equal(2, entries.Count)
    Assert.False(sink.IsEnabled Axial.LogLevel.Debug)
    Assert.Equal(Axial.LogLevel.Information, entries.[0].Level)
    Assert.Equal("request-message", entries.[0].Message)
    Assert.Equal(Axial.LogLevel.Error, entries.[1].Level)
    Assert.Equal("child-message", entries.[1].Message)
    Assert.Same(error, entries.[1].Error.Value)
    for entry in entries do
        Assert.Equal("req-3", entry.Annotations.["request"])
        Assert.Contains(("job", box "job-3"), entry.Locals)
}

[<Theory>]
[<InlineData(false)>]
[<InlineData(true)>]
let ``ASP endpoint option replaces or silences and scoped overrides win`` silent = task {
    use factory = LoggerFactory.Create(fun _ -> ())
    use services = ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider()
    let context = DefaultHttpContext()
    context.RequestServices <- services
    let outer = Log.memory ()
    let inner = Log.memory ()
    let sink = if silent then LogSink.none else LogSink.minLevel Axial.LogLevel.Information outer
    let source: Flow<Axial.Hosting.AspNetCore.HttpEndpointEnv<unit>, Axial.Hosting.AspNetCore.EndpointError<string>, IResult> = flow {
        do! Log.debug "disabled"
        do! Log.info "outer"
        let! child = Log.info "inner" |> Flow.withLogSink inner |> Flow.fork
        do! Fiber.join child
        return Results.NoContent()
    }
    let endpoint = Axial.Hosting.AspNetCore.FlowEndpoint.flowEndpointWithOptions
                       { LogSink = Some(fun supplied -> Assert.Same(factory, supplied); sink) }
                       (fun _ -> ()) Results.BadRequest source
    let! _ = endpoint.Invoke context
    Assert.Equal((if silent then 0 else 1), outer.Entries.Count)
    Assert.Equal("inner", Assert.Single(inner.Entries).Message)
}

[<Fact>]
let ``ASP endpoint no installation option needs no logger factory`` () = task {
    let memory = Log.memory ()
    let source: Flow<Axial.Hosting.AspNetCore.HttpEndpointEnv<unit>, Axial.Hosting.AspNetCore.EndpointError<string>, IResult> =
        (flow {
            do! Log.info "manual"
            return Results.NoContent()
        }) |> Flow.withLogSink memory
    let endpoint = Axial.Hosting.AspNetCore.FlowEndpoint.flowEndpointWithOptions
                       { LogSink = None } (fun _ -> ()) Results.BadRequest source
    let! _ = endpoint.Invoke(DefaultHttpContext())
    Assert.Equal("manual", Assert.Single(memory.Entries).Message)
}

[<Theory>]
[<InlineData(false)>]
[<InlineData(true)>]
let ``GenHTTP scoped sink survives default and disabled endpoint policies`` disabled = task {
    let inner = Log.memory ()
    let stop = InvalidOperationException("stop-before-response")
    let source: Flow<Axial.Hosting.GenHttp.HttpEndpointEnv<unit>, Axial.Hosting.GenHttp.EndpointError<string>, Axial.Hosting.GenHttp.HttpResponse> =
        (flow {
            do! Log.info "manual"
            return! Flow.die stop
        }) |> Flow.withLogSink inner
    Assert.True(Option.isNone Axial.Hosting.GenHttp.EndpointOptions.Default.LogSink)
    let mapError = Axial.Hosting.GenHttp.Response.text GenHTTP.Api.Protocol.ResponseStatus.BadRequest
    let endpoint =
        if disabled then Axial.Hosting.GenHttp.FlowEndpoint.flowEndpointWithOptions
                             { LogSink = Some LogSink.none } (fun _ -> ()) mapError source
        else Axial.Hosting.GenHttp.FlowEndpoint.flowEndpoint (fun _ -> ()) mapError source
    let! thrown = Assert.ThrowsAsync<InvalidOperationException>(Func<Task>(fun () -> endpoint.Invoke(Unchecked.defaultof<_>).AsTask() :> Task))
    Assert.Same(stop, thrown)
    Assert.Equal("manual", Assert.Single(inner.Entries).Message)
}

