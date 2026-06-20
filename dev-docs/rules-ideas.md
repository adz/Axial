# Rules Ideas

This note explores three shapes for a first-class validation/rule system in FsFlow, then follows the consequences into a `parse don't validate` story that stays F#-native, path-aware, and more regular than Rails' `ActiveModel`.

## Problem Statement

The current `Check` surface is good at one thing: checking a value now and returning a `Result`.
It is not good at building reusable validation programs that can be applied later to a model, a field, or a nested collection.

That creates two pressures:

- checks want to be reusable and composable before they are applied
- object validation wants field paths, nested scopes, and source access for rendering

The design question is not "how do we make checks prettier".
It is "what is the first-class unit of validation in FsFlow".

The answer should let us write something compact and regular:

```fsharp
Rule.For<User> {
    field _.Username |> whenNotBlank UsernameRequired
    field _.Username |> whenMinLength 3 UsernameTooShort
    sub _.Address {
        field _.City |> whenNotBlank CityRequired
    }
    each _.Lines (fun i ->
        field _.Name |> whenNotBlank (LineNameRequired i)
    )
}
```

That exact syntax is not yet available without a compiler feature or generator. The point of this note is to separate the shapes that are possible from the shapes that are merely attractive.

## The Three Variants

### 1. Explicit Field Descriptors

This is the most honest shape.

```fsharp
type Field<'root, 'value> =
    { Path: PathSegment list
      Get: 'root -> 'value }

type Rule<'root, 'error> = 'root -> Validation<'root, 'error>
```

You explicitly declare the source path and the getter once, then reuse it everywhere.

```fsharp
let username =
    Field.ofGetter [ PathSegment.Name "Username" ] (fun (u: User) -> u.Username)

let validateUser : Rule<User, UserError> =
    Rule.all
        [
            Rule.whenNotBlank UsernameRequired username
            Rule.whenMinLength 3 UsernameTooShort username
        ]
```

Why this is good:

- AOT-safe
- no reflection
- no quotation dependency
- no hidden magic
- the field path is available for rendering, diagnostics, and nested composition

Why this is not enough:

- it repeats the member name
- it is not the compact shape people want from Rails
- it is correct, but not especially slick

This is the best baseline. If the final design cannot beat this on ergonomics without losing its guarantees, the extra abstraction is not worth it.

### 2. Quotations

This is the most promising way to recover field metadata without generation.

```fsharp
field <@ fun (u: User) -> u.Username @>
```

The quotation gives us an AST, not just a function value. That means we can inspect:

- the member name
- nested member access
- indexer access
- source path segments

The value path then becomes data, not a convention.

Why this is attractive:

- it avoids duplicate field names
- it can infer `.Username` without a generator
- it can describe nested access in a structured way
- it stays local to F#, without needing source rewriting

What it buys us over raw reflection:

- the shape is explicit in the syntax tree
- nested member access is easier to reason about than a post hoc delegate inspection
- the compiler already knows the source expression, so we are not guessing at runtime behavior

What it costs:

- the syntax is heavier than `_.Username`
- it is still not the exact Rails surface
- it introduces a dependency on quotation handling
- for AOT, we must keep the implementation entirely on expression inspection and avoid reflective "find property by name" fallbacks

The important constraint is that quotations should be used as metadata, not as a runtime execution trick.
If the design starts depending on converting quotations back into live object access through reflection, it gets brittle fast.

### 3. Proof-Based Validation

This is the most F#-native model when the goal is type transformation.

```fsharp
type Proof<'failure, 'value> =
    | Valid of 'value
    | Invalid of 'failure list * Map<string, 'failure list>
```

This is the style used by `FSharp.Data.Validation`: validation is not just "pass or fail", it is a proof that a value has been transformed into a trusted shape.

The shape is attractive because it naturally supports:

- smart constructors
- domain-specific failures
- field-scoped failures
- nested collection failures
- optional and conditional validation

The good part is not the proof type itself.
The good part is that the API makes parsing and validation the same move:

```fsharp
validation {
    withField (fun () -> vm.Username)
    refuteWith isRequired MissingUsername
    disputeWithFact (UsernameTooShort 3) (fun u -> u.Length >= 3)
    qed Username
}
```

This style is excellent when the output is a trusted domain type, not just a report.

What it buys us:

- parse/validate/construct collapse into one flow
- the final value is already trusted
- failure values can stay strongly typed
- nested models and lists are a natural fit

What it costs:

- more surface area than a simple rule DSL
- more concepts than many app developers want to carry around
- a larger gap between "small field checks" and "validated model construction"

If we want FsFlow to support "parse don't validate" as a first-class style, the proof-based shape is the strongest conceptual anchor.

## Comparison

### Explicit Descriptors

Best for:

- AOT certainty
- zero magic
- simple implementation
- predictable diagnostics and rendering

Weakness:

- repetitive
- not very slick

### Quotations

Best for:

- compact syntax
- field-name inference
- nested path capture
- maintaining a good developer experience without a generator

Weakness:

- more implementation complexity
- not the exact `_.Username` surface
- must be kept AOT-clean by design

### Proof-Based

Best for:

- parsing into trusted domain values
- smart constructors
- nested validation with typed failures
- a first-class `parse don't validate` story

Weakness:

- larger API
- more ceremony
- easier to overbuild

## What "Slicker Than ActiveModel" Actually Means

Rails is slick because the validation declaration is:

- short
- declarative
- field-aware
- regular

It is not slick because it is magical. It is slick because it has a stable mental model.

FsFlow should aim for the same regularity, but with better foundations:

- typed failures instead of strings
- tree-shaped diagnostics instead of a flat list
- typed paths instead of loosely coupled field labels
- parsed domain values instead of raw forms that remain half-trusted

That means the model should not be "a bag of validators".
It should be "a rule program that knows the source model, the field path, and the target type".

## Parse Don't Validate

This is the real design target.

The rule system should not merely confirm that a value is okay.
It should turn an untrusted input shape into a trusted output shape.

That suggests a pipeline like this:

```fsharp
Raw input -> parse -> validate -> construct trusted model
```

or, more honestly in F#:

```fsharp
Raw input -> Proof / Validation -> trusted domain value
```

The important consequence is that validation is not a side activity.
It is the transition from untrusted to trusted.

### First-Class Support

FsFlow can support that by making the following first-class:

- `Rule.For<'root>` or `Parse.For<'raw,'model>`
- scoped path helpers for nested members and list items
- typed source descriptors that can be reused for rendering
- a proof/result bridge that can produce trusted domain values

A useful layering would be:

```fsharp
Check<'value>
ValueRule<'value, 'error>
ModelRule<'root, 'error>
Parse<'raw, 'model, 'error>
```

This lets the system move from raw checks to model construction without flattening everything into one monolith.

### What the API Should Do

The surface should support these jobs:

1. Extract a field value from a root model.
2. Attach the field path once, not at every rule.
3. Validate nested records and lists with the same rule style.
4. Reuse the same source metadata for form rendering.
5. Produce a trusted model when validation succeeds.

That suggests a model-oriented API more like this:

```fsharp
Rule.For<User> {
    field _.Username |> whenNotBlank UsernameRequired
    field _.Username |> whenMinLength 3 UsernameTooShort
    sub _.Address {
        field _.City |> whenNotBlank CityRequired
    }
    each _.Lines (fun i ->
        field _.Quantity |> whenPositive (LineQuantityInvalid i)
    )
    build
}
```

The exact syntax may end up being quotation-backed, proof-backed, or explicit-descriptor-backed.
The shape that matters is:

- source binding once
- rules compose before application
- nested data keeps its path
- success returns a trusted model

## Recommended Direction

If we want the slickest design that still fits F# well, the hierarchy should be:

1. Explicit descriptors as the fallback and AOT baseline.
2. Quotations as the ergonomic layer when we want member inference.
3. Proof-based validation as the deep parse/validate/construct story.

That gives us a coherent ladder:

- `Check` for raw predicates and tiny helpers
- `ValueRule` for reusable single-value validation
- `ModelRule` for path-aware object validation
- `Parse` or `Proof` for turning raw data into trusted domain values

That is the shape I would keep pushing toward.

It is more F#-native than a Rails imitation, more regular than a validator bag, and more honest about where the source metadata actually comes from.
