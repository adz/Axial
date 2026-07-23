---
title: "Refined"
weight: 500
---

This page shows the `Axial.Refined` surface for turning untrusted boundary data into stronger structural values. `Refine.from` runs the refinement defined for its source and destination types. `Parse` converts serialized strings into primitive values. Focused modules such as `Text`, `Numeric`, `Collection`, and `Temporal` construct named refined values such as `Slug`, `NonZeroInt`, and `DateTimeOffsetRange`. `refine { }` sequences dependent parsing and refinement and stops at the first failure.

## Errors and refined types

- [`Refined.ParseError`](./types/t-refined-parseerror.md): Primitive parse failures returned by <code>Parse</code> helpers.
- [`Refined.RefinementError`](./types/t-refined-refinementerror.md): Structural failures returned by built-in refinement constructors and the <code>refine { }</code> builder.
- [`Refined.NonBlankString`](./types/t-refined-nonblankstring.md): A string that is not null, empty, or whitespace.
- [`Refined.TrimmedString`](./types/t-refined-trimmedstring.md): A string that has no leading or trailing whitespace.
- [`Refined.BoundedString`](./types/t-refined-boundedstring.md): A string whose length is within a caller-supplied inclusive range.
- [`Refined.Slug`](./types/t-refined-slug.md): An ASCII slug containing lowercase letters, digits, and hyphens.
- [`Refined.PositiveInt`](./types/t-refined-positiveint.md): An integer greater than zero.
- [`Refined.NonNegativeInt`](./types/t-refined-nonnegativeint.md): An integer greater than or equal to zero.
- [`Refined.NonZeroInt`](./types/t-refined-nonzeroint.md): An integer that is not zero.
- [`Refined.NegativeInt`](./types/t-refined-negativeint.md): An integer less than zero.
- [`Refined.NonPositiveInt`](./types/t-refined-nonpositiveint.md): An integer less than or equal to zero.
- [`Refined.NonEmptyList`](./types/t-refined-nonemptylist.md): A list that contains at least one item.
- [`Refined.NonEmptyArray`](./types/t-refined-nonemptyarray.md): An array that contains at least one item.
- [`Refined.DistinctList`](./types/t-refined-distinctlist.md): A list with no duplicate items, preserving first-seen order.
- [`Refined.BoundedList`](./types/t-refined-boundedlist.md): A list whose count is within a caller-supplied inclusive range.
- [`Refined.BoundedArray`](./types/t-refined-boundedarray.md): An array whose count is within a caller-supplied inclusive range.
- [`Refined.DateTimeOffsetRange`](./types/t-refined-datetimeoffsetrange.md): A date and time range where <code>Start &lt;= End</code>.
- [`Refined.DateOnlyRange`](./types/t-refined-dateonlyrange.md): A date-only range where <code>Start &lt;= End</code>.

## Parse

- [`Refined.Parse.int`](./parse/m-refined-parse-int.md): Parses a 32-bit integer.
- [`Refined.Parse.long`](./parse/m-refined-parse-long.md): Parses a 64-bit integer.
- [`Refined.Parse.decimal`](./parse/m-refined-parse-decimal.md): Parses a decimal number.
- [`Refined.Parse.float`](./parse/m-refined-parse-float.md): Parses a double-precision floating point number.
- [`Refined.Parse.bool`](./parse/m-refined-parse-bool.md): Parses a boolean.
- [`Refined.Parse.guid`](./parse/m-refined-parse-guid.md): Parses a GUID.
- [`Refined.Parse.dateTime`](./parse/m-refined-parse-datetime.md): Parses a date and time value.
- [`Refined.Parse.dateTimeOffset`](./parse/m-refined-parse-datetimeoffset.md): Parses a date and time value with offset.
- [`Refined.Parse.dateOnly`](./parse/m-refined-parse-dateonly.md): Parses a date-only value.
- [`Refined.Parse.timeOnly`](./parse/m-refined-parse-timeonly.md): Parses a time-only value.
- [`Refined.Parse.enum`](./parse/m-refined-parse-enum.md): Parses an enum value by name or numeric text.
- [`Refined.Parse.intOption`](./parse/m-refined-parse-intoption.md): Parses an optional integer. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Refined.Parse.boolOption`](./parse/m-refined-parse-booloption.md): Parses an optional Boolean. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Refined.Parse.decimalOption`](./parse/m-refined-parse-decimaloption.md): Parses an optional decimal. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Refined.Parse.guidOption`](./parse/m-refined-parse-guidoption.md): Parses an optional GUID. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Refined.Parse.intOrDefault`](./parse/m-refined-parse-intordefault.md): Parses an optional integer, using the supplied fallback only when the input is absent.
- [`Refined.Parse.boolOrDefault`](./parse/m-refined-parse-boolordefault.md): Parses an optional Boolean, using the supplied fallback only when the input is absent.
- [`Refined.Parse.decimalOrDefault`](./parse/m-refined-parse-decimalordefault.md): Parses an optional decimal, using the supplied fallback only when the input is absent.

## Text

- [`Refined.Text.nonBlankString`](./text/m-refined-text-nonblankstring.md): Builds a non-blank string.
- [`Refined.Text.trimmedString`](./text/m-refined-text-trimmedstring.md): Builds a string that has no leading or trailing whitespace.
- [`Refined.Text.boundedString`](./text/m-refined-text-boundedstring.md): Builds a string whose length is within an inclusive range.
- [`Refined.Text.slug`](./text/m-refined-text-slug.md): Builds an ASCII slug made of lowercase letters, digits, and hyphens.

## Numeric

- [`Refined.Numeric.positiveInt`](./numeric/m-refined-numeric-positiveint.md): Builds a positive integer.
- [`Refined.Numeric.nonNegativeInt`](./numeric/m-refined-numeric-nonnegativeint.md): Builds a non-negative integer.
- [`Refined.Numeric.nonZeroInt`](./numeric/m-refined-numeric-nonzeroint.md): Builds a non-zero integer.
- [`Refined.Numeric.negativeInt`](./numeric/m-refined-numeric-negativeint.md): Builds a negative integer.
- [`Refined.Numeric.nonPositiveInt`](./numeric/m-refined-numeric-nonpositiveint.md): Builds a non-positive integer.

## Collection

- [`Refined.Collection.nonEmptyList`](./collection/m-refined-collection-nonemptylist.md): Builds a non-empty list from a sequence.
- [`Refined.Collection.nonEmptyArray`](./collection/m-refined-collection-nonemptyarray.md): Builds a non-empty array from a sequence.
- [`Refined.Collection.distinctList`](./collection/m-refined-collection-distinctlist.md): Builds a list that contains no duplicate items.
- [`Refined.Collection.boundedList`](./collection/m-refined-collection-boundedlist.md): Builds a list whose count is within an inclusive range.
- [`Refined.Collection.boundedArray`](./collection/m-refined-collection-boundedarray.md): Builds an array whose count is within an inclusive range.
- [`Refined.Collection.exactlyOne`](./collection/m-refined-collection-exactlyone.md): Extracts the only item from a sequence.
- [`Refined.Collection.atMostOne`](./collection/m-refined-collection-atmostone.md): Extracts zero or one item from a sequence.

## Temporal

- [`Refined.Temporal.dateTimeOffsetRange`](./temporal/m-refined-temporal-datetimeoffsetrange.md): Builds a date and time range where <code>Start &lt;= End</code>.
- [`Refined.Temporal.dateOnlyRange`](./temporal/m-refined-temporal-dateonlyrange.md): Builds a date-only range where <code>Start &lt;= End</code>.

## Character

- [`Refined.Character.isAsciiDigit`](./character/m-refined-character-isasciidigit.md): Returns true when the character is an ASCII digit.
- [`Refined.Character.isAsciiHexDigit`](./character/m-refined-character-isasciihexdigit.md): Returns true when the character is an ASCII hexadecimal digit.
- [`Refined.Character.isLowercase`](./character/m-refined-character-islowercase.md): Returns true when the character is lowercase according to invariant Unicode casing.
- [`Refined.Character.isUppercase`](./character/m-refined-character-isuppercase.md): Returns true when the character is uppercase according to invariant Unicode casing.
- [`Refined.Character.isWhitespace`](./character/m-refined-character-iswhitespace.md): Returns true when the character is whitespace.
- [`Refined.Character.isControl`](./character/m-refined-character-iscontrol.md): Returns true when the character is a control character.
- [`Refined.Character.isNumeric`](./character/m-refined-character-isnumeric.md): Returns true when the character is numeric according to Unicode character data.

## Choice

- [`Refined.Choice.orElse`](./choice/m-refined-choice-orelse.md): Tries the left parser first, then the right parser, mapping either success into your output type.
- [`Refined.Choice.tryAny`](./choice/m-refined-choice-tryany.md): Tries parser strategies in order and returns the first success.

## Re-certifying helpers

- [`Refined.NonBlankString.value`](./non-blank-string/m-refined-nonblankstring-value.md): Returns the underlying string value.
- [`Refined.NonBlankString.create`](./non-blank-string/m-refined-nonblankstring-create.md): Builds a non-blank string.
- [`Refined.NonBlankString.map`](./non-blank-string/m-refined-nonblankstring-map.md): Transforms the value and re-certifies the non-blank invariant.
- [`Refined.PositiveInt.value`](./positive-int/m-refined-positiveint-value.md): Returns the underlying integer value.
- [`Refined.PositiveInt.create`](./positive-int/m-refined-positiveint-create.md): Builds a positive integer.
- [`Refined.PositiveInt.map`](./positive-int/m-refined-positiveint-map.md): Transforms the value and re-certifies the positive integer invariant.
- [`Refined.PositiveInt.replace`](./positive-int/m-refined-positiveint-replace.md): Replaces the value and re-certifies the positive integer invariant.
- [`Refined.NonEmptyList.toList`](./non-empty-list/m-refined-nonemptylist-tolist.md): Returns the refined value as a standard list.
- [`Refined.NonEmptyList.create`](./non-empty-list/m-refined-nonemptylist-create.md): Builds a non-empty list from a sequence.
- [`Refined.NonEmptyList.cons`](./non-empty-list/m-refined-nonemptylist-cons.md): Prepends a head item to a list, producing a non-empty list without failure.
- [`Refined.NonEmptyList.map`](./non-empty-list/m-refined-nonemptylist-map.md): Transforms each item while preserving non-emptiness.
- [`Refined.NonEmptyList.filter`](./non-empty-list/m-refined-nonemptylist-filter.md): Filters the list, returning a standard list because filtering can remove every item.
- [`Refined.NonEmptyList.tryFilter`](./non-empty-list/m-refined-nonemptylist-tryfilter.md): Filters the list and re-certifies that at least one item remains.

## Refine facade

- [`Refined.Refine.from`](./refine/m-refined-refine-from.md):
 Resolves the <code>Refinement</code> definition for the raw value and expected destination type, then runs its smart
 constructor. A destination type participates by defining a static <code>Refinement</code> member.

- [`Refined.Refine.withCheck`](./refine/m-refined-refine-withcheck.md): Builds a refined value by running a reusable <a href="../../check/t-errorhandling-check.md">Check</a> program
 before calling the constructor. Failures carry the check&#39;s own <a href="../../result/errors/t-errorhandling-checkfailure.md">CheckFailure</a>
 values, so callers never need to reinterpret or re-describe them.
- [`Refined.Refine.withChecks`](./refine/m-refined-refine-withchecks.md): Builds a refined value by running every supplied <a href="../../check/t-errorhandling-check.md">Check</a> program
 before calling the constructor, accumulating all failures via <code>Check.all</code>.
- [`Refined.Refine.nonBlankString`](./refine/m-refined-refine-nonblankstring.md): Builds a non-blank string.
- [`Refined.Refine.trimmedString`](./refine/m-refined-refine-trimmedstring.md): Builds a string that has no leading or trailing whitespace.
- [`Refined.Refine.boundedString`](./refine/m-refined-refine-boundedstring.md): Builds a string whose length is within an inclusive range.
- [`Refined.Refine.slug`](./refine/m-refined-refine-slug.md): Builds an ASCII slug.
- [`Refined.Refine.positiveInt`](./refine/m-refined-refine-positiveint.md): Builds a positive integer.
- [`Refined.Refine.nonNegativeInt`](./refine/m-refined-refine-nonnegativeint.md): Builds a non-negative integer.
- [`Refined.Refine.nonZeroInt`](./refine/m-refined-refine-nonzeroint.md): Builds a non-zero integer.
- [`Refined.Refine.negativeInt`](./refine/m-refined-refine-negativeint.md): Builds a negative integer.
- [`Refined.Refine.nonPositiveInt`](./refine/m-refined-refine-nonpositiveint.md): Builds a non-positive integer.
- [`Refined.Refine.nonEmptyList`](./refine/m-refined-refine-nonemptylist.md): Builds a non-empty list from a sequence.
- [`Refined.Refine.nonEmptyArray`](./refine/m-refined-refine-nonemptyarray.md): Builds a non-empty array from a sequence.
- [`Refined.Refine.distinctList`](./refine/m-refined-refine-distinctlist.md): Builds a distinct list from a sequence.
- [`Refined.Refine.boundedList`](./refine/m-refined-refine-boundedlist.md): Builds a bounded list from a sequence.
- [`Refined.Refine.boundedArray`](./refine/m-refined-refine-boundedarray.md): Builds a bounded array from a sequence.
- [`Refined.Refine.dateTimeOffsetRange`](./refine/m-refined-refine-datetimeoffsetrange.md): Builds a date and time range where <code>Start &lt;= End</code>.
- [`Refined.Refine.dateOnlyRange`](./refine/m-refined-refine-dateonlyrange.md): Builds a date-only range where <code>Start &lt;= End</code>.
- [`Refined.Refine.exactlyOne`](./refine/m-refined-refine-exactlyone.md): Extracts the only item from a sequence.
- [`Refined.Refine.atMostOne`](./refine/m-refined-refine-atmostone.md): Extracts zero or one item from a sequence.

## Builder

- [`refine`](./refine-ce/p-refined--refine.md):
 The fail-fast <code>refine { }</code> computation expression. A raw value can be parsed or refined according to the
 type annotation on the left side of <code>let!</code>; explicit <code>Parse</code> and <code>Refine</code> results also bind directly.
