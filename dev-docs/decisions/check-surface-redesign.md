# Check, Take, and BindError Surface Redesign

## Extracted From

- `dev-docs/PLAN.md`
- `dev-docs/TASKS.md`
- `dev-docs/decisions/validation-surface.md`
- Current discussion about discoverability, regular naming, bind-site error adaptation, and validation-library prior art

## Source Date

- 2026-06-05: proposed revision of the validation and bind-error naming surface

## Decision

FsFlow should split validation and bind adaptation into four explicit concepts:

- `Check<'value>` is the unit-error predicate carrier.
- `Take` is the value-returning validation surface.
- `BindError` is a flow bind-site marker for assigning or mapping errors before binding.
- `Validation<'value, 'error>` is the accumulating diagnostics carrier.

The module chooses the validation intent. The function name chooses the property.

`Take` has two families:

- `Take.whenX` keeps the original input when property `x` holds.
- `Take.x` extracts or narrows the value made available by property `x`.

Use `when` only inside `Take`, and only for value-preserving forms:

```fsharp
Check.notBlank     : string -> Check<unit>
Take.whenNotBlank  : string -> Check<string>

Check.some         : 'value option -> Check<unit>
Take.whenSome      : 'value option -> Check<'value option>
Take.some          : 'value option -> Check<'value>

Check.exactlyOne    : seq<'value> -> Check<unit>
Take.whenExactlyOne : 'collection -> Result<'collection, CardinalityFailure>
Take.exactlyOne     : seq<'value> -> Result<'value, CardinalityFailure>
```

`Check` answers whether the value satisfies a property. `Take.whenX` admits the original value unchanged. `Take.x` takes the useful inner, extracted, or narrowed value.

Error attachment stays separate:

- use `Check.withError` for pure unit-error `Check<'value>` code outside `flow { }`, including results produced by `Take`
- use `Result.mapError`, `Validation.mapError`, or `Flow.mapError` when working directly with those carriers
- use `BindError.withError` or `BindError.map` only at flow bind sites

Remove `Guard.Of` and `Guard.MapError` before 1.0. They use broad overloads to hide too much source interpretation.

Replace the old value-returning `Guard` concept with `Take`.

## Why

The old `okIf*` / `failIf*` surface was regular locally, but it encoded the `Result` branch instead of the property being checked.

The old `Guard.Of` / `Guard.MapError` surface solved a real problem: `flow { }` can bind many source shapes directly, but users sometimes need to assign or map the error before the bind.

The problem was the name and scope. `Guard.Of` could mean `bool` false, `None`, `Error ()`, async option unwrapping, validation error replacement, or flow error replacement. That is too much hidden behavior for one name.

The revised design keeps the good ergonomic goal while making each concept guessable:

- `Check.x` checks property `x` and returns `unit` on success.
- `Take.whenX` keeps the source value when property `x` holds.
- `Take.x` extracts or narrows the useful value exposed by property `x`.
- `BindError.withError` assigns an error to a missing/unit-failure bind source.
- `BindError.map` maps an existing error on a bind source.

## Target Grammar

Use one primary spelling per concept. Do not keep compatibility aliases before 1.0.

### `Check` Names

Use direct property names for predicates:

- `some`
- `none`
- `valueSome`
- `valueNone`
- `notNull`
- `isNull`
- `notEmpty`
- `empty`
- `notBlank`
- `blank`
- `notNullOrEmpty`
- `nullOrEmpty`
- `equalTo`
- `notEqualTo`
- `contains`
- `exactlyOne`
- `atMostOne`
- `atLeastOne`
- `moreThanOne`

Use `has*` when the property is naturally measurable:

- `hasValue`
- `hasNoValue`
- `hasCount`
- `hasDuplicates`

Reserve `hasValue` and `hasNoValue` for `System.Nullable<'T>`. Use `some` and `none` for `option<'T>`.

Use `isTrue`, `isFalse`, and `isNull` for reserved literal checks because `true`, `false`, and `null` are reserved words.

Use explicit logic combinators:

- `negate`
- `both`
- `either`
- `all`
- `any`

Keep constructor and interop names under `from*`:

- `fromPredicate`
- `fromTry`
- `fromChoice`

Do not use branch-result prefixes:

- no `okIf*`
- no `failIf*`
- no `require*`
- no `ensure*`

### `Take` Names

Use the same base names as `Check`, then choose the success shape with the `Take` spelling.

Use `Take.whenX` when success keeps the original input:

- `whenSome`
- `whenValueSome`
- `whenHasValue`
- `whenNotNull`
- `whenNotEmpty`
- `whenNotNullOrEmpty`
- `whenNotBlank`
- `whenExactlyOne`
- `whenAtMostOne`

Use bare `Take.x` when success extracts or narrows to the value the caller wants next:

- `some`
- `valueSome`
- `hasValue`
- `exactlyOne`
- `atMostOne`

Future extraction names should follow the same grammar:

- `head`
- `last`
- `item`
- `valueFor`
- `isType`

`Take.whenX` preserves the input shape:

- `Take.whenNotBlank` keeps the original string.
- `Take.whenNotEmpty` keeps the original collection.
- `Take.whenExactlyOne` keeps the original array/list/collection when it has exactly one element.
- `Take.whenAtMostOne` keeps the original array/list/collection when it has zero or one elements.

`Take.x` returns a tighter success value:

- `Take.some` unwraps `option<'value>` to `'value`
- `Take.valueSome` unwraps `voption<'value>` to `'value`
- `Take.hasValue` unwraps `Nullable<'value>` to `'value`
- `Take.exactlyOne` extracts the only element from `seq<'value>`
- `Take.atMostOne` extracts zero or one element as `'value option`

This is intentional. It mirrors APIs such as LINQ `Single()` and F# option unwrapping: a successful take should return the value the caller actually wants next.

Simple `Take` helpers normally return `Check<'success>`. Helpers that can produce useful built-in diagnostics may return `Result<'success, BuiltInFailure>`.

`CardinalityFailure` should only expose cases that first-pass helpers actually emit:

- `ExpectedExactlyOne of actualCount: int`
- `ExpectedAtMostOne of actualCount: int`

Do not add future-looking public cases without a public helper that returns them.

Do not add a bare `Take.notBlank`, `Take.notEmpty`, or `Take.notNull` as aliases for the `when` forms. Those names have no extraction target and would make the shape ambiguous.

### `BindError` Names

Use `BindError` only for flow bind-site adaptation.

```fsharp
source |> BindError.withError DomainError
source |> BindError.map ErrorCase
```

`BindError.withError` means:

- this source has no useful error yet
- if it fails or is missing, use this error
- produce a marker that `flow { }` can bind

`BindError.map` means:

- this source already has an error
- map that error before `flow { }` binds it
- produce a marker that `flow { }` can bind

`BindError` values are not a general-purpose result adapter. They are bind instructions.

## Surface Mapping

### Core Helpers

| Current | Target | Notes |
| --- | --- | --- |
| `Check.fromPredicate` | keep | Constructor; preserves the input value. |
| `Check.fromTry` | keep | Constructor for .NET `Try*` tuples. |
| `Check.okIfTrueTuple` | remove | Redundant alias for `fromTry`. |
| `Check.fromChoice` | keep | Interop bridge. |
| `Check.withError` | keep | Pure unit-error to domain-error bridge. |
| `Check.not` | `Check.negate` | Avoid reserved-word quoting. |
| `Check.``and``` | `Check.both` | Avoid reserved-word quoting. |
| `Check.``or``` | `Check.either` | Avoid reserved-word quoting. |
| `Check.all` | keep | Sequence quantifier. |
| `Check.any` | keep | Sequence quantifier. |

### Boolean

| Current | Target | Result shape |
| --- | --- | --- |
| `Check.okIf` | `Check.isTrue` | `Check<unit>` |
| `Check.failIf` | `Check.isFalse` | `Check<unit>` |
| `Guard.Of(error, bool)` | `bool |> BindError.withError error` or `bool |> Check.isTrue |> BindError.withError error` | `BindError<...>` |

Docs should prefer `Check.isTrue` or a named predicate when the boolean expression is not self-explanatory.

### Options

| Current | Target | Result shape |
| --- | --- | --- |
| `Check.okIfSome` | `Take.some` | `Check<'value>` |
| `Check.failIfNone` | `Take.some` | `Check<'value>` |
| new | `Check.some` | `Check<unit>` |
| new | `Take.whenSome` | `Check<'value option>` |
| `Check.okIfNone` | `Check.none` | `Check<unit>` |
| `Check.failIfSome` | `Check.none` | `Check<unit>` |
| `Check.okIfValueSome` | `Take.valueSome` | `Check<'value>` |
| `Check.failIfValueNone` | `Take.valueSome` | `Check<'value>` |
| new | `Check.valueSome` | `Check<unit>` |
| new | `Take.whenValueSome` | `Check<'value voption>` |
| `Check.okIfValueNone` | `Check.valueNone` | `Check<unit>` |
| `Check.failIfValueSome` | `Check.valueNone` | `Check<unit>` |
| `Guard.Of(error, option)` | `option |> BindError.withError error` | `BindError<...>` |

### Nullable And Null

| Current | Target | Result shape |
| --- | --- | --- |
| `Check.okIfNotNullable` | `Take.hasValue` | `Check<'value>` |
| `Check.failIfNullable` | `Take.hasValue` | `Check<'value>` |
| `Check.notNullable` | remove | Avoid a special `CheckError.Null` path for simple missingness. |
| new | `Check.hasValue` | `Check<unit>` |
| new | `Take.whenHasValue` | `Check<Nullable<'value>>` |
| `Check.okIfNullable` | `Check.hasNoValue` | `Check<unit>` |
| `Check.failIfNotNullable` | `Check.hasNoValue` | `Check<unit>` |
| `Check.okIfNotNull` | `Take.whenNotNull` | `Check<'value>` |
| `Check.failIfNull` | `Take.whenNotNull` | `Check<'value>` |
| `Check.notNull` | `Take.whenNotNull` | `Check<'value>` |
| new | `Check.notNull` | `Check<unit>` |
| `Check.okIfNull` | `Check.isNull` | `Check<unit>` |
| `Check.failIfNotNull` | `Check.isNull` | `Check<unit>` |

The current `Check.notNull` value-preserving helper changes meaning. Because FsFlow is pre-1.0, remove the old helper rather than aliasing it.

### Collections And Cardinality

| Current | Target | Result shape |
| --- | --- | --- |
| `Check.okIfNotEmpty` | `Take.whenNotEmpty` | `Check<seq<'value>>` |
| `Check.failIfEmpty` | `Take.whenNotEmpty` | `Check<seq<'value>>` |
| `Check.notEmpty` | `Take.whenNotEmpty` | `Check<seq<'value>>` |
| new | `Check.notEmpty` | `Check<unit>` |
| `Check.okIfEmpty` | `Check.empty` | `Check<unit>` |
| `Check.failIfNotEmpty` | `Check.empty` | `Check<unit>` |
| `Check.okIfCountIs` | `Check.hasCount` | `Check<unit>` |
| `Check.okIfContains` | `Check.contains` | `Check<unit>` |
| `Check.okIfExactlyOne` | `Take.exactlyOne` | `Result<'value, CardinalityFailure>` |
| new | `Take.whenExactlyOne` | `Result<'collection, CardinalityFailure>` |
| new | `Check.exactlyOne` | `Check<unit>` |
| `Check.okIfAtMostOne` | `Take.atMostOne` | `Result<'value option, CardinalityFailure>` |
| new | `Take.whenAtMostOne` | `Result<'collection, CardinalityFailure>` |
| new | `Check.atMostOne` | `Check<unit>` |
| `Check.failIfAtMostOne` | `Check.moreThanOne` | `Check<unit>` |
| `Check.failIfExactlyOne` | use `Check.negate (Check.exactlyOne xs)` | Avoid a low-signal named inverse. |

Cardinality `Take` helpers may keep `CardinalityFailure` because the actual count is useful and expensive to reconstruct after the take runs.

Use `Result.mapError` in pure code or `BindError.map` in `flow { }` code to translate those built-in failures into domain failures.

### Value-Returning Precedent For `Take`

LINQ and nearby .NET/F# APIs should inform `Take`, but FsFlow should not copy every query operator into validation.

Useful precedents:

- [`Enumerable.Single`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.single) and F# [`Seq.exactlyOne`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-seqmodule.html#exactlyOne) assert cardinality and return the element.
- F# [`Seq.tryExactlyOne`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-seqmodule.html#tryExactlyOne) returns an option for the only element. FsFlow should prefer a typed result for diagnostic cardinality takes so empty and too-many can be distinguished when that matters.
- [`Enumerable.First`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.first), [`Enumerable.Last`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.last), [`Enumerable.ElementAt`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.elementat), and F# `Seq.tryHead` / `Seq.tryLast` / `Seq.tryItem` are positional extraction helpers.
- F# [`option`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/options) pattern matching and `Option.get` unwrap `Some` to the underlying value.
- .NET [`Nullable<'T>.HasValue` plus `Value`](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1.value) unwrap nullable values after a presence check.
- C# [declaration patterns](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns) check the runtime type and introduce a variable with the narrowed type.
- [`Dictionary.TryGetValue`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.trygetvalue) checks key presence and exposes the value in one operation.

First-pass `Take` should include or reserve these value-preserving forms when the original wrapper or collection is useful:

| Preserving kind | Take | Success shape |
| --- | --- | --- |
| option presence | `Take.whenSome` | original option |
| value option presence | `Take.whenValueSome` | original value option |
| nullable presence | `Take.whenHasValue` | original nullable |
| reference presence | `Take.whenNotNull` | original reference |
| string presence | `Take.whenNotNullOrEmpty`, `Take.whenNotBlank` | original string |
| collection presence | `Take.whenNotEmpty` | original collection |
| exact cardinality | `Take.whenExactlyOne` | original collection |
| optional cardinality | `Take.whenAtMostOne` | original collection |

First-pass `Take` should also include the extraction forms needed by current examples:

| Extraction kind | Take | Success shape |
| --- | --- | --- |
| option unwrap | `Take.some` | `'value` |
| value option unwrap | `Take.valueSome` | `'value` |
| nullable unwrap | `Take.hasValue` | `'value` |
| exact cardinality extraction | `Take.exactlyOne` | `'value` |
| optional cardinality extraction | `Take.atMostOne` | `'value option` |

Future `Take` candidates are valid only when a real example needs the returned value:

| Candidate | Shape | Rationale |
| --- | --- | --- |
| `Take.head` | `seq<'value> -> Result<'value, PositionFailure>` | Positional extraction without exception/default semantics. |
| `Take.last` | `seq<'value> -> Result<'value, PositionFailure>` | Same grammar as `head`; useful only when the last value is the next domain input. |
| `Take.item index` | `seq<'value> -> Result<'value, PositionFailure>` | Mirrors `ElementAt`; should keep index/out-of-range diagnostics. |
| `Take.valueFor key` | dictionary/map-like source -> `Check<'value>` or `Result<'value, LookupFailure>` | Mirrors `TryGetValue` while avoiding out parameters. |
| `Take.isType<'target>` | `obj -> Check<'target>` | Mirrors C# declaration patterns for runtime type narrowing. |

Do not add LINQ-style filtering or projection helpers to `Take`:

- no `Take.where`
- no `Take.select`
- no `Take.choose`
- no `Take.ofType` as a silent filter

Those are transformations. A take should fail when the invariant is not met, not silently drop or reshape unrelated values.

Be careful with lazy `seq<'value>` preservation. A cardinality check can consume enumeration. `Take.whenExactlyOne` and `Take.whenAtMostOne` should either target reusable collection shapes first or document any materialization/re-enumeration behavior explicitly.

### Equality And Comparison

| Current | Target | Result shape |
| --- | --- | --- |
| `Check.okIfEqual` | `Check.equalTo` | `Check<unit>` |
| `Check.failIfNotEqual` | `Check.equalTo` | `Check<unit>` |
| `Check.equal` | remove | Alias. |
| `Check.okIfNotEqual` | `Check.notEqualTo` | `Check<unit>` |
| `Check.failIfEqual` | `Check.notEqualTo` | `Check<unit>` |
| `Check.notEqual` | remove | Alias. |

Future comparison helpers should follow the same grammar:

- `greaterThan`
- `greaterThanOrEqualTo`
- `lessThan`
- `lessThanOrEqualTo`
- `positive`
- `nonNegative`
- `negative`
- `nonPositive`

### Strings

| Current | Target | Result shape |
| --- | --- | --- |
| `Check.okIfNonEmptyStr` | `Take.whenNotNullOrEmpty` | `Check<string>` |
| `Check.failIfEmptyStr` | `Take.whenNotNullOrEmpty` | `Check<string>` |
| new | `Check.notNullOrEmpty` | `Check<unit>` |
| `Check.okIfEmptyStr` | `Check.nullOrEmpty` | `Check<unit>` |
| `Check.failIfNonEmptyStr` | `Check.nullOrEmpty` | `Check<unit>` |
| `Check.okIfNotBlank` | `Take.whenNotBlank` | `Check<string>` |
| `Check.failIfBlank` | `Take.whenNotBlank` | `Check<string>` |
| `Check.notBlank` | `Take.whenNotBlank` | `Check<string>` |
| new | `Check.notBlank` | `Check<unit>` |
| `Check.okIfBlank` | `Check.blank` | `Check<unit>` |
| `Check.failIfNotBlank` | `Check.blank` | `Check<unit>` |

Prefer `nullOrEmpty` over `emptyString` because the behavior follows `String.IsNullOrEmpty`.

Prefer `blank` for `String.IsNullOrWhiteSpace`.

## Regular Usage Examples

Predicate-only checks stay in `Check`:

```fsharp
let namePresent : Check<unit> =
    name |> Check.notBlank
```

Value-returning validations move to `Take`:

```fsharp
let usableName : Check<string> =
    name |> Take.whenNotBlank

let maybeUserStillWrapped : Check<User option> =
    maybeUser |> Take.whenSome

let user : Check<User> =
    maybeUser |> Take.some

let singletonIds : Result<OrderId list, CardinalityFailure> =
    ids |> Take.whenExactlyOne

let onlyId : Result<OrderId, CardinalityFailure> =
    ids |> Take.exactlyOne
```

Pure domain errors stay explicit:

```fsharp
let nameResult : Result<string, SignUpError> =
    name |> Take.whenNotBlank |> Check.withError NameRequired

let primaryId : Result<OrderId, OrderError> =
    ids |> Take.exactlyOne |> Result.mapError InvalidPrimaryId
```

`Take` helpers that return `Check<'value>` use `Check.withError` because `withError` is the unit-error carrier bridge, not a predicate constructor. If that call shape reads wrong in implementation examples, rename the bridge globally instead of adding module-specific duplicates.

Flow bind-site errors use `BindError`:

```fsharp
flow {
    let! user =
        tryGetUser username
        |> BindError.withError InvalidUser

    do!
        isPwdValid password user
        |> Check.isTrue
        |> BindError.withError InvalidPwd

    do!
        authorize user
        |> BindError.map Unauthorized

    return!
        createAuthToken user
        |> BindError.map TokenErr
}
```

`BindError.map` avoids the old subflow ceremony:

```fsharp
do!
    authorize user
    |> BindError.map Unauthorized
```

instead of:

```fsharp
do!
    flow { return! authorize user }
    |> Flow.mapError Unauthorized
```

## BindError Implementation Shape

Avoid adding source-specific overloads directly to `FlowBuilder`.

Use a private marker that wraps the adapted `Flow`:

```fsharp
type BindError<'env, 'error, 'value> =
    private BindError of Flow<'env, 'error, 'value>
```

`FlowBuilder` then needs only marker overloads:

```fsharp
member _.ReturnFrom(source: BindError<'env, 'error, 'value>) : Flow<'env, 'error, 'value>

member _.Bind
    (
        source: BindError<'env, 'error, 'value>,
        binder: 'value -> Flow<'env, 'error, 'next>
    ) : Flow<'env, 'error, 'next>
```

The source-specific conversion lives in `BindError.withError` and `BindError.map`, probably through a small inline SRTP dispatcher backed by private tupled static members.

This keeps core `Flow` execution unchanged and prevents the builder from accumulating dozens of bind overloads.

## BindError Source Scope

`BindError.withError` should support source shapes that fail with missingness, falsehood, or `unit`:

- `bool`
- `option<'value>`
- `voption<'value>`
- `Result<'value, unit>`
- `Flow<'env, unit, 'value>`
- `Async<bool>`
- `Async<option<'value>>`
- `Async<voption<'value>>`
- `Async<Result<'value, unit>>`
- .NET `Task` / `ValueTask` equivalents where supported

`BindError.map` should support sources that already carry a meaningful error:

- `Result<'value, 'error>`
- `Flow<'env, 'error, 'value>`
- `Async<Result<'value, 'error>>`
- .NET `Task<Result<'value, 'error>>` / `ValueTask<Result<'value, 'error>>` where supported

Do not make `BindError` the validation accumulation adapter. Use `Validation.mapError` inside validation code.

## Missing Validation Families

Add future families only when they follow the same grammar.

### Inclusion And Exclusion

- `Check.contains`
- `Check.oneOf`
- `Check.inRange`
- `Check.matchesAny`
- `Check.matchesNone`

### Length And Cardinality

- `Check.hasCount`
- `Check.exactlyOne`
- `Check.atMostOne`
- `Check.atLeastOne`
- `Check.moreThanOne`
- `Take.whenExactlyOne`
- `Take.whenAtMostOne`
- `Take.exactlyOne`
- `Take.atMostOne`

### Format And Pattern

- `Check.matches`
- `Check.matchesRegex`
- `Check.email`
- `Check.url`

### Uniqueness And Duplication

- `Check.unique`
- `Check.distinct`
- `Check.hasDuplicates`

### Confirmation And Pairing

- `Check.matchesPair`
- `Check.confirmedBy`

Do not reuse `matches` for unrelated pairwise confirmation. Reserve bare `matches` for pattern matching.

### Collection Quantifiers

- `Check.all`
- `Check.any`

Add `Check.noneOf` only if `Check.negate (Check.any checks)` proves noisy in real examples.

## Rejected Shapes

Do not add `Ok` and `Fail` modules. They encode the `Result` branch instead of the property being checked.

Do not keep `okIf*` and `failIf*` as aliases. They are regular locally, but they make future names longer and less discoverable.

Do not make `require*` or `ensure*` the main grammar. They imply error attachment, which belongs to `Check.withError`, `Result.mapError`, `Validation.mapError`, `Flow.mapError`, or `BindError`.

Do not keep `Guard` as the module name for value-returning checks. It carries the old broad adapter meaning.

Do not use `Gate`, `Admit`, or `Allow` as separate modules. They are close enough in meaning that they make the surface harder to guess. Use one `Take` module and distinguish preservation from extraction with `when`.

Do not use an overloaded `Guard.Of` as the golden path. It makes the call site shorter by making the source interpretation implicit.

Do not use `Failure.map` for the bind marker. It sounds like an immediate carrier-preserving map, but the marker only becomes meaningful when `flow { }` binds it.

Do not make `Flow.from |> Flow.mapError` the primary bind-site story. It is honest, but it breaks the fluency that direct `flow { }` binding is meant to preserve.

Do not add both `Admit.exactlyOne` and `Take.exactlyOne`. That asks users and LLMs to learn a subtle module distinction. Prefer `Take.whenExactlyOne` for preservation and `Take.exactlyOne` for extraction.

Do not add `Take.withError`. It would duplicate the same unit-error carrier bridge. If examples prove `Check.withError` is still too awkward after the `Take` split, rename the bridge globally rather than adding aliases.

## LLM Guessability Rules

The first token after `Check.` should tell an LLM that the result is a unit-success predicate.

The first token after `Take.` should tell an LLM which success shape to expect:

- `whenX` means keep the original source when property `x` holds
- bare `x` means extract or narrow the value exposed by property `x`

The first token after `BindError.` should tell an LLM that the source is being prepared for `flow { }` binding:

- `withError` means assign an error to missingness, falsehood, or `unit` failure
- `map` means map an existing error before binding

Argument order should stay data-last for curried helpers:

```fsharp
items |> Check.contains expected
items |> Check.hasCount 3
actual |> Check.equalTo expected
items |> Take.whenExactlyOne
items |> Take.exactlyOne
source |> BindError.withError DomainError
source |> BindError.map ErrorCase
```

Docs should present predicate, preserving, and extracting forms together:

| Predicate | Preserve original | Extract/narrow |
| --- | --- | --- |
| `Check.some` | `Take.whenSome` | `Take.some` |
| `Check.hasValue` | `Take.whenHasValue` | `Take.hasValue` |
| `Check.notNull` | `Take.whenNotNull` | none |
| `Check.notEmpty` | `Take.whenNotEmpty` | none |
| `Check.notBlank` | `Take.whenNotBlank` | none |
| `Check.exactlyOne` | `Take.whenExactlyOne` | `Take.exactlyOne` |

Docs should present pure and flow bind-site error adaptation separately:

| Context | Use |
| --- | --- |
| pure unit-error check | `check |> Check.withError DomainError` |
| pure existing error | `result |> Result.mapError ErrorCase` |
| flow bind, missing/unit failure | `source |> BindError.withError DomainError` |
| flow bind, existing error | `source |> BindError.map ErrorCase` |

## Migration

Because FsFlow is pre-1.0, remove old names in the implementation pass instead of adding aliases.

The implementation pass must update:

- source comments in `src/FsFlow/Check.fs`
- new value-returning validation source in `src/FsFlow/Take.fs`
- new bind marker source in `src/FsFlow/BindError.fs`
- flow builder marker overloads in `src/FsFlow/FlowBuilder.fs`
- tests in `tests/FsFlow.Tests/ValidationTests.fs`
- tests in `tests/FsFlow.Tests/WorkflowErrorTests.fs`
- user-facing guides in `README.md`, `docs/`, and `llms.txt`
- generated reference docs

Do not hand-edit generated reference pages as the primary fix.

## Consequences

The target API is stricter than the current surface.

`Check` no longer mixes predicates with extracting conveniences.

`Take` becomes the predictable place to look when success should return a value.

`Take.whenX` keeps the original source value when the property holds.

`Take.x` extracts or narrows the useful value exposed by the property.

`BindError` preserves direct `flow { }` binding fluency when an error needs to be assigned or mapped before the bind.

`Check.withError` remains the direct pure helper for `Check<'value>` outside `flow { }`.

Cardinality takes keep typed failure information because the count is a real diagnostic.

The docs can now teach a repeatable pattern:

```fsharp
name |> Check.notBlank
name |> Take.whenNotBlank |> Check.withError NameRequired
ids |> Take.whenExactlyOne |> Result.mapError InvalidIds
ids |> Take.exactlyOne |> Result.mapError InvalidPrimaryId
maybeUser |> BindError.withError InvalidUser
authorize user |> BindError.map Unauthorized
```

## Acceptance Criteria

The redesign is coherent when:

- every `Check` predicate helper returns `Check<unit>`
- every simple `Take.whenX` helper returns the original source value in a unit-error carrier
- every simple bare `Take.x` helper extracts or narrows the success value in a unit-error carrier
- diagnostic `Take` helpers return typed `Result` values only when the built-in diagnostic is useful
- `Check` and `Take` use the same base property name for the same property where both forms exist
- `BindError.withError` and `BindError.map` are the only golden-path flow bind-site error adapters
- `Guard.Of` and `Guard.MapError` are removed
- old `okIf*` and `failIf*` names are gone
- reserved-word helpers no longer require backticks
- docs show `Check`, `Take.whenX`, and `Take.x` forms together
- docs keep pure error attachment separate from flow bind-site error adaptation
- `llms.txt` uses only the target names
