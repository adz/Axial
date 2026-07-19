---
weight: 20
title: Responses And Errors
description: Read complete response transcripts, decode JSON into typed values, and match one typed error.
type: docs
---


This page shows how one response transcript and one error type replace scattered status checks and exception
handling.

## The Response Transcript

Every exchange produces a complete `HttpResponse`:

```fsharp
let workflow =
    flow {
        let! response = Http.get "https://api.example.com/users" |> Http.send
        let etag = response |> Response.tryHeader "ETag"   // case-insensitive
        return response.StatusCode, response.Text, response.Duration, etag
    }
```

- `StatusCode`, `ReasonPhrase`, and `Headers` (response plus content headers, in arrival order).
- `Body` is the exact bytes; `Text` is decoded with the response charset, defaulting to UTF-8.
- `Request` is the redacted request line, so the transcript is safe to log as-is.
- `StartedAt` and `Duration` time the full exchange including body download.

## Typed JSON Decoding

`Response.json` and the `fetchJson`/`Http.getJson` terminals take a decoder of type
`string -> Result<'value, string>`. Any JSON library fits that shape:

```fsharp
open Axial.Flow.HttpClient.DSL

let user : Flow<#IHas<IHttp>, HttpError, User> =
    GET $"https://api.example.com/users/{userId}"
    |> fetchJson (Json.deserializeResult userCodec)   // Axial.Schema.Codec, Thoth, or hand-written
```

A decoder failure becomes `HttpError.DecodeFailed(message, response)` — the full transcript rides along, so the
error handler can log the offending payload without re-fetching it.

To POST a value and decode the reply in one step:

```fsharp
let created =
    Http.postJson (Json.serialize userCodec) (Json.deserializeResult userCodec)
        "https://api.example.com/users" user
```

## One Error Type

Every way an HTTP call can fail is one case of `HttpError`:

```fsharp
match error with
| HttpError.InvalidRequest message -> ...            // malformed URL or request construction
| HttpError.ConnectionFailed(request, message) -> ...// DNS, refused, dropped connection
| HttpError.TimedOut(request, timeout) -> ...        // per-request timeout elapsed
| HttpError.Canceled message -> ...                  // the workflow was interrupted
| HttpError.Status response -> ...                   // status outside the expectation, full transcript
| HttpError.DecodeFailed(message, response) -> ...   // body did not decode, full transcript
```

`HttpError.describe` formats any case with its redacted request context and a bounded body preview, so a single
`Flow.mapError HttpError.describe` produces loggable messages. `HttpError.tryResponse` extracts the transcript
from the cases that carry one.

## Statuses Are Data, Not Exceptions

`Http.send` fails with `HttpError.Status` for anything outside the request's expectation (2xx by default).
When a "failure" status is a normal outcome, widen the expectation and branch on the code:

```fsharp
let findUser userId =
    flow {
        let! response =
            GET $"https://api.example.com/users/{userId}"
            |> expect [ 200; 404 ]
            |> fetch
        if response.StatusCode = 404 then return None
        else return! response |> Response.json decodeUser |> Result.map Some
    }
```

`Http.sendResult` skips status interpretation entirely and returns whatever arrived; use it when a proxy or
health check needs the raw exchange.

## When Not To Decode

`fetchText` and `fetchBytes` return the body directly for HTML scraping, file downloads, and pass-through
proxying. Reach for `fetchJson` only when the payload should become a typed value; decoding a body you will
immediately re-serialize wastes the transcript you already have.
