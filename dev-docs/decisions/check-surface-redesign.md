# Check and BindError Surface Redesign

## Extracted From

- `dev-docs/PLAN.md`
- `dev-docs/TASKS.md`
- `dev-docs/decisions/validation-surface.md`
- `dev-docs/check-surface-redesign-continued.md`
- Current discussion about discoverability, regular naming, bind-site error adaptation, and validation-library prior art

## Source Date

- 2026-06-05: proposed revision of the validation and bind-error naming surface
- 2026-06-07: updated to remove the separate `Take` module and fold value-returning checks into `Check`

## Decision

FsFlow should split validation and bind adaptation into three explicit concepts:

- `Check<'value>` is the unit-error carrier for simple pure checks.
- `Check` is also the full validation helper module. The function prefix chooses the success shape.
- `BindError` is a flow bind-site marker for assigning or mapping errors before binding.
- `Validation<'value, 'error>` is the accumulating diagnostics carrier.

There is no public `Take` module. The old `Take` split made users choose a module before they could discover the property they wanted. The public grammar is now:

- `Check.x` tests property `x` and usually succeeds with `unit`.
- `Check.whenX` tests property `x` and preserves the original input on success.
- `Check.takeX` tests property `x` and extracts an inner value or returns a deliberately different success shape.

Use `when` only for value-preserving gates. Use `take` only when the success value is structurally different. Do not add a `takeX` helper that returns the same success shape as `whenX`.

```fsharp
Check.notBlank       : string -> Check<unit>
Check.whenNotBlank   : string -> Check<string>

Check.isSome         : 'value option -> Check<unit>
Check.whenSome       : 'value option -> Check<'value option>
Check.takeSome       : 'value option -> Check<'value>

Check.isSingle       : seq<'value> -> Result<unit, CardinalityFailure>
Check.whenSingle     : 'collection -> Result<'collection, CardinalityFailure>
Check.takeSingle     : seq<'value> -> Result<'value, CardinalityFailure>
```

Error attachment stays separate:

- use `Check.withError` for pure unit-error `Check<'value>` code outside `flow { }`
- use `Result.mapError`, `Validation.mapError`, or `Flow.mapError` when the source already carries a meaningful error
- use `BindError.withError` or `BindError.map` only at flow bind sites

Remove `Guard.Of`, `Guard.MapError`, the public `Take` module, and old `okIf*` / `failIf*` names before 1.0.

## Why

The old `okIf*` / `failIf*` surface was regular locally, but it encoded the `Result` branch instead of the property being checked.

The old `Take` module made a helpful distinction between preserving and extracting checks, but it split one conceptual DSL across two modules. Users and LLMs should be able to start with `Check.` and then choose:

- an unprefixed predicate when success only means "the property holds"
- a `when*` gate when the next step needs the original input
- a `take*` extraction when the next step needs an inner value or a deliberately different success shape

`BindError` remains separate because it is not a pure validation helper. It is a marker interpreted by `flow { }` at a bind site.

## Target Grammar

Use one primary spelling per concept. Do not keep compatibility aliases before 1.0.

### Predicate Names

Use direct property names for predicates when the property reads naturally:

- `notNull`
- `isNull`
- `notEmpty`
- `empty`
- `notBlank`
- `blank`
- `notNullOrEmpty`
- `nullOrEmpty`
- `notEmptyString`
- `emptyString`
- `equalTo`
- `notEqualTo`
- `contains`
- `hasCount`
- `hasDuplicates`
- `hasNoDuplicates`
- `greaterThan`
- `lessThan`
- `atLeast`
- `atMost`
- `between`
- `positive`
- `nonNegative`
- `negative`
- `nonPositive`

Use `is*` when the concept is a union branch or reserved literal:

- `isTrue`
- `isFalse`
- `isSome`
- `isNone`
- `isValueSome`
- `isValueNone`
- `isOk`
- `isError`
- `isSingle`

Use `has*` for nullable presence:

- `hasValue`
- `hasNoValue`

Reserve `hasValue` and `hasNoValue` for `System.Nullable<'T>`. Use `isSome` and `isNone` for `option<'T>`.

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

### Preserving Names

Use `Check.whenX` when success keeps the original input:

- `whenTrue`
- `whenFalse`
- `whenSome`
- `whenNone`
- `whenValueSome`
- `whenValueNone`
- `whenHasValue`
- `whenHasNoValue`
- `whenNotNull`
- `whenNull`
- `whenOk`
- `whenError`
- `whenNotBlank`
- `whenBlank`
- `whenNotNullOrEmpty`
- `whenNullOrEmpty`
- `whenNotEmptyString`
- `whenEmptyString`
- `whenMinLength`
- `whenMaxLength`
- `whenExactLength`
- `whenMatchesRegex`
- `whenNotEmpty`
- `whenEmpty`
- `whenContains`
- `whenCount`
- `whenHasDuplicates`
- `whenHasNoDuplicates`
- `whenSingle`
- `whenAtMostOne`
- `whenAtLeastOne`
- `whenMoreThanOne`
- `whenEqualTo`
- `whenNotEqualTo`
- `whenGreaterThan`
- `whenLessThan`
- `whenAtLeast`
- `whenAtMost`
- `whenBetween`
- `whenPositive`
- `whenNonNegative`
- `whenNegative`
- `whenNonPositive`

### Extraction Names

Use `Check.takeX` when success extracts an inner value or returns a deliberately different success shape:

- `takeSome`
- `takeValueSome`
- `takeHasValue`
- `takeOk`
- `takeError`
- `takeHead`
- `takeSingle`
- `takeAtMostOne`

Future extraction names should follow the same grammar:

- `takeLast`
- `takeItem`
- `takeValueFor`
- `takeType`

Do not add unprefixed value-returning helpers. A bare helper under `Check` should read as a predicate unless it is a constructor such as `fromPredicate`.

## Error Shapes

Simple predicates and simple `when*` / `take*` helpers return `Check<'success>` and can be assigned a domain error with `Check.withError`.

Helpers that expose useful built-in diagnostics return typed `Result` values:

- string length helpers return `StringLengthFailure`
- comparison and numeric helpers return `RangeFailure<'value>`
- cardinality helpers return `CardinalityFailure`

Use `Result.mapError` to map those diagnostics to an application error.

## Surface Mapping

| Old or retired shape | Target |
| --- | --- |
| `Check.okIf` | `Check.isTrue` |
| `Check.failIf` | `Check.isFalse` |
| `Check.okIfSome` / `Check.failIfNone` | `Check.takeSome` |
| `Check.okIfNone` / `Check.failIfSome` | `Check.isNone` |
| `Check.okIfValueSome` / `Check.failIfValueNone` | `Check.takeValueSome` |
| `Check.okIfValueNone` / `Check.failIfValueSome` | `Check.isValueNone` |
| `Check.okIfNotNullable` / `Check.failIfNullable` | `Check.takeHasValue` |
| `Check.okIfNullable` / `Check.failIfNotNullable` | `Check.hasNoValue` |
| `Check.okIfNotNull` / `Check.failIfNull` | `Check.whenNotNull` |
| `Check.okIfNull` / `Check.failIfNotNull` | `Check.isNull` |
| `Check.okIfNotEmpty` / `Check.failIfEmpty` | `Check.whenNotEmpty` |
| `Check.okIfEmpty` / `Check.failIfNotEmpty` | `Check.empty` |
| `Check.okIfExactlyOne` | `Check.takeSingle` |
| `Check.okIfAtMostOne` | `Check.takeAtMostOne` |
| `Check.okIfNonEmptyStr` / `Check.failIfEmptyStr` | `Check.whenNotNullOrEmpty` |
| `Check.okIfNotBlank` / `Check.failIfBlank` | `Check.whenNotBlank` |
| `Take.whenX` | `Check.whenX` |
| `Take.some` | `Check.takeSome` |
| `Take.valueSome` | `Check.takeValueSome` |
| `Take.hasValue` | `Check.takeHasValue` |
| `Take.exactlyOne` | `Check.takeSingle` |
| `Take.atMostOne` | `Check.takeAtMostOne` |
| `Guard.Of(error, source)` | `source |> BindError.withError error` at a `flow { }` bind site |
| `Guard.MapError(mapper, source)` | `source |> BindError.map mapper` at a `flow { }` bind site |

## Regular Usage Examples

Predicate-only checks:

```fsharp
let namePresent : Check<unit> =
    name |> Check.notBlank

let onePrimaryId : Result<unit, CardinalityFailure> =
    ids |> Check.isSingle
```

Value-preserving gates:

```fsharp
let usableName : Check<string> =
    name |> Check.whenNotBlank

let maybeUserStillWrapped : Check<User option> =
    maybeUser |> Check.whenSome

let singletonIds : Result<OrderId list, CardinalityFailure> =
    ids |> Check.whenSingle
```

Value extraction:

```fsharp
let user : Check<User> =
    maybeUser |> Check.takeSome

let onlyId : Result<OrderId, CardinalityFailure> =
    ids |> Check.takeSingle
```

Pure domain errors stay explicit:

```fsharp
let nameResult : Result<string, SignUpError> =
    name |> Check.whenNotBlank |> Check.withError NameRequired

let primaryId : Result<OrderId, OrderError> =
    ids |> Check.takeSingle |> Result.mapError InvalidPrimaryId
```

Flow bind-site errors use `BindError`. Boolean predicates must be converted to a unit-error result before assignment:

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
}
```

Docs should present predicate, preserving, and extracting forms together:

| Predicate | Preserve original | Extract/narrow |
| --- | --- | --- |
| `Check.isSome` | `Check.whenSome` | `Check.takeSome` |
| `Check.hasValue` | `Check.whenHasValue` | `Check.takeHasValue` |
| `Check.notNull` | `Check.whenNotNull` | none |
| `Check.notEmpty` | `Check.whenNotEmpty` | `Check.takeHead` where a first item is needed |
| `Check.notBlank` | `Check.whenNotBlank` | none |
| `Check.isSingle` | `Check.whenSingle` | `Check.takeSingle` |

Docs should present pure and flow bind-site error adaptation separately:

| Context | Use |
| --- | --- |
| pure unit-error check | `check |> Check.withError DomainError` |
| pure existing error | `result |> Result.mapError ErrorCase` |
| flow bind, option/value-option absence or unit-error failure | `source |> BindError.withError DomainError` |
| flow bind, existing error | `source |> BindError.map ErrorCase` |

## Migration

Because FsFlow is pre-1.0, remove old names in the implementation pass instead of adding aliases.

The implementation pass must update:

- source comments in `src/FsFlow/Check.fs`
- remove `src/FsFlow/Take.fs` from the project
- bind marker source in `src/FsFlow/BindError.fs` if examples mention the retired surface
- flow and validation builder source comments
- tests in `tests/FsFlow.Tests/ValidationTests.fs`
- tests in `tests/FsFlow.Tests/WorkflowErrorTests.fs` if they mention retired names
- user-facing guides in `README.md`, `docs/`, and `llms.txt`
- generated reference docs

Do not hand-edit generated reference pages as the primary fix.

## Consequences

The target API is stricter than the current surface.

`Check` contains the full pure validation DSL.

Unprefixed helper names remain predicate-shaped.

`Check.whenX` keeps the original source value when the property holds.

`Check.takeX` extracts an inner value or returns a deliberately different success shape exposed by the property.

`BindError` preserves direct `flow { }` binding fluency when an error needs to be assigned or mapped.

`Check.withError` remains the direct pure helper for unit-error checks outside `flow { }`.

Typed diagnostic helpers keep their diagnostic error rather than forcing callers through `unit`.

## Acceptance Criteria

The redesign is coherent when:

- no public `FsFlow.Take` module remains
- simple unprefixed `Check` predicate helpers return `Check<unit>`
- diagnostic predicate helpers return `Result<unit, StringLengthFailure>`, `Result<unit, RangeFailure<_>>`, or `Result<unit, CardinalityFailure>`
- simple `Check.whenX` helpers preserve the original source value in a unit-error carrier
- diagnostic `Check.whenX` helpers preserve the original source value while keeping their diagnostic error
- simple `Check.takeX` helpers extract or reshape the success value in a unit-error carrier
- diagnostic `Check.takeX` helpers extract or reshape the success value while keeping their diagnostic error
- `BindError.withError` and `BindError.map` are the only golden-path flow bind-site error adapters
- `Guard.Of` and `Guard.MapError` are removed
- old `okIf*`, `failIf*`, and `Take.*` names are gone
- reserved-word helpers no longer require backticks
- docs show `Check.x`, `Check.whenX`, and `Check.takeX` forms together
- docs keep pure error attachment separate from flow bind-site error adaptation
- `llms.txt` uses only the target names
