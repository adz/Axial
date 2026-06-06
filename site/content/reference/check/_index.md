---
title: "Check"
weight: 70
type: docs
---

This page shows the `Check` surface for reusable, pure predicates. A check carries a `unit` error: it says whether a condition passed without deciding the final domain error yet. This makes checks easy to compose, negate, reuse, and convert into typed failures with `withError`. Use `Check` for local facts such as non-empty strings, equality, null checks, cardinality checks, and option presence. When a predicate should return a useful value, use `Take`; when you need to collect several named failures, move to `Validation`; when you need environment or async work, lift the result into `Flow`.

## Core type

- [`Check`](./t-check.md):
 Predicate helpers that return <a href="t-check.md">Check</a> values with a unit error, plus
 bridge functions that turn those checks into application errors.


## Structured errors

- [`CardinalityFailure`](./t-cardinalityfailure.md): Structured errors returned by sequence cardinality helpers that preserve useful diagnostics.

## Construction

- [`Check.fromPredicate`](./m-check-frompredicate.md): Builds a check from a predicate while preserving the successful value.
- [`Check.fromTry`](./m-check-fromtry.md): Converts a .NET <code>Try*</code> tuple into a check result.
- [`Check.fromChoice`](./m-check-fromchoice.md): Converts an F# <code>Choice</code> into a <code>Result</code>.

## Composition

- [`Check.negate`](./m-check-negate.md): Returns success when the supplied check fails.
- [`Check.both`](./m-check-both.md): Returns success when both checks succeed.
- [`Check.either`](./m-check-either.md): Returns success when either check succeeds.
- [`Check.all`](./m-check-all.md): Returns success when every check in the sequence succeeds.
- [`Check.any`](./m-check-any.md): Returns success when at least one check in the sequence succeeds.

## Predicates

- [`Check.isTrue`](./m-check-istrue.md): Returns success when the condition is true.
- [`Check.isFalse`](./m-check-isfalse.md): Returns success when the condition is false.
- [`Check.some`](./m-check-some.md): Returns success when the option is <code>Some</code>.
- [`Check.none`](./m-check-none.md): Returns success when the option is <code>None</code>.
- [`Check.valueSome`](./m-check-valuesome.md): Returns success when the value option is <code>ValueSome</code>.
- [`Check.valueNone`](./m-check-valuenone.md): Returns success when the value option is <code>ValueNone</code>.
- [`Check.hasValue`](./m-check-hasvalue.md): Returns success when the nullable has a value.
- [`Check.hasNoValue`](./m-check-hasnovalue.md): Returns success when the nullable has no value.
- [`Check.notNull`](./m-check-notnull.md): Returns success when the reference is not null.
- [`Check.isNull`](./m-check-isnull.md): Returns success when the reference is null.
- [`Check.notEmpty`](./m-check-notempty.md): Returns success when the sequence is not empty.
- [`Check.empty`](./m-check-empty.md): Returns success when the sequence is empty.
- [`Check.notNullOrEmpty`](./m-check-notnullorempty.md): Returns success when the string is not null or empty.
- [`Check.nullOrEmpty`](./m-check-nullorempty.md): Returns success when the string is null or empty.
- [`Check.notBlank`](./m-check-notblank.md): Returns success when the string is not blank.
- [`Check.blank`](./m-check-blank.md): Returns success when the string is blank.
- [`Check.equalTo`](./m-check-equalto.md): Returns success when the actual value equals the expected value.
- [`Check.notEqualTo`](./m-check-notequalto.md): Returns success when the actual value does not equal the expected value.
- [`Check.contains`](./m-check-contains.md): Returns success when the sequence contains the expected value.
- [`Check.hasCount`](./m-check-hascount.md): Returns success when the sequence count equals the expected count.
- [`Check.exactlyOne`](./m-check-exactlyone.md): Returns success when the sequence contains exactly one item.
- [`Check.atMostOne`](./m-check-atmostone.md): Returns success when the sequence contains at most one item.
- [`Check.atLeastOne`](./m-check-atleastone.md): Returns success when the sequence contains at least one item.
- [`Check.moreThanOne`](./m-check-morethanone.md): Returns success when the sequence contains more than one item.
- [`Check.hasDuplicates`](./m-check-hasduplicates.md): Returns success when the sequence contains duplicate values.

## Error attachment

- [`Check.withError`](./m-check-witherror.md): Assigns the supplied application error to a unit-error check failure.
