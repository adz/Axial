---
title: "Refined"
weight: 50
type: docs
---

`Parse` converts serialized strings into primitive values. `Refine` constructs the built-in refined values. `Refine.from` runs the `Refinement` defined for its source and expected destination types. `refine { }` binds parsing and refinement results and stops at the first failure.

## Errors and refined types

- [`Parse.ParseError`](./types/t-parse-parseerror.md): Primitive parse failures returned by <code>Parse</code> helpers.
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

- [`Parse.int`](./parse/m-parse-parse-int.md): Parses a 32-bit integer.
- [`Parse.long`](./parse/m-parse-parse-long.md): Parses a 64-bit integer.
- [`Parse.decimal`](./parse/m-parse-parse-decimal.md): Parses a decimal number.
- [`Parse.float`](./parse/m-parse-parse-float.md): Parses a double-precision floating point number.
- [`Parse.bool`](./parse/m-parse-parse-bool.md): Parses a boolean.
- [`Parse.guid`](./parse/m-parse-parse-guid.md): Parses a GUID.
- [`Parse.dateTime`](./parse/m-parse-parse-datetime.md): Parses a date and time value.
- [`Parse.dateTimeOffset`](./parse/m-parse-parse-datetimeoffset.md): Parses a date and time value with offset.
- [`Parse.dateOnly`](./parse/m-parse-parse-dateonly.md): Parses a date-only value.
- [`Parse.timeOnly`](./parse/m-parse-parse-timeonly.md): Parses a time-only value.
- [`Parse.enum`](./parse/m-parse-parse-enum.md): Parses an enum value by name or numeric text.
- [`Parse.intOption`](./parse/m-parse-parse-intoption.md): Parses an optional integer. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Parse.boolOption`](./parse/m-parse-parse-booloption.md): Parses an optional Boolean. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Parse.decimalOption`](./parse/m-parse-parse-decimaloption.md): Parses an optional decimal. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Parse.guidOption`](./parse/m-parse-parse-guidoption.md): Parses an optional GUID. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Parse.intOrDefault`](./parse/m-parse-parse-intordefault.md): Parses an optional integer, using the supplied fallback only when the input is absent.
- [`Parse.boolOrDefault`](./parse/m-parse-parse-boolordefault.md): Parses an optional Boolean, using the supplied fallback only when the input is absent.
- [`Parse.decimalOrDefault`](./parse/m-parse-parse-decimalordefault.md): Parses an optional decimal, using the supplied fallback only when the input is absent.

## Text

- [`Refined.Text.nonBlankString`](./text/m-refined-text-nonblankstring.md):
- [`Refined.Text.trimmedString`](./text/m-refined-text-trimmedstring.md):
- [`Refined.Text.boundedString`](./text/m-refined-text-boundedstring.md):
- [`Refined.Text.slug`](./text/m-refined-text-slug.md):

## Numeric

- [`Refined.Numeric.positiveInt`](./numeric/m-refined-numeric-positiveint.md):
- [`Refined.Numeric.nonNegativeInt`](./numeric/m-refined-numeric-nonnegativeint.md):
- [`Refined.Numeric.nonZeroInt`](./numeric/m-refined-numeric-nonzeroint.md):
- [`Refined.Numeric.negativeInt`](./numeric/m-refined-numeric-negativeint.md):
- [`Refined.Numeric.nonPositiveInt`](./numeric/m-refined-numeric-nonpositiveint.md):

## Collection

- [`Refined.Collection.nonEmptyList`](./collection/m-refined-collection-nonemptylist.md):
- [`Refined.Collection.nonEmptyArray`](./collection/m-refined-collection-nonemptyarray.md):
- [`Refined.Collection.distinctList`](./collection/m-refined-collection-distinctlist.md):
- [`Refined.Collection.boundedList`](./collection/m-refined-collection-boundedlist.md):
- [`Refined.Collection.boundedArray`](./collection/m-refined-collection-boundedarray.md):
- [`Refined.Collection.exactlyOne`](./collection/m-refined-collection-exactlyone.md):
- [`Refined.Collection.atMostOne`](./collection/m-refined-collection-atmostone.md):

## Temporal

- [`Refined.Temporal.dateTimeOffsetRange`](./temporal/m-refined-temporal-datetimeoffsetrange.md):
- [`Refined.Temporal.dateOnlyRange`](./temporal/m-refined-temporal-dateonlyrange.md):

## Character

- [`Refined.Character.isAsciiDigit`](./character/m-refined-character-isasciidigit.md):
- [`Refined.Character.isAsciiHexDigit`](./character/m-refined-character-isasciihexdigit.md):
- [`Refined.Character.isLowercase`](./character/m-refined-character-islowercase.md):
- [`Refined.Character.isUppercase`](./character/m-refined-character-isuppercase.md):
- [`Refined.Character.isWhitespace`](./character/m-refined-character-iswhitespace.md):
- [`Refined.Character.isControl`](./character/m-refined-character-iscontrol.md):
- [`Refined.Character.isNumeric`](./character/m-refined-character-isnumeric.md):

## Choice

- [`Refined.Choice.orElse`](./choice/m-refined-choice-orelse.md):
- [`Refined.Choice.tryAny`](./choice/m-refined-choice-tryany.md):

## Refinement

- [`Refined.Refinement`](./t-refined-refinement.md):  Defines admission into an invariant-carrying value and its total reverse projection.
- [`Refined.Refinement.define`](./m-refined-refinement-define.md):  Defines a refinement from one portable constraint.
- [`Refined.Refinement.defineAll`](./m-refined-refinement-defineall.md):  Defines a refinement from one or more portable constraints.
- [`Refined.Refinement.defineWithCheck`](./m-refined-refinement-definewithcheck.md):  Defines a metadata-free refinement from an executable check.
- [`Refined.Refinement.create`](./m-refined-refinement-create.md):  Constructs a refined value after its check succeeds.
- [`Refined.Refinement.underlying`](./m-refined-refinement-underlying.md):  Returns the canonical underlying representation.
- [`Refined.Refinement.constraints`](./m-refined-refinement-constraints.md):  Returns portable constraints retained by the refinement.

## Re-certifying helpers

- [`Refined.NonBlankString.value`](./non-blank-string/m-refined-nonblankstring-value.md):
- [`Refined.NonBlankString.create`](./non-blank-string/m-refined-nonblankstring-create.md):
- [`Refined.NonBlankString.map`](./non-blank-string/m-refined-nonblankstring-map.md):
- [`Refined.PositiveInt.value`](./positive-int/m-refined-positiveint-value.md):
- [`Refined.PositiveInt.create`](./positive-int/m-refined-positiveint-create.md):
- [`Refined.PositiveInt.map`](./positive-int/m-refined-positiveint-map.md):
- [`Refined.PositiveInt.replace`](./positive-int/m-refined-positiveint-replace.md):
- [`Refined.NonEmptyList.toList`](./non-empty-list/m-refined-nonemptylist-tolist.md):
- [`Refined.NonEmptyList.create`](./non-empty-list/m-refined-nonemptylist-create.md):
- [`Refined.NonEmptyList.cons`](./non-empty-list/m-refined-nonemptylist-cons.md):
- [`Refined.NonEmptyList.map`](./non-empty-list/m-refined-nonemptylist-map.md):
- [`Refined.NonEmptyList.filter`](./non-empty-list/m-refined-nonemptylist-filter.md):
- [`Refined.NonEmptyList.tryFilter`](./non-empty-list/m-refined-nonemptylist-tryfilter.md):

## Refine facade

- [`Refined.Refine.nonBlankString`](./refine/m-refined-refine-nonblankstring.md):
- [`Refined.Refine.trimmedString`](./refine/m-refined-refine-trimmedstring.md):
- [`Refined.Refine.boundedString`](./refine/m-refined-refine-boundedstring.md):
- [`Refined.Refine.slug`](./refine/m-refined-refine-slug.md):
- [`Refined.Refine.positiveInt`](./refine/m-refined-refine-positiveint.md):
- [`Refined.Refine.nonNegativeInt`](./refine/m-refined-refine-nonnegativeint.md):
- [`Refined.Refine.nonZeroInt`](./refine/m-refined-refine-nonzeroint.md):
- [`Refined.Refine.negativeInt`](./refine/m-refined-refine-negativeint.md):
- [`Refined.Refine.nonPositiveInt`](./refine/m-refined-refine-nonpositiveint.md):
- [`Refined.Refine.nonEmptyList`](./refine/m-refined-refine-nonemptylist.md):
- [`Refined.Refine.nonEmptyArray`](./refine/m-refined-refine-nonemptyarray.md):
- [`Refined.Refine.distinctList`](./refine/m-refined-refine-distinctlist.md):
- [`Refined.Refine.boundedList`](./refine/m-refined-refine-boundedlist.md):
- [`Refined.Refine.boundedArray`](./refine/m-refined-refine-boundedarray.md):
- [`Refined.Refine.dateTimeOffsetRange`](./refine/m-refined-refine-datetimeoffsetrange.md):
- [`Refined.Refine.dateOnlyRange`](./refine/m-refined-refine-dateonlyrange.md):
- [`Refined.Refine.exactlyOne`](./refine/m-refined-refine-exactlyone.md):
- [`Refined.Refine.atMostOne`](./refine/m-refined-refine-atmostone.md):
