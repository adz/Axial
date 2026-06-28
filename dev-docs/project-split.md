# Axial Package Boundaries

This file records the current package split. Detailed migration history belongs in current ideas only while it is active.

## Public Packages

```text
Axial                  umbrella package
Axial.Flow             workflow execution and orchestration
Axial.ErrorHandling    Check predicates, Result helpers, result { }
Axial.Refined          Parse helpers, refined values, refine { }
Axial.Validation       Validation, Diagnostics, validate { }
Axial.Flow.*           Flow add-on packages
```

Before 1.0, these packages share one coordinated version from `Directory.Build.props`.

## Responsibilities

`Axial.Flow` owns:

```text
Flow<'env, 'error, 'value>
Execution<'value, 'error>
Exit<'value, 'error>
Cause<'error>
Policy<'env, 'error, 'input, 'output>
Layer<'input, 'error, 'output>
Scope
BindError
flow { }
layer { }
```

`Axial.Flow` must not depend on `Axial.ErrorHandling`, `Axial.Refined`, or `Axial.Validation`.

`Axial.ErrorHandling` owns:

```text
Check
Result
Collection
ResultBuilder
result { }
```

`Check` is the predicate module. Value-preserving gates, extraction helpers, and typed failure attachment belong to
`Result`.

`Axial.Refined` owns:

```text
Parse
Refine
RefinementError
NonBlankString
PositiveInt
NonEmptyList<'value>
RefineBuilder
refine { }
```

`Axial.Validation` owns:

```text
Validation<'value, 'error>
Diagnostics<'error>
Diagnostic<'error>
Path
PathSegment
ValidateBuilder
validate { }
```

`Axial` is the umbrella package. It references the leaf packages and re-exports the common builders, but it should not
become a new abstraction layer.

## Dependency Rules

Leaf packages stay independent unless an explicit integration package is introduced later.

```text
Axial                -> Axial.Flow, Axial.ErrorHandling, Axial.Refined, Axial.Validation
Axial.Flow           independent
Axial.ErrorHandling  independent
Axial.Refined        independent
Axial.Validation     independent
Axial.Flow.*         -> Axial.Flow
```

## Interop Rules

`Flow` binds standard F# `Result<'value, 'error>` directly, so `Axial.Flow` does not need a dependency on
`Axial.ErrorHandling`.

Use `Bind.error` or `Bind.mapError` only at a `flow { }` bind site when the source error must be assigned or mapped
immediately before binding.

Pure code should stay in `Result` with `Result.require`, `Result.mapError`, or focused `Result` helpers. Accumulating
input validation should stay in `Validation`.
