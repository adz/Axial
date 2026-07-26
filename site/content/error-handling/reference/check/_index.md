---
title: "Check"
weight: 10
type: docs
---

This page shows the `Check` surface for reusable, path-free value constraints. `Check.*` helpers return `Result<'value, CheckFailure list>`: a passing check hands back the same value unchanged, so it pipes directly into the next step. They compose with `Check.all`, `Check.any`, `Check.not`, and `Check.mapFailure`. Use [`Predicate`](../predicate/) when a local branch needs a raw boolean instead of a structured result. `Axial.Check.CheckDSL` opens the deduplicated root names unqualified for use inside a validation module; `not`, `contains`, `distinct`, `all`, `any`, `length`, and `between` stay reachable only as `Check.___` there, since they shadow FSharp.Core names.

## Core types

- [`Check`](./t-check-check.md):
 Typed value-check programs for local structural facts.

- [`Check.CheckFailure`](./t-check-checkfailure.md): Describes why an executable value check failed, without attaching source paths or structured data.
- [`Check.CheckLengthExpectation`](./t-check-checklengthexpectation.md): Describes the length requirement that a value check expected a string-like value to satisfy.
- [`Check.CheckRangeExpectation`](./t-check-checkrangeexpectation.md): <pre>Describes the ordering requirement that a value check expected a comparable value to satisfy against a
 caller-supplied bound.</pre>
- [`Check.CheckCountExpectation`](./t-check-checkcountexpectation.md): <pre>Describes the count requirement that a value check expected a sequence-shaped value to satisfy against a
 caller-supplied count.</pre>

## Executable composition

- [`Check.all`](./m-check-check-all.md): Combines checks conjunctively by running every check against the value and accumulating all failures. An empty list succeeds.
- [`Check.any`](./m-check-check-any.md): Combines checks disjunctively by running checks until one succeeds, or returns accumulated failures when every check fails. An empty list fails with no failures.
- [`Check.``not```](./m-check-check-not.md): Inverts a check. A successful inner check becomes a custom-code failure, while any failed inner check succeeds.
- [`Check.mapFailure`](./m-check-check-mapfailure.md): Maps every failure produced by a check.

## Top-level executable checks

- [`Check.present`](./m-check-check-present.md): Runs the type-directed presence check for an already parsed optional, nullable, text, or sequence-shaped value.
- [`Check.empty`](./m-check-check-empty.md):  Runs the type-directed empty check for an already parsed optional, nullable, text, or supported sequence-shaped value.
- [`Check.notEmpty`](./m-check-check-notempty.md):  Runs the type-directed non-empty check for an already parsed optional, nullable, text, or supported sequence-shaped value.
- [`Check.length`](./m-check-check-length.md): Returns a string check requiring exactly the supplied length.
- [`Check.minLength`](./m-check-check-minlength.md): Returns a string check requiring at least the supplied length.
- [`Check.maxLength`](./m-check-check-maxlength.md): Returns a string check requiring at most the supplied length.
- [`Check.lengthBetween`](./m-check-check-lengthbetween.md): Returns a string check requiring a length inside the supplied inclusive bounds.
- [`Check.email`](./m-check-check-email.md): Runs Axial's pragmatic email-format check against an already parsed string value.
- [`Check.matches`](./m-check-check-matches.md): Returns a string check requiring a match for the supplied regular expression pattern.
- [`Check.oneOf`](./m-check-check-oneof.md): Returns a string check requiring equality with one of the supplied choices.
- [`Check.between`](./m-check-check-between.md): Returns an ordered-value check requiring a value inside the supplied inclusive bounds.
- [`Check.greaterThan`](./m-check-check-greaterthan.md): Returns an ordered-value check requiring a value greater than the supplied exclusive lower bound.
- [`Check.lessThan`](./m-check-check-lessthan.md): Returns an ordered-value check requiring a value less than the supplied exclusive upper bound.
- [`Check.atLeast`](./m-check-check-atleast.md): Returns an ordered-value check requiring a value greater than or equal to the supplied lower bound.
- [`Check.atMost`](./m-check-check-atmost.md): Returns an ordered-value check requiring a value less than or equal to the supplied upper bound.
- [`Check.positive`](./m-check-check-positive.md): Runs an ordered-value check requiring a value greater than zero.
- [`Check.nonNegative`](./m-check-check-nonnegative.md): Runs an ordered-value check requiring a value greater than or equal to zero.
- [`Check.negative`](./m-check-check-negative.md): Runs an ordered-value check requiring a value less than zero.
- [`Check.nonPositive`](./m-check-check-nonpositive.md): Runs an ordered-value check requiring a value less than or equal to zero.
- [`Check.count`](./m-check-check-count.md): Returns a sequence-shaped check requiring exactly the supplied count.
- [`Check.minCount`](./m-check-check-mincount.md): Returns a sequence-shaped check requiring at least the supplied count.
- [`Check.maxCount`](./m-check-check-maxcount.md): Returns a sequence-shaped check requiring at most the supplied count.
- [`Check.countBetween`](./m-check-check-countbetween.md): Returns a sequence-shaped check requiring a count inside the supplied inclusive bounds.
- [`Check.distinct`](./m-check-check-distinct.md): Runs a sequence-shaped check requiring no duplicate values.
- [`Check.contains`](./m-check-check-contains.md): Returns a sequence-shaped check requiring the supplied value to be present.
- [`Check.single`](./m-check-check-single.md): Runs a sequence-shaped check requiring exactly one item.
- [`Check.atMostOne`](./m-check-check-atmostone.md): Runs a sequence-shaped check requiring zero or one item.
- [`Check.atLeastOne`](./m-check-check-atleastone.md): Runs a sequence-shaped check requiring at least one item.
- [`Check.moreThanOne`](./m-check-check-morethanone.md): Runs a sequence-shaped check requiring more than one item.
- [`Check.equalTo`](./m-check-check-equalto.md): Returns a value check requiring equality with the supplied expected value.
- [`Check.notEqualTo`](./m-check-check-notequalto.md): Returns a value check requiring inequality with the supplied unexpected value.

## Executable string checks

- [`Check.String.present`](./m-check-check-string-present.md): Requires an already parsed string value to be non-null and contain at least one non-whitespace character.
- [`Check.String.empty`](./m-check-check-string-empty.md): Requires an already parsed string value to be exactly empty. Null fails as a missing value.
- [`Check.String.notEmpty`](./m-check-check-string-notempty.md): Requires an already parsed string value to contain at least one character. Whitespace counts as present text.
- [`Check.String.minLength`](./m-check-check-string-minlength.md): Requires an already parsed string value to have at least the supplied length. Null fails with an unknown actual length.
- [`Check.String.maxLength`](./m-check-check-string-maxlength.md): Requires an already parsed string value to have at most the supplied length. Null fails with an unknown actual length.
- [`Check.String.lengthBetween`](./m-check-check-string-lengthbetween.md): Requires an already parsed string value length to lie inside the supplied inclusive bounds. Null fails with an unknown actual length.
- [`Check.String.exactLength`](./m-check-check-string-exactlength.md): Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.
- [`Check.String.email`](./m-check-check-string-email.md): Requires an already parsed string value to match Axial's pragmatic email format.
- [`Check.String.matches`](./m-check-check-string-matches.md): Requires an already parsed string value to match the supplied regular expression pattern.
- [`Check.String.numeric`](./m-check-check-string-numeric.md): Requires an already parsed string value to contain one or more numeric characters.
- [`Check.String.alphaNumeric`](./m-check-check-string-alphanumeric.md): Requires an already parsed string value to contain one or more letter or digit characters.
- [`Check.String.oneOf`](./m-check-check-string-oneof.md): Requires an already parsed string value to equal one of the supplied choices. Null fails with an unknown actual value.

## Executable number checks

- [`Check.Number.between`](./m-check-check-number-between.md): Requires a value to lie inside the supplied inclusive bounds.
- [`Check.Number.greaterThan`](./m-check-check-number-greaterthan.md): Requires a value to be greater than the supplied exclusive lower bound.
- [`Check.Number.lessThan`](./m-check-check-number-lessthan.md): Requires a value to be less than the supplied exclusive upper bound.
- [`Check.Number.atLeast`](./m-check-check-number-atleast.md): Requires a value to be greater than or equal to the supplied lower bound.
- [`Check.Number.atMost`](./m-check-check-number-atmost.md): Requires a value to be less than or equal to the supplied upper bound.
- [`Check.Number.positive`](./m-check-check-number-positive.md): Requires a value to be greater than zero.
- [`Check.Number.nonNegative`](./m-check-check-number-nonnegative.md): Requires a value to be greater than or equal to zero.
- [`Check.Number.negative`](./m-check-check-number-negative.md): Requires a value to be less than zero.
- [`Check.Number.nonPositive`](./m-check-check-number-nonpositive.md): Requires a value to be less than or equal to zero.

## Executable sequence checks

- [`Check.Seq.empty`](./m-check-check-seq-empty.md): Requires an already parsed sequence-shaped value to contain no items. Null fails with an unknown actual count.
- [`Check.Seq.notEmpty`](./m-check-check-seq-notempty.md): Requires an already parsed sequence-shaped value to contain at least one item. Null fails with an unknown actual count.
- [`Check.Seq.count`](./m-check-check-seq-count.md): Requires an already parsed sequence-shaped value to contain exactly the supplied count. Null fails with an unknown actual count.
- [`Check.Seq.minCount`](./m-check-check-seq-mincount.md): Requires an already parsed sequence-shaped value to contain at least the supplied count. Null fails with an unknown actual count.
- [`Check.Seq.maxCount`](./m-check-check-seq-maxcount.md): Requires an already parsed sequence-shaped value to contain at most the supplied count. Null fails with an unknown actual count.
- [`Check.Seq.countBetween`](./m-check-check-seq-countbetween.md): Requires an already parsed sequence-shaped value count to lie inside the supplied inclusive bounds. Null fails with an unknown actual count.
- [`Check.Seq.noDuplicates`](./m-check-check-seq-noduplicates.md): Requires an already parsed sequence-shaped value to contain no duplicate values.
- [`Check.Seq.contains`](./m-check-check-seq-contains.md): Requires an already parsed sequence-shaped value to contain the supplied value.
- [`Check.Seq.single`](./m-check-check-seq-single.md): Requires an already parsed sequence-shaped value to contain exactly one item.
- [`Check.Seq.atMostOne`](./m-check-check-seq-atmostone.md): Requires an already parsed sequence-shaped value to contain zero or one item.
- [`Check.Seq.atLeastOne`](./m-check-check-seq-atleastone.md): Requires an already parsed sequence-shaped value to contain at least one item.
- [`Check.Seq.moreThanOne`](./m-check-check-seq-morethanone.md): Requires an already parsed sequence-shaped value to contain more than one item.

## Executable optional checks

- [`Check.Option.some`](./m-check-check-option-some.md): Requires an option to contain a value.
- [`Check.Option.none`](./m-check-check-option-none.md): Requires an option to contain no value.
- [`Check.ValueOption.some`](./m-check-check-valueoption-some.md): Requires a value option to contain a value.
- [`Check.ValueOption.none`](./m-check-check-valueoption-none.md): Requires a value option to contain no value.
- [`Check.Nullable.hasValue`](./m-check-check-nullable-hasvalue.md): Requires a nullable value to contain a value.
- [`Check.Nullable.hasNoValue`](./m-check-check-nullable-hasnovalue.md): Requires a nullable value to contain no value.
- [`Check.Result.ok`](./m-check-check-result-ok.md): Requires a result to contain a successful value.
- [`Check.Result.error`](./m-check-check-result-error.md): Requires a result to contain an error value.
