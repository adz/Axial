---
title: Requests
description: Build immutable HTTP requests with encoded URLs, redacted secrets, and typed bodies.
---

# Requests

This page shows how immutable request values replace string concatenation, manual escaping, and leaked credentials.

## Interpolated URLs Encode Every Hole

String-built URLs break on spaces, slashes, and user input. The DSL builders treat every interpolation hole as one
URL-encoded value:

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

```fsharp transcript
> open Axial.HttpClient;;
> open Axial.HttpClient.DSL;;
> let name = "a b/c&d" in GET $"https://api.example.com/users/{name}" |> Request.render;;
val it: string = "GET https://api.example.com/users/a%20b%2Fc%26d"
```

`HEAD`, `POST`, `PUT`, `PATCH`, and `DELETE` work the same way. A hole cannot smuggle in extra path segments or
query parameters; only the literal text of the template controls URL structure.

When a URL is already a complete string with no inserted values, use the plain builders:

```fsharp transcript
> Axial.HttpClient.Http.get "https://api.example.com/users" |> Axial.HttpClient.Request.render;;
val it: string = "GET https://api.example.com/users"
```

## Query Parameters

`query` appends one URL-encoded name-value pair. Values are formatted with the invariant culture, so numbers and
dates are safe to pass directly:

```fsharp transcript
> open Axial.HttpClient;;
> open Axial.HttpClient.DSL;;
> GET $"https://api.example.com/search" |> query "q" "f# & http" |> query "page" 2 |> Request.render;;
val it: string = "GET https://api.example.com/search?q=f%23%20%26%20http&page=2"
```

## Secrets Never Reach Diagnostics

API keys and tokens must not appear in logs, error messages, or plans. Three tools keep them out:

```fsharp transcript
> open Axial.HttpClient;;
> open Axial.HttpClient.DSL;;
> let apiKey = "s3cret" in GET $"https://api.example.com/lookup?key={secret apiKey}" |> Request.render;;
val it: string = "GET https://api.example.com/lookup?key=***"

> Http.get "https://api.example.com/lookup" |> Request.secretQuery "api_key" "s3cret" |> Request.render;;
val it: string = "GET https://api.example.com/lookup?api_key=***"

> (Http.get "https://api.example.com/me" |> bearer "token" |> Request.plan).Headers;;
val it: Tuple<string,string> list = [("Authorization", "***")]

> (Http.get "https://api.example.com/me" |> basicAuth "ada" "password" |> Request.plan).Headers;;
val it: Tuple<string,string> list = [("Authorization", "***")]
```

A secret interpolation hole renders as `***` in every transcript. A secret query parameter is sent for real and
rendered as `key=***`. `bearer` and `basicAuth` are always redacted, with no opt-in needed.

`Request.render` produces the redacted request line (for example `GET https://api.example.com/lookup?key=***`)
that appears inside `HttpError` values, so error logging is safe by default.

## Headers

```fsharp transcript
> open Axial.HttpClient;;
> open Axial.HttpClient.DSL;;
> (Http.get "https://api.example.com/users" |> header "Accept" "application/json" |> Request.userAgent "my-app/1.0" |> Request.secretHeader "X-Api-Key" "s3cret" |> Request.plan).Headers |> List.map snd;;
val it: string list = ["application/json"; "my-app/1.0"; "***"]
```

`Request.acceptJson` is shorthand for the JSON accept header; `fetchJson` and `Http.getJson` add it for you.

## Bodies

Bodies carry their content type with them:

Each body helper sets the content type. `Request.plan` shows it with the body's size:

```fsharp transcript
> open Axial.HttpClient;;
> open Axial.HttpClient.DSL;;
> (POST $"https://api.example.com/users" |> jsonBody """{"name":"Ada"}""" |> Request.plan).Body;;
val it: string = "application/json (14 characters)"

> let encodeName (name: string) = $"{{\"name\":\"{name}\"}}";;
> (POST $"https://api.example.com/users" |> jsonBodyOf encodeName "Ada" |> Request.plan).Body;;
val it: string = "application/json (14 characters)"

> (POST $"https://api.example.com/notes" |> textBody "hello" |> Request.plan).Body;;
val it: string = "text/plain (5 characters)"

> (POST $"https://api.example.com/search" |> formBody [ "q", "axial"; "page", "2" ] |> Request.plan).Body;;
val it: string = "form (2 fields)"

> (POST $"https://api.example.com/blobs" |> Request.bytesBody "application/octet-stream" [| 1uy; 2uy |] |> Request.plan).Body;;
val it: string = "application/octet-stream (2 bytes)"
```

`jsonBodyOf` takes any serializer, such as a Reified or Thoth codec's encode function.

`jsonBodyOf` takes any `'value -> string` function, so it works with `Reified.Schema.Json`, hand-written serializers, or
any other JSON library without coupling this package to one.

## Plans Show What Would Be Sent

`Request.plan` returns a redacted, serializable description without performing any I/O, for logging,
dry runs, and approval flows:

```fsharp run
Http.post "https://api.example.com/users"
|> Request.bearer "token"
|> Request.jsonBody """{"name":"Ada"}"""
|> Request.timeout (TimeSpan.FromSeconds 5.0)
|> Request.plan
|> shouldEqual
    { Method = "POST"
      Url = "https://api.example.com/users"
      Headers = [ "Authorization", "***" ]
      Body = "application/json (14 characters)"
      Timeout = Some(TimeSpan.FromSeconds 5.0)
      MaxResponseBytes = None
      Expectation = "2xx" }
```

## When Not To Use The DSL

Open `Axial.HttpClient.DSL` locally in modules that make HTTP calls, not at the top of every file: it introduces
short names such as `query`, `header`, and `timeout`. In code that only forwards a request built elsewhere, the
qualified `Request.*` functions keep the origin obvious.
