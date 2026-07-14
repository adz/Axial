---
weight: 90
title: Axial.Flow.HttpClient
linkTitle: HTTP
description: Build typed HTTP requests, decode responses safely, and retry transient failures.
---

# HTTP Clients

This page shows the shortest path from `HttpClient` boilerplate to a typed, testable HTTP workflow.

A direct `HttpClient` call mixes four failure channels into exceptions and manual status checks:

```fsharp
// Untracked: exceptions for transport, manual status checks, unchecked parsing.
let! response = client.GetAsync($"https://api.example.com/users/{userId}") |> Async.AwaitTask
response.EnsureSuccessStatusCode() |> ignore
let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
let user = parseUser body // throws on bad payloads
```

The same call as an Axial workflow:

```fsharp
open Axial.Flow.HttpClient
open Axial.Flow.HttpClient.DSL

let user =
    GET $"https://api.example.com/users/{userId}"
    |> bearer token
    |> fetchJson decodeUser
```

`user` is a `Flow<#IHas<IHttp>, HttpError, User>`. The URL hole is URL-encoded as one value, the bearer token is
redacted from every plan and error transcript, connection failures, timeouts, unexpected statuses, and decode
failures all arrive as one typed `HttpError`, and nothing is sent until a Flow runtime runs the workflow.

## Two Levels

`Axial.Flow.HttpClient` has two deliberate levels:

1. **The `Http` and `Request` modules** wrap the common `HttpClient` operations with explicit requests, typed
   errors, and service-based execution. Use them when you want full control over every request field.
2. **The `DSL` module** adds interpolated URL builders (`GET $"..."`), pipe-friendly configuration, and terminal
   verbs (`fetch`, `fetchText`, `fetchJson`) for the everyday call that should read as one line.

Both levels build the same immutable `HttpRequest` value, so they mix freely: start a request with `GET $"..."`
and finish it with `Request.expect [ 200; 404 ] >> Http.sendResult`.

## Mental Model

1. `Http.get`/`GET $"..."` create an immutable `HttpRequest`. Construction never performs I/O.
2. `Request.*` and DSL combinators (`query`, `bearer`, `timeout`, `jsonBody`, `expect`) configure it.
3. `Http.send`, `fetch`, `fetchText`, or `fetchJson` convert it to `Flow<#IHas<IHttp>, HttpError, _>`.
4. The Flow runtime resolves `IHttp` from the environment and performs the exchange.

Because the service boundary is one `IHttp.Send` method, a complete test fake is a few lines, and
`Request.plan` renders a redacted description of any request without sending it.

## Choose A Guide

- [Requests](requests/): safe interpolated URLs, query parameters, headers, bodies, and secret redaction.
- [Responses and errors](responses-and-errors/): response transcripts, typed JSON decoding, and the `HttpError` model.
- [Reliability](reliability/): per-request timeouts, expected statuses, and transient-failure retries.
- [Testing and layers](testing-and-layers/): fakes, the live `HttpClient` service, and layer composition.
