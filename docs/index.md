---
title: Home
description: FsFlow technical guides, semantics, and API reference.
---

<div class="docs-home-container">

<div class="docs-home-hero">

<div class="docs-home-copy">

<span class="eyebrow">One model from predicate checks to task execution</span>

# A single model for Result-based programs in F#.

<div class="lede">
Write predicate checks once. Keep fail-fast logic in <code>Result</code>, accumulate sibling failures with <code>Validation</code>, then lift the same logic into <code>Flow</code>, <code>AsyncFlow</code>, or <code>TaskFlow</code> when the boundary needs environment access, async work, task interop, cancellation, or runtime policy.
</div>

<div class="docs-home-meta">
<a class="docs-chip" href="./docs/start/validate-and-result/">Check -> Result -> Validation</a>
<a class="docs-chip" href="./docs/start/getting-started/">Flow / AsyncFlow / TaskFlow</a>
<a class="docs-chip" href="./docs/core-model/why-fsflow/">Typed failure</a>
<a class="docs-chip" href="./docs/core-model/env-slicing/">Explicit environment</a>
<a class="docs-chip" href="./docs/core-model/task-async-interop/">Runtime context</a>
<a class="docs-chip" href="./docs/core-model/semantics/">Cold execution semantics</a>
</div>

</div>

<div class="docs-home-visual">
<a class="docs-home-visual-link" href="./docs/start/getting-started/">
<img src="content/img/flow-graphic.png" alt="FsFlow Model" />
</a>
<div class="docs-home-visual-cta">
<a class="docs-home-cta" href="./docs/start/getting-started/">Get Started &gt;</a>
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
    taskFlow {
        let! user = Env<Api> (_.LoadUser userId)
        let! checkedAt = Env<Clock> _.UtcNow
        let! email = validateEmail user.Email

        return email, checkedAt
    }
```

<div class="docs-home-cta-row">
<a class="docs-home-cta" href="./docs/patterns/examples/">Examples &gt;</a>
</div>
</section>

</div>
