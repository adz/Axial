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

## Package Timing

`Axial.Schema` starts as a separate project immediately when schema source work begins. Do not incubate schema
definitions inside `Axial.Validation` behind a future package boundary.

Keep the first schema implementation small, but keep the package boundary real from the start so schema definitions stay
independent of diagnostics, raw input, validation interpreters, and flow execution. This preserves `Axial.Schema` as a
leaf package and lets `Axial.Validation.Schema` own the integration behavior explicitly.

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

Refined/domain value types can participate in schema integration through examples, user code, or an integration package,
but `Axial.Refined` must not depend on `Axial.Schema`, `Axial.Validation`, or `Axial.Flow`. Do not ship schema-valued
APIs from `Axial.Refined` unless the package-boundary invariant is deliberately changed.

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

Schema definitions must also retain enough typed construction and field-order information for high-performance codec
interpreters to compile direct hot-path plans. Interpreters such as JSON codecs should be able to lower a schema into
cached field-name bytes, indexed field slots, typed decoders, and direct constructor application. Do not make codecs
walk a generic metadata tree, perform runtime reflection, or invoke `obj array` constructor application for every
decoded value.

The authored schema path must stay AOT-, trimming-, and Fable-compatible. Runtime reflection belongs only in optional
.NET import/tooling packages or experiments; it must not become the foundation for schema construction, constructor
binding, validation, or codec execution.

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

Create and use the `Axial.Validation.Schema` project before implementing raw input parsing, schema validation, schema
diagnostics, or schema rules. Those features should not be staged inside `Axial.Validation` or `Axial.Schema`.

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

Pure code should stay in `Result` with `Result.require`, `Result.guard`, `Result.mapError`, or focused `Result`
helpers. The focused `Result` surface is deliberately fail-fast: keep generic combinators/conversions, extraction
helpers for option/value-option/nullable/result/sequence shapes, and value-preserving guards that adapt executable
`Check` programs. Do not grow a second predicate-specific constraint catalog in `Result`; new reusable value
constraints belong in `Check.*` first, then flow through the generic fail-fast guard. Accumulating input validation
should stay in `Validation`.

Use `Policy` for named, reusable, environment-aware requirements in workflow code. Use `Bind.error` or `Bind.mapError`
for immediate one-off bind-site assignment or mapping.
