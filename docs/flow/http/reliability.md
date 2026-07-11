---
weight: 30
title: Reliability
description: Enforce per-request timeouts and retry only the failures that can actually recover.
---

# Reliability

This page shows how typed errors turn timeout and retry policy into ordinary, testable code.

## Per-Request Timeouts

`HttpClient.Timeout` is one global setting that throws `TaskCanceledException`, indistinguishable from real
cancellation. An Axial timeout is per request and produces a dedicated typed error:

```fsharp
GET $"https://api.example.com/slow-report"
|> timeout (TimeSpan.FromSeconds 5.0)
|> fetch
// Fails with HttpError.TimedOut(request, 5s) — never confused with HttpError.Canceled.
```

The live service enforces the timeout with a linked cancellation source, so the connection is torn down when the
deadline passes, not merely abandoned.

## Retry Only Transient Failures

Retrying a 404 or a decode failure wastes time and can duplicate side effects. `HttpError.isTransient`
classifies exactly the failures where a retry can help: connection failures, timeouts, and 408/429/5xx statuses.

```fsharp
let users =
    Http.getJson decodeUsers "https://api.example.com/users"
    |> Http.retryTransient 4 (TimeSpan.FromMilliseconds 200.0)
```

`retryTransient` uses exponential backoff (200ms, 400ms, 800ms, ...) and gives up after the attempt budget.
A permanent failure such as `HttpError.Status 404` or `HttpError.DecodeFailed` fails immediately on the first
attempt. The DSL shorthand `withRetries 4` applies the same policy with a 200ms base delay.

For full control, build the policy yourself and use the general Flow retry machinery:

```fsharp
let policy =
    { HttpError.transientPolicy 6 (TimeSpan.FromMilliseconds 100.0) with
        ShouldRetry = fun error ->
            HttpError.isTransient error
            && (match error with HttpError.Status r -> r.StatusCode <> 429 | _ -> true) }

workflow |> Flow.Runtime.retry policy
```

`Schedule.retry` from `Axial.Flow` also composes with HTTP workflows when you need jitter or custom cadence:

```fsharp
workflow |> Schedule.retry (Schedule.exponential (TimeSpan.FromMilliseconds 100.0) |> Schedule.jittered)
```

Note that `Schedule.retry` retries every typed error; prefer `retryTransient` or an explicit `RetryPolicy` so
permanent failures stay fast.

## Expected Statuses Are Part Of The Request

Reliability starts with saying what success means. The expectation travels with the request, so callers cannot
forget to check:

```fsharp
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

```fsharp
POST $"https://api.example.com/payments"
|> header "Idempotency-Key" (Guid.NewGuid().ToString())
|> jsonBodyOf encodePayment payment
|> fetchJson decodeReceipt
|> withRetries 4
```
