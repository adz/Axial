---
title: "Check"
weight: 70
---

This page shows the `Check` surface for reusable, pure validation. Unprefixed helpers test a property, `when*` helpers preserve the original input on success, and `take*` helpers extract or narrow a useful value. Simple helpers carry a `unit` error and can be converted into typed failures with `Check.withError`. Helpers with useful built-in diagnostics return typed `Result` values such as `CardinalityFailure`, `StringLengthFailure`, or `RangeFailure`. Use `Check` before moving into `Result`, `Validation`, or `Flow`.

## Core type

- [`Check`](./t-check.md):
 Pure validation helpers. Unprefixed names are predicates, <code>when*</code> names preserve the
 original input on success, and <code>take*</code> names extract or narrow the successful value.


## Structured errors

- [`CardinalityFailure`](./t-cardinalityfailure.md): Structured errors returned by sequence cardinality helpers.
- [`StringLengthFailure`](./t-stringlengthfailure.md): Structured errors returned by string length helpers.
- [`RangeFailure`](./t-rangefailure.md): Structured errors returned by comparison and numeric helpers.

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

## Boolean and branch predicates

- [`Check.isTrue`](./m-check-istrue.md): Returns success when the condition is true.
- [`Check.isFalse`](./m-check-isfalse.md): Returns success when the condition is false.
- [`Check.isSome`](./m-check-issome.md): Returns success when the option is <code>Some</code>.
- [`Check.isNone`](./m-check-isnone.md): Returns success when the option is <code>None</code>.
- [`Check.isValueSome`](./m-check-isvaluesome.md): Returns success when the value option is <code>ValueSome</code>.
- [`Check.isValueNone`](./m-check-isvaluenone.md): Returns success when the value option is <code>ValueNone</code>.
- [`Check.isOk`](./m-check-isok.md): Returns success when the result is <code>Ok</code>.
- [`Check.isError`](./m-check-iserror.md): Returns success when the result is <code>Error</code>.

## Presence predicates

- [`Check.hasValue`](./m-check-hasvalue.md): Returns success when the nullable has a value.
- [`Check.hasNoValue`](./m-check-hasnovalue.md): Returns success when the nullable has no value.
- [`Check.notNull`](./m-check-notnull.md): Returns success when the reference is not null.
- [`Check.isNull`](./m-check-isnull.md): Returns success when the reference is null.
- [`Check.notEmpty`](./m-check-notempty.md): Returns success when the sequence is not empty.
- [`Check.empty`](./m-check-empty.md): Returns success when the sequence is empty.

## String predicates

- [`Check.notNullOrEmpty`](./m-check-notnullorempty.md): Returns success when the string is not null or empty.
- [`Check.nullOrEmpty`](./m-check-nullorempty.md): Returns success when the string is null or empty.
- [`Check.notEmptyString`](./m-check-notemptystring.md): Returns success when the string has length greater than zero.
- [`Check.emptyString`](./m-check-emptystring.md): Returns success when the string is exactly empty, not null.
- [`Check.notBlank`](./m-check-notblank.md): Returns success when the string is not blank.
- [`Check.blank`](./m-check-blank.md): Returns success when the string is blank.
- [`Check.minLength`](./m-check-minlength.md): Returns success when the string length is at least the supplied minimum.
- [`Check.maxLength`](./m-check-maxlength.md): Returns success when the string length is at most the supplied maximum.
- [`Check.exactLength`](./m-check-exactlength.md): Returns success when the string length equals the supplied length.
- [`Check.matchesRegex`](./m-check-matchesregex.md): Returns success when the string matches the supplied regular expression pattern.

## Collection predicates

- [`Check.contains`](./m-check-contains.md): Returns success when the sequence contains the expected value.
- [`Check.hasCount`](./m-check-hascount.md): Returns success when the sequence count equals the expected count.
- [`Check.hasDuplicates`](./m-check-hasduplicates.md): Returns success when the sequence contains duplicate values.
- [`Check.hasNoDuplicates`](./m-check-hasnoduplicates.md): Returns success when the sequence contains no duplicate values.
- [`Check.isSingle`](./m-check-issingle.md): Returns success when the sequence contains exactly one item.
- [`Check.atMostOne`](./m-check-atmostone.md): Returns success when the sequence contains at most one item.
- [`Check.atLeastOne`](./m-check-atleastone.md): Returns success when the sequence contains at least one item.
- [`Check.moreThanOne`](./m-check-morethanone.md): Returns success when the sequence contains more than one item.

## Equality and range predicates

- [`Check.equalTo`](./m-check-equalto.md): Returns success when the actual value equals the expected value.
- [`Check.notEqualTo`](./m-check-notequalto.md): Returns success when the actual value does not equal the expected value.
- [`Check.greaterThan`](./m-check-greaterthan.md): Returns success when the actual value is greater than the supplied bound.
- [`Check.lessThan`](./m-check-lessthan.md): Returns success when the actual value is less than the supplied bound.
- [`Check.atLeast`](./m-check-atleast.md): Returns success when the actual value is greater than or equal to the supplied bound.
- [`Check.atMost`](./m-check-atmost.md): Returns success when the actual value is less than or equal to the supplied bound.
- [`Check.between`](./m-check-between.md): Returns success when the actual value is between the inclusive bounds.
- [`Check.positive`](./m-check-positive.md): Returns success when the numeric value is greater than zero.
- [`Check.nonNegative`](./m-check-nonnegative.md): Returns success when the numeric value is greater than or equal to zero.
- [`Check.negative`](./m-check-negative.md): Returns success when the numeric value is less than zero.
- [`Check.nonPositive`](./m-check-nonpositive.md): Returns success when the numeric value is less than or equal to zero.

## Preserving gates

- [`Check.whenTrue`](./m-check-whentrue.md): Keeps the boolean when it is true.
- [`Check.whenFalse`](./m-check-whenfalse.md): Keeps the boolean when it is false.
- [`Check.whenSome`](./m-check-whensome.md): Keeps the option when it is <code>Some</code>.
- [`Check.whenNone`](./m-check-whennone.md): Keeps the option when it is <code>None</code>.
- [`Check.whenValueSome`](./m-check-whenvaluesome.md): Keeps the value option when it is <code>ValueSome</code>.
- [`Check.whenValueNone`](./m-check-whenvaluenone.md): Keeps the value option when it is <code>ValueNone</code>.
- [`Check.whenHasValue`](./m-check-whenhasvalue.md): Keeps the nullable when it has a value.
- [`Check.whenHasNoValue`](./m-check-whenhasnovalue.md): Keeps the nullable when it has no value.
- [`Check.whenNotNull`](./m-check-whennotnull.md): Keeps the reference when it is not null.
- [`Check.whenNull`](./m-check-whennull.md): Keeps the reference when it is null.
- [`Check.whenOk`](./m-check-whenok.md): Keeps the result when it is <code>Ok</code>.
- [`Check.whenError`](./m-check-whenerror.md): Keeps the result when it is <code>Error</code>.
- [`Check.whenNotEmpty`](./m-check-whennotempty.md): Keeps the collection when it is not empty.
- [`Check.whenEmpty`](./m-check-whenempty.md): Keeps the collection when it is empty.
- [`Check.whenNotNullOrEmpty`](./m-check-whennotnullorempty.md): Keeps the string when it is not null or empty.
- [`Check.whenNullOrEmpty`](./m-check-whennullorempty.md): Keeps the string when it is null or empty.
- [`Check.whenNotEmptyString`](./m-check-whennotemptystring.md): Keeps the string when it has length greater than zero.
- [`Check.whenEmptyString`](./m-check-whenemptystring.md): Keeps the string when it is exactly empty, not null.
- [`Check.whenNotBlank`](./m-check-whennotblank.md): Keeps the string when it is not blank.
- [`Check.whenBlank`](./m-check-whenblank.md): Keeps the string when it is blank.
- [`Check.whenMinLength`](./m-check-whenminlength.md): Keeps the string when its length is at least the supplied minimum.
- [`Check.whenMaxLength`](./m-check-whenmaxlength.md): Keeps the string when its length is at most the supplied maximum.
- [`Check.whenExactLength`](./m-check-whenexactlength.md): Keeps the string when its length equals the supplied length.
- [`Check.whenMatchesRegex`](./m-check-whenmatchesregex.md): Keeps the string when it matches the supplied regular expression pattern.
- [`Check.whenEqualTo`](./m-check-whenequalto.md): Keeps the actual value when it equals the expected value.
- [`Check.whenNotEqualTo`](./m-check-whennotequalto.md): Keeps the actual value when it does not equal the expected value.
- [`Check.whenContains`](./m-check-whencontains.md): Keeps the collection when it contains the expected value.
- [`Check.whenCount`](./m-check-whencount.md): Keeps the collection when its count equals the expected count.
- [`Check.whenHasDuplicates`](./m-check-whenhasduplicates.md): Keeps the collection when it contains duplicate values.
- [`Check.whenHasNoDuplicates`](./m-check-whenhasnoduplicates.md): Keeps the collection when it contains no duplicate values.
- [`Check.whenSingle`](./m-check-whensingle.md): Keeps the collection when it contains exactly one item.
- [`Check.whenAtMostOne`](./m-check-whenatmostone.md): Keeps the collection when it contains at most one item.
- [`Check.whenAtLeastOne`](./m-check-whenatleastone.md): Keeps the collection when it contains at least one item.
- [`Check.whenMoreThanOne`](./m-check-whenmorethanone.md): Keeps the collection when it contains more than one item.
- [`Check.whenGreaterThan`](./m-check-whengreaterthan.md): Keeps the value when it is greater than the supplied bound.
- [`Check.whenLessThan`](./m-check-whenlessthan.md): Keeps the value when it is less than the supplied bound.
- [`Check.whenAtLeast`](./m-check-whenatleast.md): Keeps the value when it is greater than or equal to the supplied bound.
- [`Check.whenAtMost`](./m-check-whenatmost.md): Keeps the value when it is less than or equal to the supplied bound.
- [`Check.whenBetween`](./m-check-whenbetween.md): Keeps the value when it is between the inclusive bounds.
- [`Check.whenPositive`](./m-check-whenpositive.md): Keeps the numeric value when it is greater than zero.
- [`Check.whenNonNegative`](./m-check-whennonnegative.md): Keeps the numeric value when it is greater than or equal to zero.
- [`Check.whenNegative`](./m-check-whennegative.md): Keeps the numeric value when it is less than zero.
- [`Check.whenNonPositive`](./m-check-whennonpositive.md): Keeps the numeric value when it is less than or equal to zero.

## Extraction helpers

- [`Check.takeSome`](./m-check-takesome.md): Takes the value from an option when it is <code>Some</code>.
- [`Check.takeValueSome`](./m-check-takevaluesome.md): Takes the value from a value option when it is <code>ValueSome</code>.
- [`Check.takeHasValue`](./m-check-takehasvalue.md): Takes the value from a nullable when it has a value.
- [`Check.takeNotNull`](./m-check-takenotnull.md): Takes the reference when it is not null.
- [`Check.takeOk`](./m-check-takeok.md): Takes the successful value from a result when it is <code>Ok</code>.
- [`Check.takeError`](./m-check-takeerror.md): Takes the error value from a result when it is <code>Error</code>.
- [`Check.takeHead`](./m-check-takehead.md): Takes the first item from a sequence when it is not empty.
- [`Check.takeSingle`](./m-check-takesingle.md): Takes the only item from a sequence when it contains exactly one item.
- [`Check.takeAtMostOne`](./m-check-takeatmostone.md): Takes zero or one item from a sequence when it contains at most one item.

## Error attachment

- [`Check.withError`](./m-check-witherror.md): Assigns the supplied application error to a unit-error check failure.
