---
title: "Check"
weight: 70
type: docs
---

This page shows the `Check` surface for reusable, pure validation. Unprefixed helpers test a property, `when*` helpers preserve the original input on success, and `take*` helpers extract an inner value or return a deliberately different success shape. Simple helpers carry a `unit` error and can be converted into typed failures with `Check.withError`. Helpers with useful built-in diagnostics return typed `Result` values such as `CardinalityFailure`, `StringLengthFailure`, or `RangeFailure`. Use `Check` before moving into `Result`, `Validation`, or `Flow`.

## Core type

- [`Result.Check`](./t-result-check.md):
 Pure validation helpers. Unprefixed names are predicates, <code>when*</code> names preserve the
 original input on success, and <code>take*</code> names extract an inner value or return a
 deliberately different success shape.


## Structured errors

- [`Result.CardinalityFailure`](./t-result-cardinalityfailure.md): Structured errors returned by sequence cardinality helpers.
- [`Result.StringLengthFailure`](./t-result-stringlengthfailure.md): Structured errors returned by string length helpers.
- [`Result.RangeFailure`](./t-result-rangefailure.md): Structured errors returned by comparison and numeric helpers.

## Construction

- [`Result.Check.fromPredicate`](./m-result-check-frompredicate.md): Builds a check from a predicate while preserving the successful value.
- [`Result.Check.fromTry`](./m-result-check-fromtry.md): Converts a .NET <code>Try*</code> tuple into a check result.
- [`Result.Check.fromChoice`](./m-result-check-fromchoice.md): Converts an F# <code>Choice</code> into a <code>Result</code>.

## Composition

- [`Result.Check.negate`](./m-result-check-negate.md): Returns success when the supplied check fails.
- [`Result.Check.both`](./m-result-check-both.md): Returns success when both checks succeed.
- [`Result.Check.either`](./m-result-check-either.md): Returns success when either check succeeds.
- [`Result.Check.all`](./m-result-check-all.md): Returns success when every check in the sequence succeeds.
- [`Result.Check.any`](./m-result-check-any.md): Returns success when at least one check in the sequence succeeds.

## Boolean and branch predicates

- [`Result.Check.isTrue`](./m-result-check-istrue.md): Returns success when the condition is true.
- [`Result.Check.isFalse`](./m-result-check-isfalse.md): Returns success when the condition is false.
- [`Result.Check.isSome`](./m-result-check-issome.md): Returns success when the option is <code>Some</code>.
- [`Result.Check.isNone`](./m-result-check-isnone.md): Returns success when the option is <code>None</code>.
- [`Result.Check.isValueSome`](./m-result-check-isvaluesome.md): Returns success when the value option is <code>ValueSome</code>.
- [`Result.Check.isValueNone`](./m-result-check-isvaluenone.md): Returns success when the value option is <code>ValueNone</code>.
- [`Result.Check.isOk`](./m-result-check-isok.md): Returns success when the result is <code>Ok</code>.
- [`Result.Check.isError`](./m-result-check-iserror.md): Returns success when the result is <code>Error</code>.

## Presence predicates

- [`Result.Check.hasValue`](./m-result-check-hasvalue.md): Returns success when the nullable has a value.
- [`Result.Check.hasNoValue`](./m-result-check-hasnovalue.md): Returns success when the nullable has no value.
- [`Result.Check.notNull`](./m-result-check-notnull.md): Returns success when the reference is not null.
- [`Result.Check.isNull`](./m-result-check-isnull.md): Returns success when the reference is null.
- [`Result.Check.notEmpty`](./m-result-check-notempty.md): Returns success when the sequence is not empty.
- [`Result.Check.empty`](./m-result-check-empty.md): Returns success when the sequence is empty.

## String predicates

- [`Result.Check.notNullOrEmpty`](./m-result-check-notnullorempty.md): Returns success when the string is not null or empty.
- [`Result.Check.nullOrEmpty`](./m-result-check-nullorempty.md): Returns success when the string is null or empty.
- [`Result.Check.notEmptyString`](./m-result-check-notemptystring.md): Returns success when the string has length greater than zero.
- [`Result.Check.emptyString`](./m-result-check-emptystring.md): Returns success when the string is exactly empty, not null.
- [`Result.Check.notBlank`](./m-result-check-notblank.md): Returns success when the string is not blank.
- [`Result.Check.blank`](./m-result-check-blank.md): Returns success when the string is blank.
- [`Result.Check.minLength`](./m-result-check-minlength.md): Returns success when the string length is at least the supplied minimum.
- [`Result.Check.maxLength`](./m-result-check-maxlength.md): Returns success when the string length is at most the supplied maximum.
- [`Result.Check.exactLength`](./m-result-check-exactlength.md): Returns success when the string length equals the supplied length.
- [`Result.Check.matchesRegex`](./m-result-check-matchesregex.md): Returns success when the string matches the supplied regular expression pattern.

## Collection predicates

- [`Result.Check.contains`](./m-result-check-contains.md): Returns success when the sequence contains the expected value.
- [`Result.Check.hasCount`](./m-result-check-hascount.md): Returns success when the sequence count equals the expected count.
- [`Result.Check.hasDuplicates`](./m-result-check-hasduplicates.md): Returns success when the sequence contains duplicate values.
- [`Result.Check.hasNoDuplicates`](./m-result-check-hasnoduplicates.md): Returns success when the sequence contains no duplicate values.
- [`Result.Check.isSingle`](./m-result-check-issingle.md): Returns success when the sequence contains exactly one item.
- [`Result.Check.atMostOne`](./m-result-check-atmostone.md): Returns success when the sequence contains at most one item.
- [`Result.Check.atLeastOne`](./m-result-check-atleastone.md): Returns success when the sequence contains at least one item.
- [`Result.Check.moreThanOne`](./m-result-check-morethanone.md): Returns success when the sequence contains more than one item.

## Equality and range predicates

- [`Result.Check.equalTo`](./m-result-check-equalto.md): Returns success when the actual value equals the expected value.
- [`Result.Check.notEqualTo`](./m-result-check-notequalto.md): Returns success when the actual value does not equal the expected value.
- [`Result.Check.greaterThan`](./m-result-check-greaterthan.md): Returns success when the actual value is greater than the supplied bound.
- [`Result.Check.lessThan`](./m-result-check-lessthan.md): Returns success when the actual value is less than the supplied bound.
- [`Result.Check.atLeast`](./m-result-check-atleast.md): Returns success when the actual value is greater than or equal to the supplied bound.
- [`Result.Check.atMost`](./m-result-check-atmost.md): Returns success when the actual value is less than or equal to the supplied bound.
- [`Result.Check.between`](./m-result-check-between.md): Returns success when the actual value is between the inclusive bounds.
- [`Result.Check.positive`](./m-result-check-positive.md): Returns success when the numeric value is greater than zero.
- [`Result.Check.nonNegative`](./m-result-check-nonnegative.md): Returns success when the numeric value is greater than or equal to zero.
- [`Result.Check.negative`](./m-result-check-negative.md): Returns success when the numeric value is less than zero.
- [`Result.Check.nonPositive`](./m-result-check-nonpositive.md): Returns success when the numeric value is less than or equal to zero.

## Preserving gates

- [`Result.Check.whenTrue`](./m-result-check-whentrue.md): Keeps the boolean when it is true.
- [`Result.Check.whenFalse`](./m-result-check-whenfalse.md): Keeps the boolean when it is false.
- [`Result.Check.whenSome`](./m-result-check-whensome.md): Keeps the option when it is <code>Some</code>.
- [`Result.Check.whenNone`](./m-result-check-whennone.md): Keeps the option when it is <code>None</code>.
- [`Result.Check.whenValueSome`](./m-result-check-whenvaluesome.md): Keeps the value option when it is <code>ValueSome</code>.
- [`Result.Check.whenValueNone`](./m-result-check-whenvaluenone.md): Keeps the value option when it is <code>ValueNone</code>.
- [`Result.Check.whenHasValue`](./m-result-check-whenhasvalue.md): Keeps the nullable when it has a value.
- [`Result.Check.whenHasNoValue`](./m-result-check-whenhasnovalue.md): Keeps the nullable when it has no value.
- [`Result.Check.whenNotNull`](./m-result-check-whennotnull.md): Keeps the reference when it is not null.
- [`Result.Check.whenNull`](./m-result-check-whennull.md): Keeps the reference when it is null.
- [`Result.Check.whenOk`](./m-result-check-whenok.md): Keeps the result when it is <code>Ok</code>.
- [`Result.Check.whenError`](./m-result-check-whenerror.md): Keeps the result when it is <code>Error</code>.
- [`Result.Check.whenNotEmpty`](./m-result-check-whennotempty.md): Keeps the collection when it is not empty.
- [`Result.Check.whenEmpty`](./m-result-check-whenempty.md): Keeps the collection when it is empty.
- [`Result.Check.whenNotNullOrEmpty`](./m-result-check-whennotnullorempty.md): Keeps the string when it is not null or empty.
- [`Result.Check.whenNullOrEmpty`](./m-result-check-whennullorempty.md): Keeps the string when it is null or empty.
- [`Result.Check.whenNotEmptyString`](./m-result-check-whennotemptystring.md): Keeps the string when it has length greater than zero.
- [`Result.Check.whenEmptyString`](./m-result-check-whenemptystring.md): Keeps the string when it is exactly empty, not null.
- [`Result.Check.whenNotBlank`](./m-result-check-whennotblank.md): Keeps the string when it is not blank.
- [`Result.Check.whenBlank`](./m-result-check-whenblank.md): Keeps the string when it is blank.
- [`Result.Check.whenMinLength`](./m-result-check-whenminlength.md): Keeps the string when its length is at least the supplied minimum.
- [`Result.Check.whenMaxLength`](./m-result-check-whenmaxlength.md): Keeps the string when its length is at most the supplied maximum.
- [`Result.Check.whenExactLength`](./m-result-check-whenexactlength.md): Keeps the string when its length equals the supplied length.
- [`Result.Check.whenMatchesRegex`](./m-result-check-whenmatchesregex.md): Keeps the string when it matches the supplied regular expression pattern.
- [`Result.Check.whenEqualTo`](./m-result-check-whenequalto.md): Keeps the actual value when it equals the expected value.
- [`Result.Check.whenNotEqualTo`](./m-result-check-whennotequalto.md): Keeps the actual value when it does not equal the expected value.
- [`Result.Check.whenContains`](./m-result-check-whencontains.md): Keeps the collection when it contains the expected value.
- [`Result.Check.whenCount`](./m-result-check-whencount.md): Keeps the collection when its count equals the expected count.
- [`Result.Check.whenHasDuplicates`](./m-result-check-whenhasduplicates.md): Keeps the collection when it contains duplicate values.
- [`Result.Check.whenHasNoDuplicates`](./m-result-check-whenhasnoduplicates.md): Keeps the collection when it contains no duplicate values.
- [`Result.Check.whenSingle`](./m-result-check-whensingle.md): Keeps the collection when it contains exactly one item.
- [`Result.Check.whenAtMostOne`](./m-result-check-whenatmostone.md): Keeps the collection when it contains at most one item.
- [`Result.Check.whenAtLeastOne`](./m-result-check-whenatleastone.md): Keeps the collection when it contains at least one item.
- [`Result.Check.whenMoreThanOne`](./m-result-check-whenmorethanone.md): Keeps the collection when it contains more than one item.
- [`Result.Check.whenGreaterThan`](./m-result-check-whengreaterthan.md): Keeps the value when it is greater than the supplied bound.
- [`Result.Check.whenLessThan`](./m-result-check-whenlessthan.md): Keeps the value when it is less than the supplied bound.
- [`Result.Check.whenAtLeast`](./m-result-check-whenatleast.md): Keeps the value when it is greater than or equal to the supplied bound.
- [`Result.Check.whenAtMost`](./m-result-check-whenatmost.md): Keeps the value when it is less than or equal to the supplied bound.
- [`Result.Check.whenBetween`](./m-result-check-whenbetween.md): Keeps the value when it is between the inclusive bounds.
- [`Result.Check.whenPositive`](./m-result-check-whenpositive.md): Keeps the numeric value when it is greater than zero.
- [`Result.Check.whenNonNegative`](./m-result-check-whennonnegative.md): Keeps the numeric value when it is greater than or equal to zero.
- [`Result.Check.whenNegative`](./m-result-check-whennegative.md): Keeps the numeric value when it is less than zero.
- [`Result.Check.whenNonPositive`](./m-result-check-whennonpositive.md): Keeps the numeric value when it is less than or equal to zero.

## Extraction helpers

- [`Result.Check.takeSome`](./m-result-check-takesome.md): Takes the value from an option when it is <code>Some</code>.
- [`Result.Check.takeValueSome`](./m-result-check-takevaluesome.md): Takes the value from a value option when it is <code>ValueSome</code>.
- [`Result.Check.takeHasValue`](./m-result-check-takehasvalue.md): Takes the value from a nullable when it has a value.
- [`Result.Check.takeOk`](./m-result-check-takeok.md): Takes the successful value from a result when it is <code>Ok</code>.
- [`Result.Check.takeError`](./m-result-check-takeerror.md): Takes the error value from a result when it is <code>Error</code>.
- [`Result.Check.takeHead`](./m-result-check-takehead.md): Takes the first item from a sequence when it is not empty.
- [`Result.Check.takeSingle`](./m-result-check-takesingle.md): Takes the only item from a sequence when it contains exactly one item.
- [`Result.Check.takeAtMostOne`](./m-result-check-takeatmostone.md): Takes zero or one item from a sequence when it contains at most one item.

## Error attachment

- [`Result.Check.withError`](./m-result-check-witherror.md): Assigns the supplied application error to a unit-error check failure.
