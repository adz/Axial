module Axial.Tests.HttpLogSinkTests

open System
open System.Threading.Tasks
open Axial
open Axial.PlatformService
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
    let endpoint = Axial.Hosting.AspNetCore.FlowEndpoint.flowEndpoint (fun _ -> ()) Results.BadRequest source
    let! _ = endpoint.Invoke context
    let entries = provider.Entries
    Assert.Equal(2, entries.Length)
    Assert.Equal("Axial.Endpoint", entries.[0].Category)
    Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, entries.[0].Level)
    Assert.Equal("request-message", entries.[0].Message)
    Assert.Equal("child-message", entries.[1].Message)
    Assert.Same(error, entries.[1].Error.Value)
    for entry in entries do
        Assert.Equal(box "req-2", entry.Scope.["request"])
        Assert.Equal(box "job-2", entry.Scope.["job"])
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
    Assert.Equal(Axial.LogLevel.Information, entries.[0].Level)
    Assert.Equal("request-message", entries.[0].Message)
    Assert.Equal(Axial.LogLevel.Error, entries.[1].Level)
    Assert.Equal("child-message", entries.[1].Message)
    Assert.Same(error, entries.[1].Error.Value)
    for entry in entries do
        Assert.Equal("req-3", entry.Annotations.["request"])
        Assert.Contains(("job", box "job-3"), entry.Locals)
}

