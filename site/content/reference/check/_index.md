---
title: "Check"
weight: 70
type: docs
---

This page shows the `Check` surface for reusable, pure predicates. `Check` functions return `bool`; they do not attach errors, preserve values in a `Result`, or extract inner values. Use them directly with ordinary F# boolean composition, collection functions, and `Result.guard` or `Result.fromPredicate` when a predicate needs to become a fail-fast result.

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
