---
title: Parse, don't validate
linkTitle: Parse
description: Declare the model once. Parsing, validation, redisplay, rules, and docs fall out.
menu:
  main:
    weight: 5
---

<div class="docs-home-container axial-landing">

<div class="docs-home-hero">

<div class="docs-home-copy">
<span class="eyebrow" style="color:#0b55d9">Axial &middot; Parse-don't-validate</span>

<h1>Parse, don't validate.</h1>

<div class="lede">
Declare the model once. Parsing, validation, redisplay, contextual rules, and metadata all fall out of one schema
&mdash; and an invalid model is <em>never constructed</em>.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" href="{{< relref "/docs/schema/tutorials/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/docs/schema/" >}}">Schema guides</a>
<a class="docs-chip" href="{{< relref "/docs/patterns/examples/" >}}">Examples</a>
</div>
</div>

<div class="axial-hero-panels">

<div class="axial-panel">
<span class="label">One declaration</span>

```fsharp
let signupSchema =
    Schema.recordFor<Signup, _> (fun email age ->
        { Email = email; Age = age })
    |> Schema.fieldWith
        [ SchemaConstraint.required; SchemaConstraint.email ]
        "email" _.Email Value.text
    |> Schema.fieldWith
        [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.int
    |> Schema.build
```

</div>

<div class="axial-panel">
<span class="label">Path-aware diagnostics &middot; raw input kept</span>
<div class="axial-diag">order
├─ customer
│  └─ email        <span class="diag-err">invalid format</span>
├─ address
│  └─ postcode     <span class="diag-err">missing</span>
└─ items[1]
   └─ quantity     <span class="diag-err">must be greater than 0</span></div>
</div>

</div>

</div>

<ul class="axial-checks">
<li>Errors keep their paths</li>
<li>Raw input kept for redisplay</li>
<li>Same schema drives JSON Schema, docs, and UI metadata</li>
<li>Zero reflection &mdash; AOT, trimming, and Fable safe</li>
<li>Independent of Flow</li>
</ul>

<section>
<span class="label">The two-lane rule</span>

<div class="axial-lanes">

<div class="axial-lane">
<h3>Modelling a domain? Declare a schema.</h3>
<p>Parsing raw input, re-validating existing values, workflow rules, and metadata interpreters all read the same
declaration. Constraints hold by construction, so downstream code never re-checks.</p>

```fsharp
let parsed = Input.parse signupSchema raw

match parsed.Result with
| Ok signup -> register signup      // trusted
| Error _ -> renderForm parsed      // paths + raw input
```

</div>

<div class="axial-lane">
<h3>Simple code? Plain <code>Result</code> is the whole story.</h3>
<p>No schema, no framework types: standard F# <code>Result</code> with your own error union is idiomatic Axial, not a
compromise.</p>

```fsharp
type LoginError = UserNotFound | InvalidPassword

let validatePassword password =
    password
    |> Result.notBlank
    |> Result.mapError (fun () -> InvalidPassword)
```

</div>

</div>
</section>

<section class="axial-machinery">
<span class="label">Under the hood</span>
<p>
Schemas and results are the two doors. The machinery behind them stays available when you need it directly:
reusable <a href="{{< relref "/docs/error-handling/checks/" >}}">Check</a> constraints, accumulating
<a href="{{< relref "/docs/validation/" >}}">Validation</a> diagnostics, and
<a href="{{< relref "/docs/refined/" >}}">Refined</a> parsing for single values.
</p>
</section>

<div class="axial-strip axial-strip--flow">
<p>
Need this inside a workflow with dependencies, async work, or cancellation? <code>Flow.verify</code> runs any parsing
or rule policy with the environment injected &mdash; and short-circuits on failure.
</p>
<a href="{{< relref "/flow/" >}}">Axial.Flow &rarr;</a>
</div>

</div>
