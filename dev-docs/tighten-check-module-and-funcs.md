# Tighten `Check` Module And Function Names

## Context

`Check<'value>` is moving from a loose group of boolean predicates toward a complete typed value-constraint subsystem:

```fsharp
type Check<'value> = 'value -> Result<unit, CheckFailure list>
```

That direction is right, but the current call sites are too verbose for everyday validation code:

```fsharp
Check.Collection.countBetween 2 4 [ 1; 2; 3; 4; 5 ]
Check.Collection.distinct [ 1; 2; 3 ]
Check.String.present "Ada"
```

The nested modules are useful as implementation and documentation structure, but making users spell the type bucket on every common check creates a lot of ceremony compared with idiomatic .NET/F# collection and string APIs.

The desired direction is:

- `Check.*` should primarily mean structured `Result` checks.
- Type-specific submodules should remain available as direct, explicit implementations.
- Top-level `Check.*` should offer the ergonomic surface.
- Use SRTP only as a thin facade where one check concept naturally applies to multiple unrelated value shapes.
- Add boolean predicates outside the structured `Check.*` surface, preferably as extensions or type-specific predicate modules, so bool checks remain easy without confusing them with result checks.

## Goals

1. Make common structured checks short:

   ```fsharp
   Check.present value
   Check.notEmpty value
   Check.distinct values
   Check.countBetween 2 4 values
   Check.lengthBetween 2 40 text
   Check.between 1 10 number
   ```

2. Keep direct type-specific implementations:

   ```fsharp
   Check.String.present text
   Check.Seq.noDuplicates values
   Check.Option.some maybeValue
   Check.ValueOption.some maybeValue
   Check.Nullable.hasValue nullableValue
   Check.Result.ok result
   ```

3. Keep the public meaning clear:

   - `Check.*` returns `Result<unit, CheckFailure list>`.
   - `Seq.isDistinct`, `String.isBlank`, etc. return `bool`.
   - Avoid `Seq.distinct` as a check name because FSharp.Core already uses it for transformation.

4. Avoid making SRTP the semantic foundation. SRTP should only dispatch to explicit functions.

5. Preserve AOT/trimming safety. Do not use runtime reflection for dispatch.

## Non-Goals

- Do not make all checks generic.
- Do not replace the nested modules with SRTP.
- Do not shadow core transform names such as `Seq.distinct`.
- Do not put path, raw-input, or schema metadata into `CheckFailure`.
- Do not turn `Check` into `Validation` or schema. `Check` remains path-free and raw-input-free.

## Proposed Public Model

The public `Check` module should have two layers.

### Layer 1: Direct Type Modules

These are the stable, explicit, boring implementations:

```fsharp
Check.String.*
Check.Number.*
Check.Seq.*
Check.Option.*
Check.ValueOption.*
Check.Nullable.*
Check.Result.*
```

The current `Check.Collection` module should be renamed or aliased to `Check.Seq`.

Reasoning:

- The functions accept `#seq<'value>`, not arbitrary collection-specific interfaces.
- `Seq` is the common F# vocabulary for sequence-shaped values.
- The module name does not imply mutation, indexing, dictionary semantics, or collection construction.
- Use `Check.Seq.noDuplicates` for the direct sequence check so no `Seq.distinct` member competes with FSharp.Core's
  sequence transformation name.

`Check.Collection` can remain as a temporary compatibility alias while pre-1.0 cleanup is happening, but because Axial is pre-1.0, the cleaner move is to replace it outright if the other agent's branch has not committed public docs yet.

### Layer 2: Top-Level Convenience Facade

Top-level `Check.*` functions should be short, common, and result-returning.

Some should be concrete:

```fsharp
Check.distinct      : #seq<'a> -> Result<unit, CheckFailure list>
Check.countBetween  : int -> int -> #seq<'a> -> Result<unit, CheckFailure list>
Check.minCount      : int -> #seq<'a> -> Result<unit, CheckFailure list>
Check.maxCount      : int -> #seq<'a> -> Result<unit, CheckFailure list>
Check.lengthBetween : int -> int -> string -> Result<unit, CheckFailure list>
Check.minLength     : int -> string -> Result<unit, CheckFailure list>
Check.maxLength     : int -> string -> Result<unit, CheckFailure list>
Check.email         : string -> Result<unit, CheckFailure list>
Check.matches       : string -> string -> Result<unit, CheckFailure list>
Check.oneOf         : string seq -> string -> Result<unit, CheckFailure list>
Check.between       : 'a -> 'a -> 'a -> Result<unit, CheckFailure list>
Check.greaterThan   : 'a -> 'a -> Result<unit, CheckFailure list>
Check.lessThan      : 'a -> 'a -> Result<unit, CheckFailure list>
Check.atLeast       : 'a -> 'a -> Result<unit, CheckFailure list>
Check.atMost        : 'a -> 'a -> Result<unit, CheckFailure list>
```

Some may be SRTP facade functions:

```fsharp
Check.present  : ^value -> Result<unit, CheckFailure list>
Check.empty    : ^value -> Result<unit, CheckFailure list>
Check.notEmpty : ^value -> Result<unit, CheckFailure list>
```

The SRTP set should stay very small. These names are worth generic dispatch because they are common across strings, options, value options, nullable values, and sequences, and users reasonably expect the same word to work across those shapes.

## SRTP Policy

Use SRTP only where all of these are true:

1. The concept has the same user-facing meaning across multiple unrelated types.
2. A non-SRTP top-level function would force ugly names or artificial type buckets.
3. The direct type-specific implementation already exists.
4. The SRTP function only delegates to direct implementations.
5. The expected call sites are common enough to justify the extra compiler complexity.

Initial approved SRTP facade:

```fsharp
Check.present
Check.empty
Check.notEmpty
```

Potential later SRTP facade, only if the initial shape feels good:

```fsharp
Check.some
Check.none
```

Do not start with SRTP for:

```fsharp
Check.distinct
Check.countBetween
Check.minCount
Check.maxCount
Check.lengthBetween
Check.minLength
Check.maxLength
Check.email
Check.matches
Check.oneOf
Check.between
Check.greaterThan
Check.lessThan
Check.atLeast
Check.atMost
Check.ok
Check.error
```

Reasoning:

- `distinct`, count checks, and sequence checks have one natural target: `seq<'a>`.
- Length/email/regex/string choice checks have one natural target: `string`.
- Numeric/range checks can already be inline generic functions without SRTP overload dispatch.
- `ok` and `error` are result-shape checks and should stay explicit to avoid confusion with constructors.

## Naming Decisions

### Use `present` For Required-Value Checks

`present` means "the value has meaningful presence for its type."

Direct implementations:

```fsharp
Check.String.present      : string -> Result<unit, CheckFailure list>
Check.Option.present      : 'a option -> Result<unit, CheckFailure list>
Check.ValueOption.present : 'a voption -> Result<unit, CheckFailure list>
Check.Nullable.present    : Nullable<'a> -> Result<unit, CheckFailure list>
```

Top-level facade:

```fsharp
Check.present value
```

String behavior:

- `null` -> `Error [ Missing ]`
- `""` -> `Error [ Blank ]`
- whitespace -> `Error [ Blank ]`
- otherwise -> `Ok ()`

Option behavior:

- `None` -> `Error [ Missing ]`
- `Some _` -> `Ok ()`

Value option behavior:

- `ValueNone` -> `Error [ Missing ]`
- `ValueSome _` -> `Ok ()`

Nullable behavior:

- no value -> `Error [ Missing ]`
- has value -> `Ok ()`

Do not use `required` for this layer yet. `required` is likely schema/input terminology, where missing raw fields and blank scalars may have path-aware behavior. `present` is a value-level check.

### Use `empty` And `notEmpty`

`empty` and `notEmpty` should be structured checks, not bool predicates, when under `Check`.

Direct implementations:

```fsharp
Check.String.empty
Check.String.notEmpty
Check.Seq.empty
Check.Seq.notEmpty
Check.Option.empty
Check.Option.notEmpty
Check.ValueOption.empty
Check.ValueOption.notEmpty
Check.Nullable.empty
Check.Nullable.notEmpty
```

Top-level facade:

```fsharp
Check.empty value
Check.notEmpty value
```

String semantics:

- `Check.String.empty null` -> `Error [ Missing ]`
- `Check.String.empty ""` -> `Ok ()`
- `Check.String.empty " "` -> failure unless a separate `blank` check is used.
- `Check.String.notEmpty null` -> `Error [ Missing ]`
- `Check.String.notEmpty ""` -> `Error [ Length(MinimumLength 1, Some 0) ]`

Recommendation:

- Use `present` for non-null, non-empty, non-whitespace strings.
- Use `notEmpty` for non-null and length greater than zero.
- Use `blank` or `notBlank` only in bool predicate space unless a structured blank check is explicitly needed.

Sequence semantics:

- `Check.Seq.empty null` -> `Error [ Count(ExactCount 0, None) ]`
- `Check.Seq.notEmpty null` -> `Error [ Count(MinimumCount 1, None) ]`.
- Sequence count checks treat null as an unknown observed count, not as `Missing`. Keep the current `notEmpty`
  behavior so sequence-shaped checks consistently report `Count(..., None)` for null values.

Option/value option/nullable semantics:

- `empty` means `None`, `ValueNone`, or no nullable value.
- `notEmpty` should alias `present`.

### Use `distinct` For Structured Sequence Checks

Use:

```fsharp
Check.distinct values
Check.Seq.noDuplicates values
```

Do not use:

```fsharp
Seq.distinct values // already means transform sequence
Seq.requireDistinct values
Seq.checkDistinct values
```

Reasoning:

- Under `Check`, `distinct` clearly returns a structured check result.
- Under `Seq`, `distinct` already has a transformation meaning.
- `requireDistinct` and `checkDistinct` duplicate the domain word inside one naming family.

Structured failure:

```fsharp
Error [ CustomCode "seq.distinct" ]
```

Prefer `"seq.distinct"` over `"collection.distinct"` if the module becomes `Check.Seq`.

Null behavior:

```fsharp
Check.distinct nullValues = Error [ Missing ]
```

### Use `count*` For Structured Sequence Count Checks

Use:

```fsharp
Check.count 3 values
Check.minCount 2 values
Check.maxCount 4 values
Check.countBetween 2 4 values
```

Direct:

```fsharp
Check.Seq.count
Check.Seq.minCount
Check.Seq.maxCount
Check.Seq.countBetween
```

Current code has `minCount`, `maxCount`, and `countBetween`, but not exact count as an executable check. Add:

```fsharp
Check.Seq.count : int -> Check<#seq<'value>>
Check.count     : int -> Check<#seq<'value>>
```

Failure mapping:

```fsharp
Check.count 3 [ 1; 2 ] = Error [ Count(ExactCount 3, Some 2) ]
Check.minCount 3 [ 1; 2 ] = Error [ Count(MinimumCount 3, Some 2) ]
Check.maxCount 3 [ 1; 2; 3; 4 ] = Error [ Count(MaximumCount 3, Some 4) ]
Check.countBetween 2 4 [ 1 ] = Error [ Count(CountBetween(2, 4), Some 1) ]
```

### Use `length*` For Structured String Length Checks

Use:

```fsharp
Check.length 3 text
Check.minLength 2 text
Check.maxLength 40 text
Check.lengthBetween 2 40 text
```

Direct:

```fsharp
Check.String.length
Check.String.minLength
Check.String.maxLength
Check.String.lengthBetween
```

Current code has `minLength`, `maxLength`, and `lengthBetween`, but not exact length as an executable check. Add:

```fsharp
Check.String.length : int -> Check<string>
Check.length        : int -> Check<string>
```

Failure mapping:

```fsharp
Check.length 3 "ab" = Error [ Length(ExactLength 3, Some 2) ]
Check.minLength 3 "ab" = Error [ Length(MinimumLength 3, Some 2) ]
Check.maxLength 3 "abcd" = Error [ Length(MaximumLength 3, Some 4) ]
Check.lengthBetween 2 4 "abcde" = Error [ Length(LengthBetween(2, 4), Some 5) ]
```

### Keep Numeric Checks Flat And Inline

Use:

```fsharp
Check.between 1 10 value
Check.greaterThan 0 value
Check.lessThan 100 value
Check.atLeast 1 value
Check.atMost 10 value
```

Direct:

```fsharp
Check.Number.between
Check.Number.greaterThan
Check.Number.lessThan
Check.Number.atLeast
Check.Number.atMost
```

These can remain inline generic comparison functions. They do not need SRTP overload dispatch.

Also add executable numeric sign checks, mirroring existing bool predicates:

```fsharp
Check.positive
Check.nonNegative
Check.negative
Check.nonPositive
Check.Number.positive
Check.Number.nonNegative
Check.Number.negative
Check.Number.nonPositive
```

Failure mapping recommendation:

```fsharp
Check.positive 0 = Error [ Range(GreaterThan "0", Some "0") ]
Check.nonNegative -1 = Error [ Range(AtLeast "0", Some "-1") ]
Check.negative 0 = Error [ Range(LessThan "0", Some "0") ]
Check.nonPositive 1 = Error [ Range(AtMost "0", Some "1") ]
```

### Keep String Format Checks Flat

Use:

```fsharp
Check.email text
Check.matches pattern text
Check.oneOf choices text
```

Direct:

```fsharp
Check.String.email
Check.String.matches
Check.String.oneOf
```

Add executable alphanumeric/numeric text checks if they remain useful:

```fsharp
Check.String.numeric
Check.String.alphaNumeric
Check.numeric
Check.alphaNumeric
```

Avoid `isNumeric` and `isAlphaNumeric` under `Check` because `is*` implies bool.

### Option And Result Names

Direct option checks should use both old explicit names and presence vocabulary:

```fsharp
Check.Option.some
Check.Option.none
Check.Option.present
Check.Option.empty

Check.ValueOption.some
Check.ValueOption.none
Check.ValueOption.present
Check.ValueOption.empty

Check.Nullable.hasValue
Check.Nullable.hasNoValue
Check.Nullable.present
Check.Nullable.empty
```

Top-level SRTP candidate:

```fsharp
Check.present maybe
Check.empty maybe
```

Do not add top-level `Check.some` and `Check.none` initially unless there is clear demand. They are less universal and can conflict mentally with `Option` constructors.

Direct result checks:

```fsharp
Check.Result.ok
Check.Result.error
```

Top-level result checks are optional:

```fsharp
Check.ok
Check.error
```

Recommendation: do not add top-level `Check.ok` and `Check.error` in the first tightening pass. They read too much like constructors and do not solve the current verbosity pain.

## Boolean Predicate Surface

Move or mirror boolean predicates outside the structured `Check.*` surface.

Current top-level bool predicates include:

```fsharp
Check.isSome
Check.isNone
Check.isValueSome
Check.isValueNone
Check.hasValue
Check.hasNoValue
Check.notNull
Check.isNull
Check.isOk
Check.isError
Check.notEmpty
Check.isEmpty
Check.notNullOrEmpty
Check.nullOrEmpty
Check.notEmptyString
Check.emptyString
Check.notBlank
Check.blank
Check.hasMinLength
Check.hasMaxLength
Check.hasExactLength
Check.matchesRegex
Check.isEmail
Check.isNumeric
Check.isAlphaNumeric
Check.equalTo
Check.notEqualTo
Check.contains
Check.hasCount
Check.isSingle
Check.atMostOne
Check.atLeastOne
Check.moreThanOne
Check.hasDuplicates
Check.hasNoDuplicates
Check.greaterThan
Check.lessThan
Check.atLeast
Check.atMost
Check.between
Check.positive
Check.nonNegative
Check.negative
Check.nonPositive
Check.negate
```

Those names cannot remain as top-level `Check.*` bool predicates if top-level `Check.*` is becoming structured checks.

Recommended destinations:

### `Option` Bool Predicates

Use FSharp.Core where possible:

```fsharp
Option.isSome
Option.isNone
```

If Axial adds extension predicates:

```fsharp
maybe.IsPresent()
maybe.IsEmpty()
```

### `ValueOption` Bool Predicates

Potential module or extensions:

```fsharp
ValueOption.isSome
ValueOption.isNone
valueOption.IsPresent()
valueOption.IsEmpty()
```

### Nullable Bool Predicates

Prefer existing property:

```fsharp
nullable.HasValue
```

Optional extension:

```fsharp
nullable.IsEmpty()
nullable.IsPresent()
```

### Reference Bool Predicates

Use:

```fsharp
Object.isNull value
not (Object.isNull value)
```

or extension helpers if desired.

Do not keep these as prominent `Check.*` bool names.

### Result Bool Predicates

Potential module:

```fsharp
Result.isOk
Result.isError
```

or extensions:

```fsharp
result.IsOk
result.IsError
```

### String Bool Predicates

Potential module:

```fsharp
String.isEmpty
String.isNotEmpty
String.isBlank
String.isNotBlank
String.hasMinLength
String.hasMaxLength
String.hasLength
String.matches
String.isEmail
String.isNumeric
String.isAlphaNumeric
```

Potential extensions:

```fsharp
text.IsBlank()
text.IsPresent()
text.HasMinLength(3)
text.HasMaxLength(40)
text.HasLength(12)
text.IsEmail()
text.IsNumeric()
text.IsAlphaNumeric()
```

### Sequence Bool Predicates

Do not add `Seq.distinct` as a bool predicate.

Use:

```fsharp
Seq.isEmpty
Seq.isNotEmpty
Seq.contains expected values
Seq.hasCount expected values
Seq.isSingle values
Seq.atMostOne values
Seq.atLeastOne values
Seq.moreThanOne values
Seq.hasDuplicates values
Seq.isDistinct values
```

Optional extensions:

```fsharp
values.IsEmpty()
values.IsNotEmpty()
values.HasCount(3)
values.HasDuplicates()
values.IsDistinct()
```

`Seq.isDistinct` is acceptable for bool because it does not collide with `Seq.distinct` and the `is*` prefix signals a predicate.

### Comparison Bool Predicates

Potential module:

```fsharp
Compare.greaterThan minimum value
Compare.lessThan maximum value
Compare.atLeast minimum value
Compare.atMost maximum value
Compare.between minimum maximum value
Compare.positive value
Compare.nonNegative value
Compare.negative value
Compare.nonPositive value
```

But this can likely wait. The structured checks are the current design pressure.

## Method-By-Method Proposal

### Composition

Keep top-level:

```fsharp
Check.all
Check.any
Check.not
Check.mapFailure
```

These already operate on `Check<'value>` and belong at top level.

Consider renaming:

```fsharp
Check.not
```

to:

```fsharp
Check.notSatisfied
Check.negate
```

But only if the backtick usage is annoying in practice:

```fsharp
Check.``not`` check
```

`Check.not` currently returns `CustomCode "check.not"` when the inner check succeeds. That is reasonable for a generic inversion combinator, but document that users should prefer a specific positive check where a meaningful failure exists.

### String Checks

Keep direct:

```fsharp
Check.String.present
Check.String.minLength
Check.String.maxLength
Check.String.lengthBetween
Check.String.email
Check.String.matches
Check.String.oneOf
```

Add direct:

```fsharp
Check.String.empty
Check.String.notEmpty
Check.String.length
Check.String.numeric
Check.String.alphaNumeric
```

Top-level concrete:

```fsharp
Check.length
Check.minLength
Check.maxLength
Check.lengthBetween
Check.email
Check.matches
Check.oneOf
Check.numeric
Check.alphaNumeric
```

Top-level SRTP:

```fsharp
Check.present
Check.empty
Check.notEmpty
```

### Number Checks

Keep direct:

```fsharp
Check.Number.between
Check.Number.greaterThan
Check.Number.lessThan
Check.Number.atLeast
Check.Number.atMost
```

Add direct:

```fsharp
Check.Number.positive
Check.Number.nonNegative
Check.Number.negative
Check.Number.nonPositive
```

Top-level concrete inline:

```fsharp
Check.between
Check.greaterThan
Check.lessThan
Check.atLeast
Check.atMost
Check.positive
Check.nonNegative
Check.negative
Check.nonPositive
```

### Sequence Checks

Rename direct module:

```fsharp
Check.Collection
```

to:

```fsharp
Check.Seq
```

Keep/add direct:

```fsharp
Check.Seq.empty
Check.Seq.notEmpty
Check.Seq.count
Check.Seq.minCount
Check.Seq.maxCount
Check.Seq.countBetween
Check.Seq.noDuplicates
Check.Seq.contains
Check.Seq.single
Check.Seq.atMostOne
Check.Seq.atLeastOne
Check.Seq.moreThanOne
```

Top-level concrete:

```fsharp
Check.count
Check.minCount
Check.maxCount
Check.countBetween
Check.distinct
Check.contains
Check.single
Check.atMostOne
Check.atLeastOne
Check.moreThanOne
```

Top-level SRTP:

```fsharp
Check.empty
Check.notEmpty
```

### Option Checks

Keep direct:

```fsharp
Check.Option.some
Check.Option.none
```

Add direct aliases:

```fsharp
Check.Option.present = Check.Option.some
Check.Option.empty = Check.Option.none
Check.Option.notEmpty = Check.Option.some
```

Top-level SRTP:

```fsharp
Check.present
Check.empty
Check.notEmpty
```

Do not add top-level `Check.some`/`Check.none` in the first pass.

### Value Option Checks

Keep direct:

```fsharp
Check.ValueOption.some
Check.ValueOption.none
```

Add direct aliases:

```fsharp
Check.ValueOption.present = Check.ValueOption.some
Check.ValueOption.empty = Check.ValueOption.none
Check.ValueOption.notEmpty = Check.ValueOption.some
```

Top-level SRTP:

```fsharp
Check.present
Check.empty
Check.notEmpty
```

### Nullable Checks

Keep direct:

```fsharp
Check.Nullable.hasValue
Check.Nullable.hasNoValue
```

Add direct aliases:

```fsharp
Check.Nullable.present = Check.Nullable.hasValue
Check.Nullable.empty = Check.Nullable.hasNoValue
Check.Nullable.notEmpty = Check.Nullable.hasValue
```

Top-level SRTP:

```fsharp
Check.present
Check.empty
Check.notEmpty
```

### Result Checks

Keep direct:

```fsharp
Check.Result.ok
Check.Result.error
```

Do not add top-level aliases initially.

If later added, names should be:

```fsharp
Check.ok
Check.error
```

but this is lower priority and may confuse construction with checking.

### Equality Checks

Current bool predicates:

```fsharp
Check.equalTo
Check.notEqualTo
```

Add structured checks:

```fsharp
Check.equalTo expected actual
Check.notEqualTo expected actual
```

Failure mapping:

```fsharp
Check.equalTo expected actual
// Error [ Equality(EqualTo(string expected), Some(string actual)) ]

Check.notEqualTo unexpected actual
// Error [ Equality(NotEqualTo(string unexpected), Some(string actual)) ]
```

This creates a conflict with current bool names, which is another reason top-level bool predicates need to move.

## Example Target Call Sites

Simple checks:

```fsharp
Check.present "Ada"
Check.email "ada@example.com"
Check.lengthBetween 2 40 "Ada"
Check.between 1 10 5
Check.distinct [ 1; 2; 3 ]
Check.countBetween 2 4 [ 1; 2; 3 ]
```

Explicit fallback:

```fsharp
Check.String.present "Ada"
Check.Seq.noDuplicates [ 1; 2; 3 ]
Check.Option.some (Some 1)
Check.Nullable.hasValue (Nullable 1)
Check.Result.ok (Ok 1)
```

Composition:

```fsharp
let username =
    Check.all [
        Check.present
        Check.lengthBetween 3 40
        Check.matches "^[a-zA-Z0-9_]+$"
    ]

username "ada_lovelace"
```

For SRTP functions in lists, type annotations may sometimes be needed:

```fsharp
let username : Check<string> =
    Check.all [
        Check.present
        Check.lengthBetween 3 40
        Check.matches "^[a-zA-Z0-9_]+$"
    ]
```

If SRTP hurts this use case, prefer concrete top-level aliases for string/seq high-frequency checks and keep SRTP limited to direct calls.

## Expected API Shape After Tightening

Top-level structured check members:

```fsharp
all
any
not
mapFailure

present
empty
notEmpty

length
minLength
maxLength
lengthBetween
email
matches
oneOf
numeric
alphaNumeric

between
greaterThan
lessThan
atLeast
atMost
positive
nonNegative
negative
nonPositive

count
minCount
maxCount
countBetween
distinct
contains
single
atMostOne
atLeastOne
moreThanOne

equalTo
notEqualTo
```

Nested modules:

```fsharp
Check.String
Check.Number
Check.Seq
Check.Option
Check.ValueOption
Check.Nullable
Check.Result
```

Bool predicates should no longer be asserted as top-level `Check` members.

## Migration From Current Source

1. Keep `type Check<'value>` and `CheckFailure` unchanged.
2. Rename `Check.Collection` to `Check.Seq`.
3. Add missing exact checks:

   ```fsharp
   Check.String.length
   Check.Seq.count
   ```

4. Add missing structured checks mirroring current bool predicates where they are valuable:

   ```fsharp
   Check.String.empty
   Check.String.notEmpty
   Check.String.numeric
   Check.String.alphaNumeric
   Check.Number.positive
   Check.Number.nonNegative
   Check.Number.negative
   Check.Number.nonPositive
   Check.Seq.empty
   Check.Seq.contains
   Check.Seq.single
   Check.Seq.atMostOne
   Check.Seq.atLeastOne
   Check.Seq.moreThanOne
   Check.equalTo
   Check.notEqualTo
   ```

5. Add top-level concrete aliases for common single-target checks.
6. Add SRTP facade for `present`, `empty`, and `notEmpty`.
7. Move or remove top-level bool predicates from `Check`.
8. Add bool predicates as extensions or in type-specific modules.
9. Update API shape tests so `Check` means structured checks.
10. Update source comments and generated docs.

## Test Plan

Tests should cover both direct and facade surfaces.

Direct modules:

```fsharp
Check.String.present
Check.String.length
Check.String.minLength
Check.String.maxLength
Check.String.lengthBetween
Check.String.email
Check.String.matches
Check.String.oneOf

Check.Number.between
Check.Number.greaterThan
Check.Number.lessThan
Check.Number.atLeast
Check.Number.atMost
Check.Number.positive
Check.Number.nonNegative
Check.Number.negative
Check.Number.nonPositive

Check.Seq.empty
Check.Seq.notEmpty
Check.Seq.count
Check.Seq.minCount
Check.Seq.maxCount
Check.Seq.countBetween
Check.Seq.noDuplicates
Check.Seq.contains
Check.Seq.single
Check.Seq.atMostOne
Check.Seq.atLeastOne
Check.Seq.moreThanOne

Check.Option.some
Check.Option.none
Check.Option.present
Check.Option.empty

Check.ValueOption.some
Check.ValueOption.none
Check.ValueOption.present
Check.ValueOption.empty

Check.Nullable.hasValue
Check.Nullable.hasNoValue
Check.Nullable.present
Check.Nullable.empty

Check.Result.ok
Check.Result.error
```

Facade:

```fsharp
Check.present "Ada"
Check.present (Some 1)
Check.present (ValueSome 1)
Check.present (Nullable 1)

Check.empty ""
Check.empty []
Check.empty None
Check.empty ValueNone
Check.empty (Nullable<int>())

Check.notEmpty "Ada"
Check.notEmpty [ 1 ]
Check.notEmpty (Some 1)
Check.notEmpty (ValueSome 1)
Check.notEmpty (Nullable 1)

Check.distinct [ 1; 2; 3 ]
Check.countBetween 2 4 [ 1; 2; 3 ]
Check.lengthBetween 2 4 "Ada"
Check.between 1 10 5
```

Composition:

```fsharp
Check.all [ Check.present; Check.lengthBetween 2 40 ]
Check.any [ Check.email; Check.matches "^[a-z]+$" ]
Check.not Check.email
Check.mapFailure mapper Check.email
```

API shape:

- Assert top-level `Check` contains structured check names.
- Assert nested modules exist.
- Remove assertions that top-level `Check` contains bool-only predicate names.
- Add predicate API shape tests wherever predicate functions/extensions are placed.

## Open Decisions

1. Should `Check.Option.present` replace `Check.Option.some` in examples, or should both be documented?
2. Should top-level `Check.ok` and `Check.error` exist, or remain only under `Check.Result`?
3. Should exact sequence count be named `count` or `hasCount` in structured result space? Recommendation: `count`.
4. Should exact string length be named `length` or `hasLength` in structured result space? Recommendation: `length`.
5. Should bool predicates be extension methods, modules, or both?

## Recommendation

Proceed with the split:

- `Check.*` is structured result checks.
- `Check.String`, `Check.Number`, `Check.Seq`, `Check.Option`, `Check.ValueOption`, `Check.Nullable`, and `Check.Result` are direct implementations.
- `Check.present`, `Check.empty`, and `Check.notEmpty` are the only initial SRTP facade functions.
- Common single-target checks get top-level concrete aliases.
- Bool predicates move out of top-level `Check` and can be added as `Seq.isDistinct`, `String.isBlank`, extensions, or equivalent predicate-focused APIs.

This keeps the nice call sites without making the entire API depend on SRTP:

```fsharp
Check.present name
Check.lengthBetween 2 40 name
Check.distinct tags
Check.countBetween 2 4 tags
Check.between 1 10 quantity
```

And it keeps an explicit escape hatch for type inference, docs, and debugging:

```fsharp
Check.String.present name
Check.Seq.noDuplicates tags
Check.Number.between 1 10 quantity
```
