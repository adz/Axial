---
title: Axial
weight: 0
---

<img src="content/img/axial-inline-light.svg" alt="Axial" width="220" />

# Typed asynchronous workflows for F#

Axial makes a workflow's required services and expected failures visible in its type. The same workflow model
carries cancellation, resource scopes, structured concurrency, retries, streams, hosting, and telemetry on .NET
and Fable JavaScript.

<p><a class="btn btn-primary btn-lg" href="getting-started/index.html">Get started</a></p>

## Start with the problem you have

| If this is familiar | Read |
| --- | --- |
| Code cannot be tested without a real database or HTTP call | [Dependencies](dependencies/index.html) |
| A function's expected failures are hidden behind exceptions | [Error handling](error-handling/index.html) |
| Retry and timeout logic is repeated at each call site | [Scheduling and retries](scheduling-and-retries/index.html) |
| Tracing or metrics require plumbing through every function | [Observability](observability/index.html) |
| The same logic must run on the server and in the browser | [Platforms and hosting](platforms-and-hosting/index.html) |
| You need a mockable HTTP client | [HTTP](http/index.html) |

## One workflow model

`Flow<'env, 'error, 'value>` describes the services a workflow reads, the expected errors it may return, and its
successful value. Application code supplies the environment at the boundary; tests supply a smaller value with the
same shape.

```fsharp
type RegistrationEnv =
    { LoadUser: int -> Task<Result<User, RegistrationError>>
      SaveUser: User -> Task<Result<unit, RegistrationError>> }

let register userId : Flow<RegistrationEnv, RegistrationError, unit> =
    flow {
        let! loadUser = Flow.read _.LoadUser
        let! saveUser = Flow.read _.SaveUser
        let! user = loadUser userId
        return! saveUser user
    }
```

The [getting-started guide](getting-started/index.html) builds and runs this shape before introducing Axial's wider
vocabulary.
