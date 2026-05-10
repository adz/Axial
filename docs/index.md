---
title: Home
description: FsFlow technical guides, semantics, and API reference.
---

<div class="docs-home-container">

<div class="docs-home-hero">

<div class="docs-home-copy"><span class="eyebrow">One model from predicate checks to task execution</span>

<h1>A single model for Result-based programs in F#.</h1>

<div class="lede">
Write predicate checks once. Keep fail-fast logic in <code>Result</code>, accumulate multiple failures with <a href="{{< relref "/reference/validation/" >}}"><code>Validation</code></a>, then lift the same logic into <a href="{{< relref "/reference/flow/" >}}"><code>Flow</code></a> when the boundary needs environment access, async work, task interop, cancellation, or runtime policy.
</div>

<div class="docs-home-meta">
<a class="docs-chip" href="{{< relref "/docs/validation-results/" >}}">Pure Checks -> Result & Validation</a>
<a class="docs-chip" href="{{< relref "/docs/start/getting-started.md" >}}">Flow</a>
<a class="docs-chip" href="{{< relref "/docs/core-model/why-fsflow.md" >}}">Typed failure</a>
<a class="docs-chip" href="{{< relref "/docs/core-model/env-slicing.md" >}}">Explicit environment</a>
<a class="docs-chip" href="{{< relref "/docs/core-model/task-async-interop.md" >}}">Runtime context</a>
<a class="docs-chip" href="{{< relref "/docs/core-model/semantics.md" >}}">Cold execution semantics</a>
</div>

</div>

<div class="docs-home-visual">
<a class="docs-home-visual-link" href="{{< relref "/docs/start/getting-started.md" >}}">
<img src="content/img/flow-graphic.png" alt="FsFlow Model" />
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
    |> Check.notBlank
    |> Check.orError EmailMissing

type User =
    { Email: string }

type Api =
    { LoadUser: int -> Task<Result<User, RegistrationError>> }

type Clock =
    { UtcNow: DateTimeOffset }

let readVerifiedEmail userId =
    flow {
        let! user = Env<Api> (_.LoadUser userId)
        let! checkedAt = Env<Clock> _.UtcNow
        let! email = validateEmail user.Email

        return email, checkedAt
    }
```

<div class="docs-home-cta-row">
<a class="docs-home-cta" href="{{< relref "/docs/patterns/examples/" >}}">Examples &gt;</a>
</div>
</section>

</div>
