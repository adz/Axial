---
title: "Predicate"
weight: 15
type: docs
---

This page shows the `Predicate` and `PredicateExtensions` surface: plain `bool` facts for local branching (`if`, `match`, guard clauses), as opposed to [`Check`](../check/), which returns a structured `Result`. `PredicateExtensions` is `AutoOpen`, adding members such as `IsBlank`, `IsPresent`, and `HasItems` directly onto the types they describe. `Predicate.present`, `Predicate.empty`, and `Predicate.notEmpty` are the `bool`-returning counterparts to `Check.present`/`Check.empty`/`Check.notEmpty`, using the same type-directed SRTP dispatch.

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
