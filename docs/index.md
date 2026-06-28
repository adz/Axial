---
title: Home
description: Axial technical guides, semantics, and API reference.
---

<div class="docs-home-container">

<div class="docs-home-hero">

<div class="docs-home-copy"><span class="eyebrow">
A coherent application architecture model for F# on .NET.
</span>

<h1>
Structured composition over normal F#/.NET code
</h1>

<div class="lede">
Axial is a model for Result-based programs. Write predicate checks with <code>Check</code>, keep fail-fast logic in <code>Result</code>, compile untrusted values with <a href="{{< relref "/reference/refined/" >}}"><code>Refined</code></a>, accumulate sibling failures with <a href="{{< relref "/reference/validation/" >}}"><code>Validation</code></a>, then lift boundary policy into <a href="{{< relref "/reference/flow/" >}}"><code>Flow</code></a> when the boundary needs environment access, async work, task interop, cancellation, or runtime policy.
</div>

<div class="docs-home-meta">
<a class="docs-chip" href="{{< relref "/docs/start/getting-started.md" >}}">Get Started</a>
<a class="docs-chip" href="{{< relref "/docs/flow/tutorials/" >}}">Flow Tutorials</a>
<a class="docs-chip" href="{{< relref "/docs/error-handling/" >}}">Error Handling</a>
<a class="docs-chip" href="{{< relref "/docs/refined/" >}}">Refined</a>
<a class="docs-chip" href="{{< relref "/docs/validation/" >}}">Validation</a>
<a class="docs-chip" href="{{< relref "/docs/flow/" >}}">Flow</a>
<a class="docs-chip" href="{{< relref "/docs/flow/dependencies.md" >}}">Managing dependencies</a>
<a class="docs-chip" href="{{< relref "/docs/flow/semantics.md" >}}">Cold execution semantics</a>
</div>

</div>

<div class="docs-home-visual">
<a class="docs-home-visual-link" href="{{< relref "/docs/start/getting-started.md" >}}">
<img src="content/img/flow-graphic.png" alt="Axial Model" />
</a>
<div class="docs-home-visual-cta">
<a class="docs-home-cta" href="{{< relref "/docs/start/getting-started.md" >}}">Get Started &gt;</a>
</div>
</div>
 
</div>

<section class="docs-home-example">
<span class="label">Check once, lift later</span>

```fsharp
type RegistrationError =
    | EmailMissing
    | UserNotFound

let validateEmail (email: string) : Result<string, RegistrationError> =
    email
    |> Result.notBlank EmailMissing

type User =
    { Email: string }

type Api =
    { LoadUser: int -> Task<Result<User, RegistrationError>> }

let readVerifiedEmail userId =
    flow {
        let! loadUser = Flow.read _.LoadUser
        let! user = loadUser userId
        return! validateEmail user.Email |> Flow.fromResult
    }
```

<div class="docs-home-cta-row">
<a class="docs-home-cta" href="{{< relref "/docs/patterns/examples/" >}}">Examples &gt;</a>
</div>
</section>

</div>
