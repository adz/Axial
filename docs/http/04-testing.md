---
title: Testing HTTP
description: Fake the one-method HTTP service, wire the live HttpClient service, and compose layers.
---

# Testing And Layers

## A Complete Fake In A Few Lines

The service has one method, and `Response.create` builds synthetic transcripts from an explicit timestamp:

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.HttpClient
open Axial.PlatformService

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
type User = { Id: int; Name: string }

/// A hand-written decoder for {"id":1,"name":"Ada"}; a JSON library's decoder fits the same signature.
let decodeUser (json: string) : Result<User, string> =
    let found = Text.RegularExpressions.Regex.Match(json, """^\{"id":(\d+),"name":"([^"]*)"\}$""")

    if found.Success then Ok { Id = int found.Groups[1].Value; Name = found.Groups[2].Value }
    else Error $"not a user: {json}"

type TestEnv =
    { Http: IHttp }
    interface IHasHttp with
        member this.Http = this.Http

let stub status body =
    let startedAt = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    { Http =
        { new IHttp with
            member _.Send(_) = Flow.succeed (Response.create startedAt status body) } }
```

A test runs the workflow against the stub and checks the result, here with `shouldEqual`; an xUnit test would use its
own assertion:

```fsharp run
Http.getJson decodeUser "https://api.example.test/users/1"
|> Flow.run (stub 200 """{"id":1,"name":"Ada"}""")
|> shouldEqual (Exit.Success { Id = 1; Name = "Ada" })
```

Because the fake receives the full `HttpRequest`, tests can also assert on what was sent: method, URL, query,
headers, and body are all plain data. `IHttp.Send` returns a `Flow`, so a fake reports a failure with `Flow.fail`,
not a bare `Result`:

```fsharp
/// A fake that always fails with a per-request timeout; no network and no clock are involved.
let alwaysTimesOut =
    { Http =
        { new IHttp with
            member _.Send(request) =
                Flow.fail (HttpError.TimedOut(Request.render request, TimeSpan.FromSeconds 5.0)) } }
```

Returning a typed failure such as `HttpError.TimedOut` from a fake exercises retry and fallback paths
deterministically, with no network and no clock.

### A scripted double for sequences

When a test needs several requests to answer in turn — two transient failures, then success — a scripted double
dequeues one reply per request and records what was sent. The reliability guide uses this shape for its retry
examples:

```fsharp
/// An HTTP service that replies with each status and body in turn, and records the requests it received.
type ScriptedHttp(replies: (int * string) list) =
    let remaining = Collections.Generic.Queue(replies)
    let sent = ResizeArray<HttpRequest>()
    member _.Sent = List.ofSeq sent
    member this.Env = { Http = this }

    interface IHttp with
        member _.Send(request) =
            Flow.delay (fun () ->
                sent.Add request
                let status, body = remaining.Dequeue()
                Flow.succeed (Response.create (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) status body))
```

Build one with the replies it should return in order, run the workflow against its `Env`, then assert on `Sent` to
check how many attempts were made and what each request carried.

## The Live Service

`Http.live` adapts an explicit `IClock` and one `HttpClient`; `Layer.succeed (Http.live …)` exposes them as a layer:

```fsharp

type AppEnv =
    { Http: IHttp }
    interface IHasHttp with
        member this.Http = this.Http

let appLayer (clock: IClock) (client: HttpClient) : Layer<unit, HttpError, AppEnv> =
    layer {
        let! http = Layer.succeed (Http.live clock client)
        return { Http = http }
    }

let runUserLookup (client: HttpClient) =
    Http.getJson decodeUser "users/1"
    |> Layer.provide (appLayer Clock.live client)
    |> Flow.run ()
```

`runUserLookup` sends the relative URL `"users/1"`, so the caller must supply a `client` whose `BaseAddress` is
set, or the lookup must use an absolute URL. Without a base address, `Http.live` fails the request with
`HttpError.InvalidRequest`.

Reuse one `HttpClient` per application: connection pooling, DNS rotation handlers,
and proxy settings remain `HttpClient` concerns. Axial adds the typed request/response boundary on top
without hiding the client or the clock used for transcript timestamps and durations. Tests can pass a `ManualClock`
(`Clock.fromValue`) or another `IClock` fake for deterministic time.

Base addresses configured on the client work as usual: relative request URLs resolve against
`client.BaseAddress`:

```fsharp
let apiClient () = new HttpClient(BaseAddress = Uri "https://api.example.com/")
// Http.getJson decodeUser "users/1" now resolves to https://api.example.com/users/1
```

## Composing With Other Services

Service records compose the same way as the other platform packages:

```fsharp
type WorkerEnv =
    { Http: IHttp
      Variables: IEnvironmentVariables }
    interface IHasHttp with member this.Http = this.Http
    interface IHasEnvironmentVariables with member this.EnvironmentVariables = this.Variables
```

The same pattern adds `IHasProcess` for `Axial.Process`, or any other package's contract.

A workflow that needs both declares `Flow<WorkerEnv, ...>` (or stays polymorphic with
`'env :> IHasHttp` constraints) and runs against one environment value.

## Portability

Request construction, the `Request`/`Response` modules, `HttpError`, and the DSL are portable and compile under
Fable. The `Http.live` service and `Layer.succeed (Http.live …)` are .NET-only: on other hosts, implement `IHttp` over the
platform's fetch primitive and provide it through the same environment record.

## When Not To Fake

Fakes verify workflow logic, not server behavior. A small number of tests against a real endpoint (a
loopback listener works well) cover the live service's encoding, header, timeout, and error mapping; the
package's own test suite does this.
