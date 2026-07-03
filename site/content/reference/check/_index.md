---
title: "Check"
weight: 70
type: docs
---

This page shows the `Check` surface for reusable, path-free value constraints. Executable checks return `Result<unit, CheckFailure list>` and can be composed with `Check.all`, `Check.any`, `Check.not`, and `Check.mapFailure`. Top-level helpers such as `Check.notBlank` remain lightweight boolean predicates for local structural facts. Use `Result.require` or `Result.guard` when a `Check<'value>` should become a fail-fast result that preserves the checked value.

## Core types

- [`ErrorHandling.Check`](./t-errorhandling-check.md):
 Typed value-check programs and common boolean predicates for local structural facts.

- [`ErrorHandling.CheckFailure`](./t-errorhandling-checkfailure.md): Describes why an executable value check failed, without attaching source paths or raw input.
- [`ErrorHandling.CheckLengthExpectation`](./t-errorhandling-checklengthexpectation.md): Describes the length requirement that a value check expected a string-like value to satisfy.
- [`ErrorHandling.CheckRangeExpectation`](./t-errorhandling-checkrangeexpectation.md): Describes the ordering requirement that a value check expected a comparable value to satisfy.
- [`ErrorHandling.CheckCountExpectation`](./t-errorhandling-checkcountexpectation.md): Describes the count requirement that a value check expected a sequence-shaped value to satisfy.
- [`ErrorHandling.CheckEqualityExpectation`](./t-errorhandling-checkequalityexpectation.md): Describes the equality requirement that a value check expected a value to satisfy.

## Executable composition

- [`ErrorHandling.Check.all`](./m-errorhandling-check-all.md): Combines checks conjunctively by running every check against the value and accumulating all failures. An empty list succeeds.
- [`ErrorHandling.Check.any`](./m-errorhandling-check-any.md): Combines checks disjunctively by running checks until one succeeds, or returns accumulated failures when every check fails. An empty list fails with no failures.
- [`ErrorHandling.Check.``not```](./m-errorhandling-check-not.md): Inverts a check. A successful inner check becomes a custom-code failure, while any failed inner check succeeds.
- [`ErrorHandling.Check.mapFailure`](./m-errorhandling-check-mapfailure.md): Maps every failure produced by a check.

## Executable string checks

- [`ErrorHandling.Check.String.present`](./m-errorhandling-check-string-present.md): Requires an already parsed string value to be non-null and contain at least one non-whitespace character.
- [`ErrorHandling.Check.String.minLength`](./m-errorhandling-check-string-minlength.md): Requires an already parsed string value to have at least the supplied length. Null fails with an unknown actual length.
- [`ErrorHandling.Check.String.maxLength`](./m-errorhandling-check-string-maxlength.md): Requires an already parsed string value to have at most the supplied length. Null fails with an unknown actual length.
- [`ErrorHandling.Check.String.lengthBetween`](./m-errorhandling-check-string-lengthbetween.md): Requires an already parsed string value length to lie inside the supplied inclusive bounds. Null fails with an unknown actual length.
- [`ErrorHandling.Check.String.exactLength`](./m-errorhandling-check-string-exactlength.md): Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.
- [`ErrorHandling.Check.String.email`](./m-errorhandling-check-string-email.md): Requires an already parsed string value to match Axial's pragmatic email format.
- [`ErrorHandling.Check.String.matches`](./m-errorhandling-check-string-matches.md): Requires an already parsed string value to match the supplied regular expression pattern.
- [`ErrorHandling.Check.String.oneOf`](./m-errorhandling-check-string-oneof.md): Requires an already parsed string value to equal one of the supplied choices. Null fails with an unknown actual value.

## Executable number checks

- [`ErrorHandling.Check.Number.between`](./m-errorhandling-check-number-between.md): Requires a value to lie inside the supplied inclusive bounds.
- [`ErrorHandling.Check.Number.greaterThan`](./m-errorhandling-check-number-greaterthan.md): Requires a value to be greater than the supplied exclusive lower bound.
- [`ErrorHandling.Check.Number.lessThan`](./m-errorhandling-check-number-lessthan.md): Requires a value to be less than the supplied exclusive upper bound.
- [`ErrorHandling.Check.Number.atLeast`](./m-errorhandling-check-number-atleast.md): Requires a value to be greater than or equal to the supplied lower bound.
- [`ErrorHandling.Check.Number.atMost`](./m-errorhandling-check-number-atmost.md): Requires a value to be less than or equal to the supplied upper bound.

## Executable sequence checks

- [`ErrorHandling.Check.Seq.notEmpty`](./m-errorhandling-check-seq-notempty.md): Requires an already parsed sequence-shaped value to contain at least one item. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.minCount`](./m-errorhandling-check-seq-mincount.md): Requires an already parsed sequence-shaped value to contain at least the supplied count. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.maxCount`](./m-errorhandling-check-seq-maxcount.md): Requires an already parsed sequence-shaped value to contain at most the supplied count. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.countBetween`](./m-errorhandling-check-seq-countbetween.md): Requires an already parsed sequence-shaped value count to lie inside the supplied inclusive bounds. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.distinct`](./m-errorhandling-check-seq-distinct.md): Requires an already parsed sequence-shaped value to contain no duplicate values.

## Executable optional checks

- [`ErrorHandling.Check.Option.some`](./m-errorhandling-check-option-some.md): Requires an option to contain a value.
- [`ErrorHandling.Check.Option.none`](./m-errorhandling-check-option-none.md): Requires an option to contain no value.
- [`ErrorHandling.Check.ValueOption.some`](./m-errorhandling-check-valueoption-some.md): Requires a value option to contain a value.
- [`ErrorHandling.Check.ValueOption.none`](./m-errorhandling-check-valueoption-none.md): Requires a value option to contain no value.
- [`ErrorHandling.Check.Nullable.hasValue`](./m-errorhandling-check-nullable-hasvalue.md): Requires a nullable value to contain a value.
- [`ErrorHandling.Check.Nullable.hasNoValue`](./m-errorhandling-check-nullable-hasnovalue.md): Requires a nullable value to contain no value.
- [`ErrorHandling.Check.Result.ok`](./m-errorhandling-check-result-ok.md): Requires a result to contain a successful value.
- [`ErrorHandling.Check.Result.error`](./m-errorhandling-check-result-error.md): Requires a result to contain an error value.

## Option and result predicates

- [`ErrorHandling.Check.isSome`](./m-errorhandling-check-issome.md): Returns true when the option is <code>Some</code>.
- [`ErrorHandling.Check.isNone`](./m-errorhandling-check-isnone.md): Returns true when the option is <code>None</code>.
- [`ErrorHandling.Check.isValueSome`](./m-errorhandling-check-isvaluesome.md): Returns true when the value option is <code>ValueSome</code>.
- [`ErrorHandling.Check.isValueNone`](./m-errorhandling-check-isvaluenone.md): Returns true when the value option is <code>ValueNone</code>.
- [`ErrorHandling.Check.isOk`](./m-errorhandling-check-isok.md): Returns true when the result is <code>Ok</code>.
- [`ErrorHandling.Check.isError`](./m-errorhandling-check-iserror.md): Returns true when the result is <code>Error</code>.

## Presence predicates

- [`ErrorHandling.Check.hasValue`](./m-errorhandling-check-hasvalue.md): Returns true when the nullable contains a value.
- [`ErrorHandling.Check.hasNoValue`](./m-errorhandling-check-hasnovalue.md): Returns true when the nullable is empty.
- [`ErrorHandling.Check.notNull`](./m-errorhandling-check-notnull.md): Returns true when the reference is not null.
- [`ErrorHandling.Check.isNull`](./m-errorhandling-check-isnull.md): Returns true when the reference is null.
- [`ErrorHandling.Check.notEmpty`](./m-errorhandling-check-notempty.md): Returns true when the sequence contains at least one item.
- [`ErrorHandling.Check.isEmpty`](./m-errorhandling-check-isempty.md): Returns true when the sequence is empty.

## String predicates

- [`ErrorHandling.Check.notNullOrEmpty`](./m-errorhandling-check-notnullorempty.md): Returns true when the string is not null or empty.
- [`ErrorHandling.Check.nullOrEmpty`](./m-errorhandling-check-nullorempty.md): Returns true when the string is null or empty.
- [`ErrorHandling.Check.notEmptyString`](./m-errorhandling-check-notemptystring.md): Returns true when the string has at least one character and is not null.
- [`ErrorHandling.Check.emptyString`](./m-errorhandling-check-emptystring.md): Returns true when the string is exactly empty and not null.
- [`ErrorHandling.Check.notBlank`](./m-errorhandling-check-notblank.md): Returns true when the string is not null, empty, or whitespace.
- [`ErrorHandling.Check.blank`](./m-errorhandling-check-blank.md): Returns true when the string is null, empty, or whitespace.
- [`ErrorHandling.Check.hasMinLength`](./m-errorhandling-check-hasminlength.md): Returns true when the string length is at least the supplied minimum.
- [`ErrorHandling.Check.hasMaxLength`](./m-errorhandling-check-hasmaxlength.md): Returns true when the string length is at most the supplied maximum.
- [`ErrorHandling.Check.hasExactLength`](./m-errorhandling-check-hasexactlength.md): Returns true when the string length equals the supplied expected length.
- [`ErrorHandling.Check.matchesRegex`](./m-errorhandling-check-matchesregex.md): Returns true when the string matches the supplied regular expression pattern.
- [`ErrorHandling.Check.isEmail`](./m-errorhandling-check-isemail.md): Returns true when the string matches a pragmatic email pattern.
- [`ErrorHandling.Check.isNumeric`](./m-errorhandling-check-isnumeric.md): Returns true when the string contains only numeric characters.
- [`ErrorHandling.Check.isAlphaNumeric`](./m-errorhandling-check-isalphanumeric.md): Returns true when the string contains only letter or digit characters.

## Collection predicates

- [`ErrorHandling.Check.contains`](./m-errorhandling-check-contains.md): Returns true when the sequence contains the expected value.
- [`ErrorHandling.Check.hasCount`](./m-errorhandling-check-hascount.md): Returns true when the sequence count equals the expected count.
- [`ErrorHandling.Check.hasDuplicates`](./m-errorhandling-check-hasduplicates.md): Returns true when the sequence contains duplicate values.
- [`ErrorHandling.Check.hasNoDuplicates`](./m-errorhandling-check-hasnoduplicates.md): Returns true when the sequence contains no duplicate values.
- [`ErrorHandling.Check.isSingle`](./m-errorhandling-check-issingle.md): Returns true when the sequence contains exactly one item.
- [`ErrorHandling.Check.atMostOne`](./m-errorhandling-check-atmostone.md): Returns true when the sequence contains zero or one item.
- [`ErrorHandling.Check.atLeastOne`](./m-errorhandling-check-atleastone.md): Returns true when the sequence contains at least one item.
- [`ErrorHandling.Check.moreThanOne`](./m-errorhandling-check-morethanone.md): Returns true when the sequence contains more than one item.

## Equality and range predicates

- [`ErrorHandling.Check.equalTo`](./m-errorhandling-check-equalto.md): Returns true when the actual value equals the expected value.
- [`ErrorHandling.Check.notEqualTo`](./m-errorhandling-check-notequalto.md): Returns true when the actual value does not equal the expected value.
- [`ErrorHandling.Check.greaterThan`](./m-errorhandling-check-greaterthan.md): Returns true when the value is greater than the supplied exclusive lower bound.
- [`ErrorHandling.Check.lessThan`](./m-errorhandling-check-lessthan.md): Returns true when the value is less than the supplied exclusive upper bound.
- [`ErrorHandling.Check.atLeast`](./m-errorhandling-check-atleast.md): Returns true when the value is greater than or equal to the supplied lower bound.
- [`ErrorHandling.Check.atMost`](./m-errorhandling-check-atmost.md): Returns true when the value is less than or equal to the supplied upper bound.
- [`ErrorHandling.Check.between`](./m-errorhandling-check-between.md): Returns true when the value lies between the supplied inclusive bounds.
- [`ErrorHandling.Check.positive`](./m-errorhandling-check-positive.md): Returns true when the numeric value is greater than zero.
- [`ErrorHandling.Check.nonNegative`](./m-errorhandling-check-nonnegative.md): Returns true when the numeric value is greater than or equal to zero.
- [`ErrorHandling.Check.negative`](./m-errorhandling-check-negative.md): Returns true when the numeric value is less than zero.
- [`ErrorHandling.Check.nonPositive`](./m-errorhandling-check-nonpositive.md): Returns true when the numeric value is less than or equal to zero.

## Predicate composition

- [`ErrorHandling.Check.negate`](./m-errorhandling-check-negate.md): Inverts a predicate.
