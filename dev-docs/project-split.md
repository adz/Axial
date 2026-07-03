# Axial Package Boundaries

This file records the current package split. Detailed migration history belongs in current ideas only while it is active.

## Public Packages

```text
Axial                  umbrella package
Axial.Flow             workflow execution and orchestration
Axial.ErrorHandling    typed Check constraints, Result helpers, result { }
Axial.Refined          Parse helpers, refined values, refine { }
Axial.Schema           model and value schema definitions
Axial.Validation       Validation, Diagnostics, validate { }
Axial.Validation.Schema schema/input/validation/rules interpreters
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

`Axial.Flow` must not depend on `Axial.ErrorHandling`, `Axial.Refined`, `Axial.Schema`, or `Axial.Validation`.

`Axial.ErrorHandling` owns:

```text
Check<'value>
CheckFailure
Check composition
Typed Check modules
Result
Collection
ResultBuilder
result { }
```

`Check` is the complete typed value-constraint subsystem:

```text
Check<'value> = 'value -> Result<unit, CheckFailure list>
```

Checks are path-free, raw-input-free value programs. Value-preserving gates, extraction helpers, and typed failure
attachment belong to `Result`. Parsing and refined value construction belong to `Axial.Refined`.

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

Refined/domain value types can expose value schemas for schema integration, but `Axial.Refined` must not depend on
`Axial.Schema`, `Axial.Validation`, or `Axial.Flow`.

`Axial.Schema` owns:

```text
Schema<'model>
ValueSchema<'value>
Field<'model, 'value>
external field names
constructor application descriptors
getter/inspection descriptors
field ordering
schema constraint metadata
```

Schema definitions describe model shape, trusted construction, inspection, and portable constraints. Schema constraints
must retain metadata for interpreters and lower to executable `Check` programs where appropriate.

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

`Axial.Validation.Schema` owns schema interpreters:

```text
Input parsing over schemas
validation of existing models over schemas
schema constraints to Diagnostics
schema constraints to Check
rules over trusted models
SchemaError / diagnostic interpretation
```

Schema interpreters give schema definitions path-aware, raw-input-aware, diagnostic, and contextual behavior. Core
schema definitions stay independent of validation and flow execution.

`Axial` is the umbrella package. It references the leaf packages and re-exports the common builders, but it should not
become a new abstraction layer.

## Dependency Rules

Leaf packages stay independent unless an explicit integration package is introduced later.

```text
Axial                -> Axial.Flow, Axial.ErrorHandling, Axial.Refined, Axial.Schema, Axial.Validation,
                        Axial.Validation.Schema
Axial.Flow           independent
Axial.ErrorHandling  independent
Axial.Refined        independent
Axial.Schema         independent
Axial.Validation     independent
Axial.Validation.Schema -> Axial.Schema, Axial.Validation, Axial.ErrorHandling, Axial.Refined
Axial.Flow.*         -> Axial.Flow
```

`Axial.Flow` owns `Policy<'env, 'error, 'input, 'output>` because policy can read workflow environment and adapt
requirements into typed workflow errors. `Policy` constructors should accept ordinary `Result`-returning functions so
`Axial.Flow` does not depend on `Axial.ErrorHandling`, `Axial.Refined`, `Axial.Schema`, or `Axial.Validation`.

## Interop Rules

`Flow` binds standard F# `Result<'value, 'error>` directly, so `Axial.Flow` does not need a dependency on
`Axial.ErrorHandling`.

Use `Bind.error` or `Bind.mapError` only at a `flow { }` bind site when the source error must be assigned or mapped
immediately before binding.

Pure code should stay in `Result` with `Result.require`, `Result.mapError`, or focused `Result` helpers. Accumulating
input validation should stay in `Validation`.

Use `Policy` for named, reusable, environment-aware requirements in workflow code. Use `Bind.error` or `Bind.mapError`
for immediate one-off bind-site assignment or mapping.
