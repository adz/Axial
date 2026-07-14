# Axial Package Boundaries

This file records the current package split.

## Public Packages

```text
Axial                  umbrella package
Axial.Flow             workflow execution and orchestration
Axial.ErrorHandling    Check, Predicate, Result (Axial.ErrorHandling namespace);
                        Validation, Diagnostics, validate { } (Axial.Validation namespace);
                        Parse, Refine, refine { } (Axial.Refined namespace)
Axial.Schema           model/value schema declaration (Schema module) and interpreters
                        (Model.parse/reconstruct, Rules, Inspect, JsonSchema)
Axial.Codec            compiled JSON codecs, depends on Axial.Schema
Axial.Flow.*            Flow add-on packages
```

Before 1.0, these packages share one coordinated version from `Directory.Build.props`.

## Package Timing

`Axial.Schema` is a separate project from `Axial.ErrorHandling`. Do not incubate schema definitions inside
`Axial.ErrorHandling`.

Keep the package boundary real: `Axial.Schema` is a leaf-adjacent package (it depends on `Axial.ErrorHandling`, but
nothing depends back on it except `Axial.Codec`), so schema definitions stay independent of workflow execution.

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

`Axial.Flow` must not depend on `Axial.ErrorHandling` or `Axial.Schema`.

`Axial.ErrorHandling` owns three namespaces, all in one package:

```text
Axial.ErrorHandling namespace: Check<'value>, CheckFailure, Predicate, Result, Collection, ResultBuilder, result { }
Axial.Validation namespace:    Validation<'value,'error>, Diagnostics<'error>, Diagnostic<'error>, Path, PathSegment,
                                ValidateBuilder, validate { }
Axial.Refined namespace:       Parse, Refine, RefinementError, NonBlankString, PositiveInt, NonEmptyList<'value>,
                                RefineBuilder, refine { }
```

These three namespaces live in one package because none of them depend on `Axial.Schema` or `Axial.Flow`, and all
three are single-value/error-vocabulary concerns rather than model-declaration concerns. `Axial.Refined` depends
only on `Check`/`Result`, not on schema metadata. `Axial.ErrorHandling` as a whole must not depend on
`Axial.Schema` or `Axial.Flow`.

`Check` is the complete typed value-constraint subsystem:

```text
Check<'value> = 'value -> Result<unit, CheckFailure list>
```

Checks are path-free, raw-input-free value programs. Value-preserving gates and generic Option/seq/nullable
extraction helpers belong to `Result`; parsing and refined value construction belong to `Axial.Refined`. `Result`
must not grow a predicate- or domain-specific helper when the same rule already is, or should be, a named type in
`Axial.Refined`'s catalog.

`Axial.Schema` owns both the declaration surface and the interpreters that consume it:

```text
Schema module:  Schema<'model>, ValueSchema<'value>, Field<'model,'value>, external field names, constructor
                application descriptors, getter/inspection descriptors, field ordering, schema constraint metadata
Model module:   Model.parse / Model.parseWith (untyped RawInput -> trusted model), Model.reconstruct (an existing
                model value -> the same trust guarantee)
Rules module:   RuleSet<'model,'error>, contextual rules over an already-trusted model (see dev-docs/decisions for
                the "unresolved" flag on this one)
Inspect / JsonSchema: read-model metadata and JSON Schema document generation
RefinedSchema:  the one-way bridge from Axial.Refined types into ValueSchema<'value> field declarations
```

`Schema` (the module) is only for declaring a schema. Every operation that produces or verifies a model using a
schema as authority — parse, reconstruct, eventually construct — lives in the separate `Model` module, not on
`Schema`, so `Model.parse` reads as "parse into a model" rather than "parse a schema."

Schema constraints must retain metadata for interpreters and lower to executable `Check` programs where appropriate.
Schema definitions must also retain enough typed construction and field-order information for high-performance codec
interpreters to compile direct hot-path plans (`Schema.specialize` + `IFieldChainFactory`). Do not make codecs walk a
generic metadata tree, perform runtime reflection, or invoke `obj array` constructor application for every decoded
value.

The authored schema path must stay AOT-, trimming-, and Fable-compatible. Runtime reflection belongs only in
optional .NET import/tooling packages or experiments; it must not become the foundation for schema construction,
constructor binding, validation, or codec execution.

`Axial.Codec` owns compiled JSON codecs, depending only on `Axial.Schema` (via `InternalsVisibleTo` for the
type-erased chain). It enforces wire shape only and does not run constraint metadata; that's `Model.parse`'s job.

`Axial` is the umbrella package. It references the leaf packages and re-exports the common builders, but it should not
become a new abstraction layer.

## Dependency Rules

```text
Axial                -> Axial.Flow, Axial.ErrorHandling, Axial.Schema, Axial.Codec
Axial.Flow            independent
Axial.ErrorHandling   independent (hosts Axial.ErrorHandling, Axial.Validation, Axial.Refined namespaces)
Axial.Schema          -> Axial.ErrorHandling
Axial.Codec           -> Axial.Schema
Axial.Flow.*          -> Axial.Flow
```

The `leaf packages stay independent of each other` API-shape test (`tests/Axial.ApiShape.Tests/ApiShapeTests.fs`)
enforces this graph.

`Axial.Flow` owns `Policy<'env, 'error, 'input, 'output>` because policy can read workflow environment and adapt
requirements into typed workflow errors. `Policy` constructors accept ordinary `Result`-returning functions so
`Axial.Flow` does not need to depend on `Axial.ErrorHandling` or `Axial.Schema`.

## Interop Rules

`Flow` binds standard F# `Result<'value, 'error>` directly, so `Axial.Flow` does not need a dependency on
`Axial.ErrorHandling`.

Use `Bind.error` or `Bind.mapError` only at a `flow { }` bind site when the source error must be assigned or mapped
immediately before binding.

Pure code should stay in `Result` with `Result.mapError` or focused `Result` helpers. The focused `Result` surface is
deliberately fail-fast: generic combinators/conversions and extraction helpers for option/value-option/nullable/
result/sequence shapes. Do not grow a second predicate-specific constraint catalog in `Result`; new reusable value
constraints belong in `Check.*`, and new reusable *named proof types* belong in `Axial.Refined`. Accumulating input
validation should stay in `Validation`.

Use `Policy` for named, reusable, environment-aware requirements in workflow code. Use `Bind.error` or `Bind.mapError`
for immediate one-off bind-site assignment or mapping.
