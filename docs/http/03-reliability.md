---
title: Reliability
description: Enforce per-request timeouts and retry only the failures that can actually recover.
---

# Reliability

This page shows how typed errors turn timeout and retry policy into ordinary, testable code.

## Per-Request Timeouts

`HttpClient.Timeout` is one global setting that throws `TaskCanceledException`, indistinguishable from real
cancellation. An Axial timeout is per request and produces a dedicated typed error:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
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

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let users =
    Http.getJson decodeUsers "https://api.example.com/users"
    |> Http.retryTransient 3 (TimeSpan.FromMilliseconds 200.0)
```

`retryTransient 3` makes up to 3 retries after the first attempt, with exponential backoff (200ms, 400ms, 800ms).
A permanent failure such as `HttpError.Status 404` or `HttpError.DecodeFailed` fails immediately on the first
attempt. The DSL shorthand `withRetries 3` applies the same schedule with a 200ms base delay. To combine it with other
schedules, take the schedule itself: `HttpError.transientRetry 3 (TimeSpan.FromMilliseconds 200.0)` is the
`Schedule` that `retryTransient` passes to `Flow.retry`.

For full control, describe the retry yourself and pass it to `Flow.retry`. A `Retry` record names each choice:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let notRateLimited error =
    HttpError.isTransient error
    && (match error with HttpError.Status r -> r.StatusCode <> 429 | _ -> true)

workflow
|> Flow.retry (Retry.schedule { Retry.defaults with Retries = 5; When = notRateLimited })
```

The same retry as a `Schedule` pipeline adds what a record cannot express, such as jitter:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
workflow
|> Flow.retry (
    Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    |> Schedule.jitteredWith random.NextDouble
    |> Schedule.recursAtMost 5
    |> Schedule.whileInput notRateLimited)
```

A schedule without `whileInput` retries every typed error; select the transient ones so permanent failures stay
fast.

## Limit Response Size

A server you do not control can send a body of any size. `Request.maxResponseBytes` caps it:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let page =
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

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
DELETE $"https://api.example.com/users/{userId}"
|> expect [ 204; 404 ]   // idempotent delete: already-gone is fine
|> fetch
```

`expectAny` disables interpretation for endpoints where every status is meaningful, and `Http.sendResult` does the
same for one call without changing the request.

## When Not To Retry

Do not wrap non-idempotent POSTs in `retryTransient` unless the server deduplicates requests (for example with an
idempotency key header): a timeout does not prove the server ignored the request. Send the key explicitly, then
retry safely:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
POST $"https://api.example.com/payments"
|> header "Idempotency-Key" (Guid.NewGuid().ToString())
|> jsonBodyOf encodePayment payment
|> fetchJson decodeReceipt
|> withRetries 3
```
