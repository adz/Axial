---
title: Responses And Errors
description: Read complete response transcripts, decode JSON into typed values, and match one typed error.
---

# Responses And Errors

This page shows how one response transcript and one error type replace scattered status checks and exception
handling.

## The Response Transcript

Every exchange produces a complete `HttpResponse`. The examples on this page run against a scripted service that
replies with fixed statuses and bodies:

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
open Axial.HttpClient.DSL

type TestEnv =
    { Http: IHttp }
    interface IHasHttp with
        member this.Http = this.Http

/// An HTTP service that replies with each status and body in turn, and records the requests it received.
type ScriptedHttp(replies: (int * string) list) =
    let remaining = Collections.Generic.Queue(replies)
    let sent = ResizeArray<HttpRequest>()
    member _.Sent = List.ofSeq sent
    member this.Env = { Http = this }

    interface IHttp with
        member _.Send(request, _) =
            sent.Add request
            let status, body = remaining.Dequeue()
            async { return Ok(Response.create (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) status body) }

type User = { Id: int; Name: string }

/// A hand-written decoder for {"id":1,"name":"Ada"}; a JSON library's decoder fits the same signature.
let decodeUser (json: string) : Result<User, string> =
    let found = Text.RegularExpressions.Regex.Match(json, """^\{"id":(\d+),"name":"([^"]*)"\}$""")

    if found.Success then Ok { Id = int found.Groups[1].Value; Name = found.Groups[2].Value }
    else Error $"not a user: {json}"
```

```fsharp
let summary : Flow<TestEnv, HttpError, int * string * string option> =
    flow {
        let! response = Http.get "https://api.example.com/users" |> Http.send
        let etag = response |> Response.tryHeader "ETag"   // case-insensitive
        return response.StatusCode, response.Text, etag
    }
```

```fsharp run
summary |> Flow.run (ScriptedHttp([ 200, "[]" ]).Env) |> shouldEqual (Exit.Success(200, "[]", None))
```

`response.Duration` records how long the exchange took, measured with the service's `IClock`.

- `StatusCode`, `ReasonPhrase`, and `Headers` (response plus content headers, in arrival order).
- `Body` is the exact bytes; `Text` is decoded with the response charset, defaulting to UTF-8.
- `Request` is the redacted request line, so the transcript is safe to log as-is.
- `StartedAt` and `Duration` time the full exchange including body download.

## Typed JSON Decoding

`Response.json` and the `fetchJson`/`Http.getJson` terminals take a decoder of type
`string -> Result<'value, string>`. Any JSON library fits that shape:

```fsharp
let user (userId: int) : Flow<TestEnv, HttpError, User> =
    GET $"https://api.example.com/users/{userId}"
    |> fetchJson decodeUser   // Reified.Schema.Json, Thoth, or hand-written
```

```fsharp run
user 1 |> Flow.run (ScriptedHttp([ 200, """{"id":1,"name":"Ada"}""" ]).Env) |> shouldEqual (Exit.Success { Id = 1; Name = "Ada" })

match user 1 |> Flow.run (ScriptedHttp([ 200, "not json" ]).Env) with
| Exit.Failure(Cause.Fail(HttpError.DecodeFailed _)) -> ()
| other -> failwithf "expected a decode failure, got %A" other
```

A decoder failure becomes `HttpError.DecodeFailed(message, response)`. It carries the full transcript, so the
error handler can log the offending payload without re-fetching it.

To POST a value and decode the reply in one step:

```fsharp
let encodeUser (user: User) = $"{{\"id\":{user.Id},\"name\":\"{user.Name}\"}}"

let created (newUser: User) : Flow<TestEnv, HttpError, User> =
    Http.postJson encodeUser decodeUser "https://api.example.com/users" newUser
```

```fsharp run
let service = ScriptedHttp([ 201, """{"id":7,"name":"Ada"}""" ])
created { Id = 0; Name = "Ada" } |> Flow.run service.Env |> shouldEqual (Exit.Success { Id = 7; Name = "Ada" })
service.Sent |> List.map (Request.plan >> _.Body) |> shouldEqual [ "application/json (22 characters)" ]
```

## One Error Type

Every way an HTTP call can fail is one case of `HttpError`:

```fsharp
let explain (error: HttpError) =
    match error with
    | HttpError.InvalidRequest message -> $"bad request: {message}"             // malformed URL or request construction
    | HttpError.ConnectionFailed(_, message) -> $"unreachable: {message}"       // DNS, refused, dropped connection
    | HttpError.TimedOut(_, timeout) -> $"no reply within {timeout}"            // per-request timeout elapsed
    | HttpError.Status response -> $"status {response.StatusCode}"              // outside the expectation, full transcript
    | HttpError.DecodeFailed(message, _) -> $"unexpected body: {message}"       // did not decode, full transcript
    | HttpError.ResponseTooLarge(_, limit) -> $"body over {limit} bytes"        // over Request.maxResponseBytes
```

```fsharp run
match user 1 |> Flow.run (ScriptedHttp([ 503, "" ]).Env) with
| Exit.Failure(Cause.Fail error) -> explain error |> shouldEqual "status 503"
| other -> failwithf "expected a status error, got %A" other
```

Interrupting the workflow is not one of them: the request is abandoned and the flow ends with `Cause.Interrupt`,
so no error handler has to recognize a cancellation it did not ask for.

`HttpError.describe` formats any case with its redacted request context and a bounded body preview, so a single
`Flow.mapError HttpError.describe` produces loggable messages. `HttpError.tryResponse` extracts the transcript
from the cases that carry one.

## Statuses Are Data, Not Exceptions

`Http.send` fails with `HttpError.Status` for anything outside the request's expectation (2xx by default).
When a "failure" status is a normal outcome, widen the expectation and branch on the code:

```fsharp
let findUser (userId: int) : Flow<TestEnv, HttpError, User option> =
    flow {
        let! response =
            GET $"https://api.example.com/users/{userId}"
            |> expect [ 200; 404 ]
            |> fetch
        if response.StatusCode = 404 then return None
        else return! response |> Response.json decodeUser |> Result.map Some
    }
```

```fsharp run
findUser 9 |> Flow.run (ScriptedHttp([ 404, "" ]).Env) |> shouldEqual (Exit.Success None)
findUser 1 |> Flow.run (ScriptedHttp([ 200, """{"id":1,"name":"Ada"}""" ]).Env) |> shouldEqual (Exit.Success(Some { Id = 1; Name = "Ada" }))
```

`Http.sendResult` skips status interpretation entirely and returns whatever arrived; use it when a proxy or
health check needs the raw exchange.

## When Not To Decode

`fetchText` and `fetchBytes` return the body directly for HTML scraping, file downloads, and pass-through
proxying. Use `fetchJson` only when the payload should become a typed value; decoding a body you will
immediately re-serialize wastes the transcript you already have.
