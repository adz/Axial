---
title: Reliability
description: Enforce per-request timeouts and retry only the failures that can actually recover.
---

# Reliability

This page shows how typed errors turn timeout and retry policy into ordinary, testable code.

## Per-Request Timeouts

`HttpClient.Timeout` is one global setting that throws `TaskCanceledException`, indistinguishable from real
cancellation. An Axial timeout is per request and produces a dedicated typed error:

The examples on this page run against a scripted service that replies with fixed statuses in turn and records each
request, so they can check how many attempts were made:

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
let slowReport : Flow<TestEnv, HttpError, HttpResponse> =
    GET $"https://api.example.com/slow-report"
    |> timeout (TimeSpan.FromSeconds 5.0)
    |> fetch
// Fails with HttpError.TimedOut(request, 5s). Interrupting the workflow is Cause.Interrupt, not an HttpError.
```

The live service enforces the timeout with a linked cancellation source, so the connection is torn down when the
deadline passes, not merely abandoned.

## Retry Only Transient Failures

Retrying a 404 or a decode failure wastes time and can duplicate side effects. `HttpError.isTransient`
classifies exactly the failures where a retry can help: connection failures, timeouts, and 408/429/5xx statuses.

```fsharp
let firstUser : Flow<TestEnv, HttpError, User> =
    Http.getJson decodeUser "https://api.example.com/users/1"
    |> Http.retryTransient 3 (TimeSpan.FromMilliseconds 1.0)
```

Two `503` replies are transient, so the third attempt succeeds:

```fsharp run
let flaky = ScriptedHttp([ 503, ""; 503, ""; 200, """{"id":1,"name":"Ada"}""" ])
firstUser |> Flow.run flaky.Env |> shouldEqual (Exit.Success { Id = 1; Name = "Ada" })
flaky.Sent.Length |> shouldEqual 3
```

`retryTransient 3` makes up to 3 retries after the first attempt, with exponential backoff (200ms, 400ms, 800ms).
A permanent failure such as `HttpError.Status 404` or `HttpError.DecodeFailed` fails immediately on the first
attempt. The DSL shorthand `withRetries 3` applies the same schedule with a 200ms base delay. To combine it with other
schedules, take the schedule itself: `HttpError.transientRetry 3 (TimeSpan.FromMilliseconds 200.0)` is the
`Schedule` that `retryTransient` passes to `Flow.retry`.

For full control, describe the retry yourself and pass it to `Flow.retry`. A `Retry` record names each choice:

```fsharp
let notRateLimited error =
    HttpError.isTransient error
    && (match error with HttpError.Status r -> r.StatusCode <> 429 | _ -> true)

let patientUser : Flow<TestEnv, HttpError, User> =
    Http.getJson decodeUser "https://api.example.com/users/1"
    |> Flow.retry (Retry.schedule { Retry.defaults with Retries = 5; When = notRateLimited })
```

A `429` is transient, but this policy leaves it to the caller, so it is not retried:

```fsharp
let limited = ScriptedHttp([ 429, "" ])

match patientUser |> Flow.run limited.Env with
| Exit.Failure(Cause.Fail(HttpError.Status response)) -> response.StatusCode |> shouldEqual 429
| other -> failwithf "expected a 429, got %A" other

limited.Sent.Length |> shouldEqual 1
```

The same retry as a `Schedule` pipeline adds what a record cannot express, such as jitter:

```fsharp
let jitteredUser : Flow<TestEnv, HttpError, User> =
    Http.getJson decodeUser "https://api.example.com/users/1"
    |> Flow.retry (
        Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
        |> Schedule.jitteredWith (System.Random().NextDouble)
        |> Schedule.recursAtMost 5
        |> Schedule.whileInput notRateLimited)
```

A schedule without `whileInput` retries every typed error; select the transient ones so permanent failures stay
fast.

## Limit Response Size

A server you do not control can send a body of any size. `Request.maxResponseBytes` caps it:

```fsharp
let page (url: string) : Flow<TestEnv, HttpError, string> =
    Http.get url
    |> Request.maxResponseBytes (2L * 1024L * 1024L)
    |> Http.text
```

The live service fails with `HttpError.ResponseTooLarge` before reading when `Content-Length` declares a larger body,
and stops reading a streamed body once it passes the limit, so the client never buffers more than the limit.
`Http.send` applies the same check to responses from any `IHttp`, including test doubles. `Request.tryMaxResponseBytes` reads a
request's limit back, for example in a test double that enforces it.

## Expected Statuses Are Part Of The Request

Reliability starts with saying what success means. The expectation travels with the request, so callers cannot
forget to check:

```fsharp
let deleteUser (userId: int) : Flow<TestEnv, HttpError, HttpResponse> =
    DELETE $"https://api.example.com/users/{userId}"
    |> expect [ 204; 404 ]   // idempotent delete: already-gone is fine
    |> fetch
```

```fsharp
deleteUser 7 |> Flow.run (ScriptedHttp([ 404, "" ]).Env) |> Exit.map _.StatusCode |> shouldEqual (Exit.Success 404)
```

`expectAny` disables interpretation for endpoints where every status is meaningful, and `Http.sendResult` does the
same for one call without changing the request.

## When Not To Retry

Do not wrap non-idempotent POSTs in `retryTransient` unless the server deduplicates requests (for example with an
idempotency key header): a timeout does not prove the server ignored the request. Send the key explicitly, then
retry safely:

```fsharp
type Payment = { Amount: decimal }

let encodePayment (payment: Payment) = $"{{\"amount\":{payment.Amount}}}"
let decodeReceipt (json: string) : Result<string, string> = Ok json

let pay (payment: Payment) : Flow<TestEnv, HttpError, string> =
    POST $"https://api.example.com/payments"
    |> header "Idempotency-Key" (Guid.NewGuid().ToString())
    |> jsonBodyOf encodePayment payment
    |> fetchJson decodeReceipt
    |> withRetries 3
```

The key is created once when the request is built, so the retry resends the same key and the server can recognise
the repeat:

```fsharp
let payments = ScriptedHttp([ 503, ""; 200, "receipt-1" ])
pay { Amount = 9.5m } |> Flow.run payments.Env |> shouldEqual (Exit.Success "receipt-1")

payments.Sent
|> List.map (fun request -> Request.plan request |> _.Headers |> List.find (fst >> (=) "Idempotency-Key") |> snd)
|> List.distinct
|> List.length
|> shouldEqual 1
```
