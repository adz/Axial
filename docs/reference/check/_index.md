---
title: "Check"
weight: 70
---

This page shows the `Check` surface for reusable, path-free value constraints. `Check.*` helpers return `Result<unit, CheckFailure list>` and can be composed with `Check.all`, `Check.any`, `Check.not`, and `Check.mapFailure`. Use `Predicate.*` helpers when a local branch needs a raw boolean, and use `Result.require` or `Result.guard` when a `Check<'value>` should become a fail-fast result that preserves the checked value.

## Core types

- [`ErrorHandling.Check`](./t-errorhandling-check.md):
 Typed value-check programs for local structural facts.

- [`ErrorHandling.CheckFailure`](./t-errorhandling-checkfailure.md): Describes why an executable value check failed, without attaching source paths or raw input.
- [`ErrorHandling.CheckLengthExpectation`](./t-errorhandling-checklengthexpectation.md): Describes the length requirement that a value check expected a string-like value to satisfy.
- [`ErrorHandling.CheckRangeExpectation`](./t-errorhandling-checkrangeexpectation.md): <pre>Describes the ordering requirement that a value check expected a comparable value to satisfy against a
 caller-supplied bound.</pre>
- [`ErrorHandling.CheckCountExpectation`](./t-errorhandling-checkcountexpectation.md): <pre>Describes the count requirement that a value check expected a sequence-shaped value to satisfy against a
 caller-supplied count.</pre>

## Executable composition

- [`ErrorHandling.Check.all`](./m-errorhandling-check-all.md): Combines checks conjunctively by running every check against the value and accumulating all failures. An empty list succeeds.
- [`ErrorHandling.Check.any`](./m-errorhandling-check-any.md): Combines checks disjunctively by running checks until one succeeds, or returns accumulated failures when every check fails. An empty list fails with no failures.
- [`ErrorHandling.Check.``not```](./m-errorhandling-check-not.md): Inverts a check. A successful inner check becomes a custom-code failure, while any failed inner check succeeds.
- [`ErrorHandling.Check.mapFailure`](./m-errorhandling-check-mapfailure.md): Maps every failure produced by a check.

## Top-level executable checks

- [`ErrorHandling.Check.present`](./m-errorhandling-check-present.md): Runs the type-directed presence check for an already parsed optional, nullable, or text value.
- [`ErrorHandling.Check.empty`](./m-errorhandling-check-empty.md):  Runs the type-directed empty check for an already parsed optional, nullable, text, or supported sequence-shaped value.
- [`ErrorHandling.Check.notEmpty`](./m-errorhandling-check-notempty.md):  Runs the type-directed non-empty check for an already parsed optional, nullable, text, or supported sequence-shaped value.
- [`ErrorHandling.Check.length`](./m-errorhandling-check-length.md): Returns a string check requiring exactly the supplied length.
- [`ErrorHandling.Check.minLength`](./m-errorhandling-check-minlength.md): Returns a string check requiring at least the supplied length.
- [`ErrorHandling.Check.maxLength`](./m-errorhandling-check-maxlength.md): Returns a string check requiring at most the supplied length.
- [`ErrorHandling.Check.lengthBetween`](./m-errorhandling-check-lengthbetween.md): Returns a string check requiring a length inside the supplied inclusive bounds.
- [`ErrorHandling.Check.email`](./m-errorhandling-check-email.md): Runs Axial's pragmatic email-format check against an already parsed string value.
- [`ErrorHandling.Check.matches`](./m-errorhandling-check-matches.md): Returns a string check requiring a match for the supplied regular expression pattern.
- [`ErrorHandling.Check.oneOf`](./m-errorhandling-check-oneof.md): Returns a string check requiring equality with one of the supplied choices.
- [`ErrorHandling.Check.between`](./m-errorhandling-check-between.md): Returns an ordered-value check requiring a value inside the supplied inclusive bounds.
- [`ErrorHandling.Check.greaterThan`](./m-errorhandling-check-greaterthan.md): Returns an ordered-value check requiring a value greater than the supplied exclusive lower bound.
- [`ErrorHandling.Check.lessThan`](./m-errorhandling-check-lessthan.md): Returns an ordered-value check requiring a value less than the supplied exclusive upper bound.
- [`ErrorHandling.Check.atLeast`](./m-errorhandling-check-atleast.md): Returns an ordered-value check requiring a value greater than or equal to the supplied lower bound.
- [`ErrorHandling.Check.atMost`](./m-errorhandling-check-atmost.md): Returns an ordered-value check requiring a value less than or equal to the supplied upper bound.
- [`ErrorHandling.Check.positive`](./m-errorhandling-check-positive.md): Runs an ordered-value check requiring a value greater than zero.
- [`ErrorHandling.Check.nonNegative`](./m-errorhandling-check-nonnegative.md): Runs an ordered-value check requiring a value greater than or equal to zero.
- [`ErrorHandling.Check.negative`](./m-errorhandling-check-negative.md): Runs an ordered-value check requiring a value less than zero.
- [`ErrorHandling.Check.nonPositive`](./m-errorhandling-check-nonpositive.md): Runs an ordered-value check requiring a value less than or equal to zero.
- [`ErrorHandling.Check.count`](./m-errorhandling-check-count.md): Returns a sequence-shaped check requiring exactly the supplied count.
- [`ErrorHandling.Check.minCount`](./m-errorhandling-check-mincount.md): Returns a sequence-shaped check requiring at least the supplied count.
- [`ErrorHandling.Check.maxCount`](./m-errorhandling-check-maxcount.md): Returns a sequence-shaped check requiring at most the supplied count.
- [`ErrorHandling.Check.countBetween`](./m-errorhandling-check-countbetween.md): Returns a sequence-shaped check requiring a count inside the supplied inclusive bounds.
- [`ErrorHandling.Check.distinct`](./m-errorhandling-check-distinct.md): Runs a sequence-shaped check requiring no duplicate values.
- [`ErrorHandling.Check.contains`](./m-errorhandling-check-contains.md): Returns a sequence-shaped check requiring the supplied value to be present.
- [`ErrorHandling.Check.single`](./m-errorhandling-check-single.md): Runs a sequence-shaped check requiring exactly one item.
- [`ErrorHandling.Check.atMostOne`](./m-errorhandling-check-atmostone.md): Runs a sequence-shaped check requiring zero or one item.
- [`ErrorHandling.Check.atLeastOne`](./m-errorhandling-check-atleastone.md): Runs a sequence-shaped check requiring at least one item.
- [`ErrorHandling.Check.moreThanOne`](./m-errorhandling-check-morethanone.md): Runs a sequence-shaped check requiring more than one item.
- [`ErrorHandling.Check.equalTo`](./m-errorhandling-check-equalto.md): Returns a value check requiring equality with the supplied expected value.
- [`ErrorHandling.Check.notEqualTo`](./m-errorhandling-check-notequalto.md): Returns a value check requiring inequality with the supplied unexpected value.

## Executable string checks

- [`ErrorHandling.Check.String.present`](./m-errorhandling-check-string-present.md): Requires an already parsed string value to be non-null and contain at least one non-whitespace character.
- [`ErrorHandling.Check.String.empty`](./m-errorhandling-check-string-empty.md): Requires an already parsed string value to be exactly empty. Null fails as a missing value.
- [`ErrorHandling.Check.String.notEmpty`](./m-errorhandling-check-string-notempty.md): Requires an already parsed string value to contain at least one character. Whitespace counts as present text.
- [`ErrorHandling.Check.String.minLength`](./m-errorhandling-check-string-minlength.md): Requires an already parsed string value to have at least the supplied length. Null fails with an unknown actual length.
- [`ErrorHandling.Check.String.maxLength`](./m-errorhandling-check-string-maxlength.md): Requires an already parsed string value to have at most the supplied length. Null fails with an unknown actual length.
- [`ErrorHandling.Check.String.lengthBetween`](./m-errorhandling-check-string-lengthbetween.md): Requires an already parsed string value length to lie inside the supplied inclusive bounds. Null fails with an unknown actual length.
- [`ErrorHandling.Check.String.exactLength`](./m-errorhandling-check-string-exactlength.md): Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.
- [`ErrorHandling.Check.String.email`](./m-errorhandling-check-string-email.md): Requires an already parsed string value to match Axial's pragmatic email format.
- [`ErrorHandling.Check.String.matches`](./m-errorhandling-check-string-matches.md): Requires an already parsed string value to match the supplied regular expression pattern.
- [`ErrorHandling.Check.String.numeric`](./m-errorhandling-check-string-numeric.md): Requires an already parsed string value to contain one or more numeric characters.
- [`ErrorHandling.Check.String.alphaNumeric`](./m-errorhandling-check-string-alphanumeric.md): Requires an already parsed string value to contain one or more letter or digit characters.
- [`ErrorHandling.Check.String.oneOf`](./m-errorhandling-check-string-oneof.md): Requires an already parsed string value to equal one of the supplied choices. Null fails with an unknown actual value.

## Executable number checks

- [`ErrorHandling.Check.Number.between`](./m-errorhandling-check-number-between.md): Requires a value to lie inside the supplied inclusive bounds.
- [`ErrorHandling.Check.Number.greaterThan`](./m-errorhandling-check-number-greaterthan.md): Requires a value to be greater than the supplied exclusive lower bound.
- [`ErrorHandling.Check.Number.lessThan`](./m-errorhandling-check-number-lessthan.md): Requires a value to be less than the supplied exclusive upper bound.
- [`ErrorHandling.Check.Number.atLeast`](./m-errorhandling-check-number-atleast.md): Requires a value to be greater than or equal to the supplied lower bound.
- [`ErrorHandling.Check.Number.atMost`](./m-errorhandling-check-number-atmost.md): Requires a value to be less than or equal to the supplied upper bound.
- [`ErrorHandling.Check.Number.positive`](./m-errorhandling-check-number-positive.md): Requires a value to be greater than zero.
- [`ErrorHandling.Check.Number.nonNegative`](./m-errorhandling-check-number-nonnegative.md): Requires a value to be greater than or equal to zero.
- [`ErrorHandling.Check.Number.negative`](./m-errorhandling-check-number-negative.md): Requires a value to be less than zero.
- [`ErrorHandling.Check.Number.nonPositive`](./m-errorhandling-check-number-nonpositive.md): Requires a value to be less than or equal to zero.

## Executable sequence checks

- [`ErrorHandling.Check.Seq.empty`](./m-errorhandling-check-seq-empty.md): Requires an already parsed sequence-shaped value to contain no items. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.notEmpty`](./m-errorhandling-check-seq-notempty.md): Requires an already parsed sequence-shaped value to contain at least one item. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.count`](./m-errorhandling-check-seq-count.md): Requires an already parsed sequence-shaped value to contain exactly the supplied count. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.minCount`](./m-errorhandling-check-seq-mincount.md): Requires an already parsed sequence-shaped value to contain at least the supplied count. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.maxCount`](./m-errorhandling-check-seq-maxcount.md): Requires an already parsed sequence-shaped value to contain at most the supplied count. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.countBetween`](./m-errorhandling-check-seq-countbetween.md): Requires an already parsed sequence-shaped value count to lie inside the supplied inclusive bounds. Null fails with an unknown actual count.
- [`ErrorHandling.Check.Seq.noDuplicates`](./m-errorhandling-check-seq-noduplicates.md): Requires an already parsed sequence-shaped value to contain no duplicate values.
- [`ErrorHandling.Check.Seq.contains`](./m-errorhandling-check-seq-contains.md): Requires an already parsed sequence-shaped value to contain the supplied value.
- [`ErrorHandling.Check.Seq.single`](./m-errorhandling-check-seq-single.md): Requires an already parsed sequence-shaped value to contain exactly one item.
- [`ErrorHandling.Check.Seq.atMostOne`](./m-errorhandling-check-seq-atmostone.md): Requires an already parsed sequence-shaped value to contain zero or one item.
- [`ErrorHandling.Check.Seq.atLeastOne`](./m-errorhandling-check-seq-atleastone.md): Requires an already parsed sequence-shaped value to contain at least one item.
- [`ErrorHandling.Check.Seq.moreThanOne`](./m-errorhandling-check-seq-morethanone.md): Requires an already parsed sequence-shaped value to contain more than one item.

## Executable optional checks

- [`ErrorHandling.Check.Option.some`](./m-errorhandling-check-option-some.md): Requires an option to contain a value.
- [`ErrorHandling.Check.Option.none`](./m-errorhandling-check-option-none.md): Requires an option to contain no value.
- [`ErrorHandling.Check.ValueOption.some`](./m-errorhandling-check-valueoption-some.md): Requires a value option to contain a value.
- [`ErrorHandling.Check.ValueOption.none`](./m-errorhandling-check-valueoption-none.md): Requires a value option to contain no value.
- [`ErrorHandling.Check.Nullable.hasValue`](./m-errorhandling-check-nullable-hasvalue.md): Requires a nullable value to contain a value.
- [`ErrorHandling.Check.Nullable.hasNoValue`](./m-errorhandling-check-nullable-hasnovalue.md): Requires a nullable value to contain no value.
- [`ErrorHandling.Check.Result.ok`](./m-errorhandling-check-result-ok.md): Requires a result to contain a successful value.
- [`ErrorHandling.Check.Result.error`](./m-errorhandling-check-result-error.md): Requires a result to contain an error value.

## Type-directed presence facade

- [`ErrorHandling.Predicate.present`](./m-errorhandling-predicate-present.md): Runs the type-directed presence predicate for an already parsed optional, nullable, or text value.
- [`ErrorHandling.Predicate.empty`](./m-errorhandling-predicate-empty.md):  Runs the type-directed empty predicate for an already parsed optional, nullable, text, or supported
 sequence-shaped value.
- [`ErrorHandling.Predicate.notEmpty`](./m-errorhandling-predicate-notempty.md):  Runs the type-directed non-empty predicate for an already parsed optional, nullable, text, or supported
 sequence-shaped value.

## Option and result predicates

- [`Microsoft.FSharp.Core.FSharpOption.IsPresent`](./m-errorhandling-predicate-option-get_ispresent.md): True when the option contains a value.
- [`Microsoft.FSharp.Core.FSharpOption.IsAbsent`](./m-errorhandling-predicate-option-get_isabsent.md): True when the option contains no value.
- [`Microsoft.FSharp.Core.FSharpValueOption.IsPresent`](./m-errorhandling-predicate-valueoption-get_ispresent.md): True when the value option contains a value.
- [`Microsoft.FSharp.Core.FSharpValueOption.IsAbsent`](./m-errorhandling-predicate-valueoption-get_isabsent.md): True when the value option contains no value.
- [`Microsoft.FSharp.Core.FSharpResult.IsOk`](./m-errorhandling-predicate-result-isok.md): True when the result is successful.
- [`Microsoft.FSharp.Core.FSharpResult.IsError`](./m-errorhandling-predicate-result-iserror.md): True when the result is failed.

## Presence predicates

- [`System.Nullable.IsPresent`](./m-errorhandling-predicate-nullable-get_ispresent.md): True when the nullable value contains a value.
- [`System.Nullable.IsAbsent`](./m-errorhandling-predicate-nullable-get_isabsent.md): True when the nullable value contains no value.
- [`ErrorHandling.Predicate.Reference.notNull`](./m-errorhandling-predicate-reference-notnull.md): Returns true when the reference is not null.
- [`ErrorHandling.Predicate.Reference.isNull`](./m-errorhandling-predicate-reference-isnull.md): Returns true when the reference is null.

## String predicates

- [`System.String.IsEmpty`](./m-errorhandling-predicate-string-isempty.md): True when the string is exactly empty and non-null.
- [`System.String.IsNotEmpty`](./m-errorhandling-predicate-string-isnotempty.md): True when the string has at least one character and is non-null.
- [`System.String.IsBlank`](./m-errorhandling-predicate-string-isblank.md): True when the string is null, empty, or whitespace.
- [`System.String.IsNotBlank`](./m-errorhandling-predicate-string-isnotblank.md): True when the string is non-null and contains at least one non-whitespace character.
- [`System.String.HasMinLength`](./m-errorhandling-predicate-string-hasminlength.md): True when the string length is at least the supplied minimum.
- [`System.String.HasMaxLength`](./m-errorhandling-predicate-string-hasmaxlength.md): True when the string length is at most the supplied maximum.
- [`System.String.HasLength`](./m-errorhandling-predicate-string-haslength.md): True when the string length equals the supplied expected length.
- [`System.String.HasLengthBetween`](./m-errorhandling-predicate-string-haslengthbetween.md): True when the string length lies inside the supplied inclusive bounds.
- [`System.String.MatchesPattern`](./m-errorhandling-predicate-string-matchespattern.md): True when the string matches the supplied regular expression pattern.
- [`System.String.IsEmail`](./m-errorhandling-predicate-string-isemail.md): True when the string matches Axial's pragmatic email format.
- [`System.String.IsNumeric`](./m-errorhandling-predicate-string-isnumeric.md): True when the string contains only numeric characters.
- [`System.String.IsAlphaNumeric`](./m-errorhandling-predicate-string-isalphanumeric.md): True when the string contains only letter or digit characters.

## Sequence predicates

- [`System.Collections.Generic.IEnumerable.HasNoItems`](./m-errorhandling-predicate-ienumerable-hasnoitems.md): True when the sequence is non-null and empty.
- [`System.Collections.Generic.IEnumerable.HasItems`](./m-errorhandling-predicate-ienumerable-hasitems.md): True when the sequence is non-null and contains at least one item.
- [`System.Collections.Generic.IEnumerable.HasItem`](./m-errorhandling-predicate-ienumerable-hasitem.md): True when the sequence is non-null and contains the supplied value.
- [`System.Collections.Generic.IEnumerable.HasCount`](./m-errorhandling-predicate-ienumerable-hascount.md): True when the sequence is non-null and contains exactly the supplied count.
- [`System.Collections.Generic.IEnumerable.HasMinCount`](./m-errorhandling-predicate-ienumerable-hasmincount.md): True when the sequence is non-null and contains at least the supplied count.
- [`System.Collections.Generic.IEnumerable.HasMaxCount`](./m-errorhandling-predicate-ienumerable-hasmaxcount.md): True when the sequence is non-null and contains at most the supplied count.
- [`System.Collections.Generic.IEnumerable.HasCountBetween`](./m-errorhandling-predicate-ienumerable-hascountbetween.md): True when the sequence is non-null and its count lies inside the supplied inclusive bounds.
- [`System.Collections.Generic.IEnumerable.HasSingleItem`](./m-errorhandling-predicate-ienumerable-hassingleitem.md): True when the sequence is non-null and contains exactly one item.
- [`System.Collections.Generic.IEnumerable.HasAtMostOneItem`](./m-errorhandling-predicate-ienumerable-hasatmostoneitem.md): True when the sequence is non-null and contains zero or one item.
- [`System.Collections.Generic.IEnumerable.HasMoreThanOneItem`](./m-errorhandling-predicate-ienumerable-hasmorethanoneitem.md): True when the sequence is non-null and contains more than one item.
- [`System.Collections.Generic.IEnumerable.HasDuplicates`](./m-errorhandling-predicate-ienumerable-hasduplicates.md): True when the sequence is non-null and contains duplicate values.
- [`System.Collections.Generic.IEnumerable.IsDistinct`](./m-errorhandling-predicate-ienumerable-isdistinct.md): True when the sequence is non-null and contains no duplicate values.

## Comparison predicates

- [`ErrorHandling.Predicate.Number.greaterThan`](./m-errorhandling-predicate-number-greaterthan.md): Returns true when the value is greater than the supplied exclusive lower bound.
- [`ErrorHandling.Predicate.Number.lessThan`](./m-errorhandling-predicate-number-lessthan.md): Returns true when the value is less than the supplied exclusive upper bound.
- [`ErrorHandling.Predicate.Number.atLeast`](./m-errorhandling-predicate-number-atleast.md): Returns true when the value is greater than or equal to the supplied lower bound.
- [`ErrorHandling.Predicate.Number.atMost`](./m-errorhandling-predicate-number-atmost.md): Returns true when the value is less than or equal to the supplied upper bound.
- [`ErrorHandling.Predicate.Number.between`](./m-errorhandling-predicate-number-between.md): Returns true when the value lies inside the supplied inclusive bounds.
- [`ErrorHandling.Predicate.Number.positive`](./m-errorhandling-predicate-number-positive.md): Returns true when the value is greater than zero.
- [`ErrorHandling.Predicate.Number.nonNegative`](./m-errorhandling-predicate-number-nonnegative.md): Returns true when the value is greater than or equal to zero.
- [`ErrorHandling.Predicate.Number.negative`](./m-errorhandling-predicate-number-negative.md): Returns true when the value is less than zero.
- [`ErrorHandling.Predicate.Number.nonPositive`](./m-errorhandling-predicate-number-nonpositive.md): Returns true when the value is less than or equal to zero.
