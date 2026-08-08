---
weight: 40
title: Testing And Layers
description: Fake the one-method HTTP service, wire the live HttpClient service, and compose layers.
---

# Testing And Layers

This page shows how the single `IHttp.Send` boundary makes HTTP workflows testable without a mocking library.

## A Complete Fake In A Few Lines

The whole service surface is one method, and `Response.create` builds synthetic transcripts from an explicit timestamp:

```fsharp
type TestEnv =
    { Http: IHttp }
    interface IHas<IHttp> with
        member this.Service = this.Http

let stub status body =
    let startedAt = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    { Http =
        { new IHttp with
            member _.Send(_, _) = async { return Ok(Response.create startedAt status body) } } }

[<Fact>]
let ``decodes the user payload`` () =
    let env = stub 200 """{"id":1,"name":"Ada"}"""
    let result = Http.getJson decodeUser "https://api.example.test/users/1" |> Flow.runSync env
    test <@ result = Exit.Success { Id = 1; Name = "Ada" } @>
```

Because the fake receives the full `HttpRequest`, tests can also assert on what was sent — method, URL, query,
headers, and body are all plain data. Returning `Error(HttpError.TimedOut(...))` from a fake exercises retry and
fallback paths deterministically, with no network and no clock.

## The Live Service

`Http.live` adapts an explicit `IClock` and one `HttpClient`; `Http.layer` exposes them as a layer:

```fsharp
type AppEnv =
    { Http: IHttp }
    interface IHas<IHttp> with
        member this.Service = this.Http

let appLayer (clock: IClock) (client: HttpClient) : Layer<unit, Never, AppEnv> =
    layer {
        let! http = Http.layer clock client
        return { Http = http }
    }

workflow
|> Flow.provide (appLayer Clock.live client)
|> Flow.runSync ()
```

Reuse one `HttpClient` per application, exactly as .NET recommends: connection pooling, DNS rotation handlers,
and proxy settings stay standard `HttpClient` concerns. Axial adds the typed request/response boundary on top
without hiding the client or the clock used for transcript timestamps and durations. Tests can pass `Clock.fromValue`
or another `IClock` fake for deterministic time.

Base addresses configured on the client work as usual — relative request URLs resolve against
`client.BaseAddress`:

```fsharp
let client = new HttpClient(BaseAddress = Uri "https://api.example.com/")
// Http.get "users/1" now resolves to https://api.example.com/users/1
```

## Composing With Other Services

Service records compose the same way as the other platform packages:

```fsharp
type WorkerEnv =
    { Http: IHttp
      Process: IProcess }
    interface IHas<IHttp> with member this.Service = this.Http
    interface IHas<IProcess> with member this.Service = this.Process
```

A workflow that needs both declares `Flow<WorkerEnv, ...>` (or stays polymorphic with
`'env :> IHas<IHttp>` constraints) and runs against one environment value.

## Portability

Request construction, the `Request`/`Response` modules, `HttpError`, and the DSL are portable and compile under
Fable. The `Http.live` service and `Http.layer` are .NET-only: on other hosts, implement `IHttp` over the
platform's fetch primitive and provide it through the same environment record.

## When Not To Fake

Fakes verify workflow logic, not server behavior. Keep a small number of tests against a real endpoint (a
loopback listener works well) to cover the live service's encoding, header, timeout, and error mapping — the
package's own test suite does exactly this.
