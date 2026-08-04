---
weight: 5
title: How it compares
description: Axial.Constraint against DataAnnotations, FluentValidation, and Validus — including where they are the better choice.
type: docs
---


The short version: most validation libraries treat a rule and its message as two artefacts. Axial treats
the message as a projection of the rule, which is why one declaration can also produce JSON Schema, test
data, and translations.

That difference matters in some projects and not others. This page tries to say which is which.

## The one structural difference

| | Where the rule lives | Where the message lives |
| --- | --- | --- |
| DataAnnotations | an attribute on a property | `ErrorMessage`, or a resource key you name |
| FluentValidation | a rule in a validator class | a message override on that rule, or a localizer you wire up |
| Validus | a validator function | a message function supplied alongside it |
| **Axial.Constraint** | a `Constraint<'value>` value | **derived from the constraint** |

Everything below follows from that row.

When the message is written separately, nothing makes the two move together. Widening `MaxLength(40)` to
`80` leaves `"must be 40 characters or fewer"` behind until someone notices. Adding a language means
enumerating every message a second time.

When the message is derived, a `Violation` carries the failing atom and the actual value rather than any
prose, and text is produced at the edge:

```fsharp
42
|> Constraint.check (Constraint.between 0 10)
|> Result.mapError Violation.render
// Error "expected a value between 0 and 10, but was 42"
```

Change the bounds and the sentence changes. Point a `Renderer` at another culture and the same violation
renders in that language. Neither required a message to be maintained anywhere.

## The second difference: a constraint is data

Because a constraint is an inspectable value rather than a lambda, other machinery can read it:

```fsharp
let retryCount : Constraint<int> = Constraint.between 0 10

Schema.int |> Schema.constrain retryCount          // used in a schema
Refinement.define retryCount RetryCount _.Value    // used in a refined type
```

The same declaration lowers to JSON Schema and generates test data. A predicate — in any library, including
Axial's own `Constraint.custom` escape hatch — cannot be inspected, and Axial says so rather than
pretending otherwise.

If you only ever need to answer "is this valid?", this buys you nothing and a predicate is simpler.

## DataAnnotations

**Choose DataAnnotations when** you are validating an ASP.NET MVC or Blazor model, want zero setup, and are
content with what attributes can express. Model binding, client-side validation, and the surrounding
tooling all work out of the box. That integration is real and Axial does not replace it.

**Its limits** are structural rather than incidental: attributes are declarations on properties, so a rule
cannot be named, passed around, composed, or reused between two types that share a concept. Conditional and
cross-field rules mean dropping to `IValidatableObject`. Messages are per-attribute strings or resource
keys.

## FluentValidation

**Choose FluentValidation when** you want a mature, widely-known library with a large community, ASP.NET
integration, async and DI-driven validators, and a team already fluent in it. It is a good library and the
ecosystem around it is far larger than Axial's.

**Where it differs:** a rule belongs to a validator for a type, rather than being a free-standing value.
Sharing "what a valid retry count is" between two types means sharing a validator or repeating the rule.
Messages are attached per rule and localization is wired up separately, so both remain artefacts to
maintain. And a validator is ultimately a set of delegates, so nothing downstream can read it to produce a
JSON Schema or generate matching test data.

## Validus

**Choose Validus when** you want idiomatic F# validation with a small surface and few concepts. Validators
compose, results accumulate, and there is very little to learn. For many F# applications that is the right
amount of machinery.

**Where it differs:** validators are functions, so the message is supplied alongside the rule rather than
derived from it, and nothing can inspect a validator afterwards. Axial's extra concepts — `Violation`,
`Renderer`, interpreted versus opaque rules — exist to buy inspectability and derived messages. If you do
not want those, they are cost without return.

## What Axial costs you

Stated plainly, because the sections above are about what it gives:

- **More concepts.** `Constraint`, `Violation`, and `Renderer` are three things to learn where a predicate
  and a string are two.
- **F# only.** There is no C# story.
- **A young ecosystem.** Fewer answers, fewer integrations, and a smaller community than FluentValidation
  or DataAnnotations.
- **No model-binding integration by default.** Axial validates values and models you hand it; wiring that
  into an ASP.NET pipeline is [`Axial.Schema.Http`]({{% relref "/schema/http-servers/" %}})'s job, not an
  attribute you add to an existing controller.

## When Axial is worth it

The benefit compounds where the same value rule is needed in more than one place — a domain type's
invariant, a request schema, a published JSON Schema, a generated fixture, a form message in two languages.
If that describes your project, one declaration replaces four or five parallel ones.

If your validation is a handful of checks in one place and never leaves it, a predicate returning
`Result<_, string>` is a perfectly good answer and you should use it.

## Related comparisons

- [`Axial.Result` against FsToolkit.ErrorHandling]({{% relref "/result/fstoolkit-comparison/" %}}) — for
  composing failures rather than describing valid values.
- Decoding serialized input is a different job from validating a typed value: see
  [`Axial.Parse`](../parse/) and [Schema]({{% relref "/schema/" %}}) rather than this page.
