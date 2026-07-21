---
weight: 10
title: Requests
description: Build immutable HTTP requests with encoded URLs, redacted secrets, and typed bodies.
type: docs
---


This page shows how immutable request values replace string concatenation, manual escaping, and leaked credentials.

## Interpolated URLs Encode Every Hole

String-built URLs break on spaces, slashes, and user input. The DSL builders treat every interpolation hole as one
URL-encoded value:

```fsharp
open Axial.Flow.HttpClient
open Axial.Flow.HttpClient.DSL

let name = "a b/c&d"
let request = GET $"https://api.example.com/users/{name}"
// Sends: https://api.example.com/users/a%20b%2Fc%26d
```

`HEAD`, `POST`, `PUT`, `PATCH`, and `DELETE` work the same way. A hole cannot smuggle in extra path segments or
query parameters; only the literal text of the template controls URL structure.

When a URL is already a complete string with no inserted values, use the plain builders:

```fsharp
let request = Http.get "https://api.example.com/users"
```

## Query Parameters

`query` appends one URL-encoded name-value pair. Values are formatted with the invariant culture, so numbers and
dates are safe to pass directly:

```fsharp
GET $"https://api.example.com/search"
|> query "q" "f# & http"     // q=f%23%20%26%20http
|> query "page" 2
```

## Secrets Never Reach Diagnostics

API keys and tokens must not appear in logs, error messages, or plans. Three tools keep them out:

```fsharp
// A secret interpolation hole renders as *** in every transcript.
GET $"https://api.example.com/lookup?key={secret apiKey}"

// A secret query parameter: sent for real, rendered as key=***.
Http.get "https://api.example.com/lookup" |> Request.secretQuery "api_key" apiKey

// bearer and basicAuth are always redacted; no opt-in needed.
request |> bearer token
request |> basicAuth user password
```

`Request.render` produces the redacted request line (for example `GET https://api.example.com/lookup?key=***`)
that appears inside `HttpError` values, so error logging is safe by default.

## Headers

```fsharp
request
|> header "Accept" "application/json"
|> Request.userAgent "my-app/1.0"
|> Request.secretHeader "X-Api-Key" apiKey   // value redacted in plans
```

`Request.acceptJson` is shorthand for the JSON accept header; `fetchJson` and `Http.getJson` add it for you.

## Bodies

Bodies carry their content type with them:

```fsharp
POST $"https://api.example.com/users"
|> jsonBody """{"name":"Ada"}"""                  // application/json

POST $"https://api.example.com/users"
|> jsonBodyOf (Json.serialize userCodec) user     // encode a value with any serializer

request |> textBody "hello"                       // text/plain
request |> formBody [ "q", "axial"; "page", "2" ] // application/x-www-form-urlencoded
request |> Request.bytesBody "application/octet-stream" payload
```

`jsonBodyOf` takes any `'value -> string` function, so it works with `Axial.Schema.Json`, hand-written serializers, or
any other JSON library without coupling this package to one.

## Plans Show What Would Be Sent

`Request.plan` returns a redacted, serializable description without performing any I/O — useful for logging,
dry runs, and approval flows:

```fsharp
let plan =
    Http.post "https://api.example.com/users"
    |> Request.bearer token
    |> Request.jsonBody """{"name":"Ada"}"""
    |> Request.timeout (TimeSpan.FromSeconds 5.0)
    |> Request.plan
// { Method = "POST"; Url = "https://api.example.com/users"
//   Headers = [ "Authorization", "***" ]
//   Body = "application/json (14 characters)"
//   Timeout = Some 00:00:05; Expectation = "2xx" }
```

## When Not To Use The DSL

Open `Axial.Flow.HttpClient.DSL` locally in modules that make HTTP calls, not at the top of every file: it introduces
short names such as `query`, `header`, and `timeout`. In code that only forwards a request built elsewhere, the
qualified `Request.*` functions keep the origin obvious.
