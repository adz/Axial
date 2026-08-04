---
weight: 20
title: Constraint
type: docs
notoc: true
description: Declare a rule once and get the check, the failure, and its message from that one declaration.
---

# Constraint

Most validation stacks make you write every rule twice: once as the check, and once as the message that
explains it to a person. The two live in different places — an attribute and a resource key, a builder call
and a message override, a predicate and a string literal — and they drift. Someone widens a length limit
and the error still quotes the old one.

`Axial.Constraint` has no second place to write it. A `Constraint<'value>` is a reusable description of
valid values, and the failure it produces is derived from that same description. **check** is the operation
that runs it.

```fsharp
open Axial.Constraint

let retryCount : Constraint<int> =
    Constraint.between 0 10

3 |> Constraint.test retryCount   // true

42
|> Constraint.check retryCount
|> Result.mapError Violation.render
// Error "expected a value between 0 and 10, but was 42"

3 |> Constraint.guard retryCount  // Ok 3
```

Nobody wrote `"expected a value between 0 and 10, but was 42"`. Change the bounds and the message changes
with them, because it was never a separate artefact to keep in step.

## The failure carries facts, not prose

A `Violation` holds the failing constraint atom and, where portable, the actual value. It carries no
language and no formatting. That is what keeps it comparable data you can retain, assert on in a test, and
pass across a boundary without dragging a culture or a `ResourceManager` along with it.

```fsharp
match "ab" |> Constraint.check (Constraint.minLength 3: Constraint<string>) with
| Ok () -> ()
| Error violation ->
    Violation.tryExpectation violation
    // Some (CardinalityAtom (Cardinality.Minimum 3))

    Violation.tryActual violation
    // Some (ConstraintValue.Integer 2L)
```

Application code can classify a failure without parsing a sentence, and a test can assert on the fact
rather than on the wording.

## One language, or many, from the same violation

Prose happens at the rendering edge. `Violation.render` is the zero-dependency English default and needs no
setup at all. When you need more, a `Renderer` carries the language and the document context while the
violation carries the facts:

```fsharp
let field = renderer |> Renderer.context "signup" |> Renderer.attribute "name"

violation |> Violation.message field      // "must be present"
violation |> Violation.fullMessage field  // "Name must be present"
```

Give the renderer a different culture and the identical violation reads `"Le nom doit être renseigné"`,
with contextual fallback, and without any application code walking a violation tree or reproducing Axial's
key catalogue.

Translation is cheap here because it is not a feature bolted on afterwards — it is the same split that
removed the duplicated message in the first place. Shipping in one language still gets the benefit; you
simply never build the resources.

## The same declaration is read by everything downstream

There is one vocabulary, not several. The same value works unchanged in a refinement and in a schema:

```fsharp
let retryCountRefinement =
    Refinement.define retryCount RetryCount _.Value

let schema =
    Schema.int |> Schema.constrain retryCount
```

A constraint built from named parts lowers to JSON Schema, generates test data, and renders localizable
messages; the equivalent hand-written lambda does none of those things, and says so honestly rather than
pretending.

The catalogue resolves across text, collections, options, and maps by the type it is used at, so most uses
need nothing extra:

```fsharp
Schema.text |> Schema.constrain Constraint.present
```

A standalone binding is the exception: the annotation is the only type information there, so it is what
selects the shape.

```fsharp
let requiredName : Constraint<string> = Constraint.present
```

The same value facts appear at three further levels:

- [Refined values](../refined/domain-values/) use a constraint to construct invariant-carrying domain types.
- [Schema]({{% relref "/schema/refined-values/" %}}) adds structured input, paths, accumulation, and wire interpreters.
- JSON Schema publishes what the target can enforce, and documents the rest.

## Where to go next

Weighing this against DataAnnotations, FluentValidation, or Validus? Read
[How it compares](./comparison/).

Otherwise take the [Constraint DSL](./constraint-dsl/) for the vocabulary and how to write a rule module,
then [Using constraints](./overview/) for composition and keeping the input, [Working with
violations](./violations/) for rendering and inspecting failures, [Interpreted and opaque](./constraints/)
for what makes a rule inspectable and what an escape hatch costs, and [Localization](./localization/) for
translating failures — with [Adding a language](./adding-a-language/) for the working order of a new
translation and [Fable support](./fable/) for the JavaScript target.
