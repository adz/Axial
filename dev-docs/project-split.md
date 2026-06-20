# Axial Project Split

This file is the decided plan for splitting the current `Axial` codebase into separate Axial packages.

The audience is a coding agent. The plan is intentionally explicit and avoids "maybe this, maybe that" branches unless a
question is truly still open.

## Objective

Split the current codebase into these packages:

```text
Axial               umbrella/meta package
Axial.Flow
Axial.Result
Axial.Validation
```

Planned later:

```text
Axial.Refined
```

Goals:

- make `Axial.Flow` the primary effect package
- make `Axial` an umbrella/meta package, not the primary implementation package
- keep effectful execution separate from pure fail-fast control flow
- keep fail-fast `Result` work separate from accumulating validation
- avoid forcing accumulated-error machinery into fail-fast APIs
- avoid forcing `result { }` into the validation package where it does not fit
- keep package dependencies explicit and minimal

## Decided Package Responsibilities

These are decisions, not options.

### Axial.Flow

`Axial.Flow` owns effectful execution and orchestration.

It contains concepts such as:

```fsharp
Flow<'Env,'Error,'A>
Execution<'A,'Error>
Exit<'A,'Error>
Cause<'Error>
Layer<'Input,'Error,'Output>
Scope
BindError
```

It also owns the computation expressions:

```text
flow { }
layer { }
```

`Axial.Flow` may bind plain `Result<'T,'Error>` directly in `flow { }`.

`Axial.Flow` must not depend on:

- `Axial.Result`
- `Axial.Validation`

`Axial.Flow` must not expose public APIs whose signatures mention:

- `Check`
- `Validation`
- `Diagnostics`
- `ValidateBuilder`

`Axial.Flow` is the primary effect package.

### Axial

`Axial` is the umbrella/meta package.

It does not own the implementation surface itself. Its job is to present the combined package family, and it may
re-export or reference the leaf packages.

The umbrella package should not become a new core abstraction layer.

### Axial.Result

`Axial.Result` owns fail-fast pure control flow.

It contains:

```text
result { }
ResultBuilder
Result helpers
check
ensure
requireSome
mapError
bindError
```

Purpose:

```text
Represent one success or one error.
Stop at the first error.
Provide the right home for fail-fast sequential composition outside Flow.
```

This package also owns `Check`.

Rule:

- if an API returns `Result<_,_>` or `Check<_>`, it belongs to `Axial.Result`
- if an API returns `Validation<_,_>` or manipulates `Diagnostics`, it belongs to `Axial.Validation`

Reason this package exists:

- `Validation` should not be forced to represent accumulated errors when the user only wants fail-fast behavior
- `result { }` belongs with fail-fast `Result`, not with accumulating validation
- users who are not inside `flow { }` still need a pure sequential CE over `Result`
- the current `Check` abstraction is already a fail-fast `Result` abstraction, not an accumulating one

`Axial.Result` must not depend on:

- `Axial.Flow`
- `Axial.Validation`

### Axial.Validation

`Axial.Validation` owns accumulating validation and diagnostics.

It contains:

```fsharp
Validation<'T,'Error>
Diagnostics<'Error>
Diagnostic<'Error>
Path
PathSegment
ValidateBuilder
```

It also owns:

```text
validate { }
```

It may contain validation-focused helper types such as:

```fsharp
Validator<'Input,'Error,'Output>
```

Purpose:

```text
Transform less trustworthy input into more trustworthy output while accumulating errors.
```

Important semantic note:

- `Validation` differs from `Result` by accumulation semantics and accumulated error representation
- `Validation.bind` is still fail-fast
- accumulation happens through applicative composition such as `map2`, `apply`, `collect`, `sequence`, and `validate { and! ... }`

`Axial.Validation` must not depend on:

- `Axial.Flow`
- `Axial.Result`
- `Axial.Refined`

### Axial.Refined

`Axial.Refined` is a later package for concrete narrowed data types such as:

```text
NonBlankString
PositiveInt
NonEmptyList<'T>
```

It is not part of the first split. Do not block the first split on refined-type design.

## Dependency Graph

Target after the first split:

```text
Axial               umbrella/meta package
Axial.Flow          independent
Axial.Result        independent
Axial.Validation    independent
```

Planned later:

```text
Axial.Refined       -> Axial.Result
Axial.Refined       -> Axial.Validation
```

Rationale:

- refined construction often needs a fail-fast constructor
- refined validation often needs an accumulating adapter
- that dependency belongs in the future refined package, not in the base packages

If tighter interoperability is needed later, create explicit integration packages instead of forcing dependencies into
the base packages.

## Canonical Interop Rules

These rules preserve the package boundaries.

### Result into Flow

The canonical bridge from `Axial.Result` into `Axial.Flow` is direct `Result` binding.

`flow { }` may bind:

```fsharp
Result<'T,'Error>
```

because that does not require an Axial package dependency. It only uses the standard F# `Result` type.

### Validation into Flow

The canonical bridge from `Axial.Validation` into `Axial.Flow` is:

```fsharp
Validation.toResult : Validation<'T,'Error> -> Result<'T, Diagnostics<'Error>>
```

Then `Axial.Flow` can consume the result because `flow { }` already binds `Result`.

Do not add a base-package dependency just to make `flow { }` bind `Validation` directly.

### Result into Validation

`Axial.Validation` may expose:

```fsharp
Validation.ofResult : Result<'T,'Error> -> Validation<'T,'Error>
```

This lifts one fail-fast error into one validation error.

That helper does not require a dependency on `Axial.Result` because it operates on the standard F# `Result` type.

### BindError

`Axial.Flow` keeps `BindError`, but its implementation must not call validation-specific helpers.

In the current codebase, `BindError` uses `Check.withError`. That coupling must be removed before or during the split.

`Axial.Flow` should instead keep its own local conversion from:

```fsharp
Result<'T, unit> -> Result<'T, 'Error>
```

so that `BindError.withError` still works without pulling in `Axial.Result` or `Axial.Validation`.

## Non-Goals

Do not do these as part of the first split:

- do not rename `Flow` to `Pipe`
- do not introduce `Axial.Core`
- do not redesign `Validation` into a different carrier shape
- do not design `Axial.Refined`
- do not merge `Axial.Result` into `Axial.Validation`
- do not add direct `Validation` binding support to `flow { }`

## Project Layout

Target layout:

```text
/src
  /Axial.Flow
    Axial.Flow.fsproj
  /Axial.Result
    Axial.Result.fsproj
  /Axial.Validation
    Axial.Validation.fsproj

/tests
  /Axial.Flow.Tests
  /Axial.Result.Tests
  /Axial.Validation.Tests
```

Later:

```text
/src
  /Axial.Refined
    Axial.Refined.fsproj

/tests
  /Axial.Refined.Tests
```

## Migration Order

Follow this order. Do not start by renaming everything blindly.

### Step 1: classify existing Axial code

For each public source file in `src/Axial`, assign exactly one target:

- `Axial.Flow`
- `Axial.Result`
- `Axial.Validation`
- `defer`

`defer` means "not needed for first split execution yet", not "shared".

### Step 2: remove current forbidden couplings

Before creating packages, remove any implementation dependency that would force one package to reference another.

The known coupling to remove first is:

```text
BindError -> Check.withError
```

### Step 3: create the package projects

Create:

```text
Axial
Axial.Flow
Axial.Result
Axial.Validation
```

with no Axial project references between the leaf packages.

The umbrella package `Axial` may reference the leaf packages, but it must not be a dependency source for them.

### Step 4: move Result code first

Move fail-fast pure code into `Axial.Result` first.

This includes:

- `ResultBuilder`
- any `result { }` surface
- `Check.fs`
- any pure fail-fast helpers that do not belong to Flow

Reason:

- it locks in the `Result` / `Validation` split early
- it prevents validation from accidentally inheriting fail-fast staging concerns

### Step 5: move Validation code second

Move accumulating validation code into `Axial.Validation`.

Expected first movers:

- `Diagnostics.fs`
- `Validation.fs`
- `ValidateBuilder.fs`

Do not move `Check.fs` into `Axial.Validation`.
Keep `Check` in `Axial.Result`.
If a future helper truly needs accumulation semantics, add a separate `Axial.Validation` helper instead.

### Step 6: move Flow code third

Move effect/runtime code into `Axial.Flow`.

Keep:

- `flow { }`
- `layer { }`
- direct `Result` binding

Do not add:

- `result { }`
- direct `Validation` binding

### Step 7: split tests by package boundary

Create at least:

```text
Axial.Flow.Tests
Axial.Result.Tests
Axial.Validation.Tests
```

Each test project must build against its own package boundary.

### Step 8: retarget dependent packages, examples, and docs

Only after the three new projects compile and tests are passing:

- retarget service packages
- retarget examples
- retarget docs
- regenerate reference docs

## Success Criteria

The first split is complete when all of the following are true:

- `Axial` is treated as the umbrella/meta package, not the primary effect package
- `Axial` builds as the umbrella package
- `Axial.Flow` builds with no Axial project references
- `Axial.Result` builds with no Axial project references
- `Axial.Validation` builds with no Axial project references
- `result { }` exists in `Axial.Result`
- `Check` exists in `Axial.Result`
- `validate { }` exists in `Axial.Validation`
- `flow { }` still binds `Result<'T,'Error>`
- `Validation.toResult` exists as the canonical bridge into Flow
- `BindError.withError` still works from user code
- users can install only the package they need for their use case
- docs and examples match the new package boundaries

## Final Rule For Check

`Check` belongs to `Axial.Result`.

This is not an open question.

Reason:

- the current `Check` abstraction is defined in fail-fast `Result` terms
- current `Check` helpers return `Check<_>` or `Result<_,_>`, not `Validation<_,_>`
- current `Check` helpers do not manipulate `Diagnostics`
- if a helper truly wants accumulation, it should become a `Validation` helper explicitly rather than staying in `Check`
