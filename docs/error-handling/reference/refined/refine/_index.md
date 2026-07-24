---
title: "Refine"
---

`Refine` contains type-directed construction and the common built-in refinement functions.

- [`Refined.Refine.from`](./m-refined-refine-from.md):
 Resolves the <code>Refinement</code> definition for the raw value and expected destination type, then runs its smart
 constructor. A destination type participates by defining a static <code>Refinement</code> member.

- [`Refined.Refine.withCheck`](./m-refined-refine-withcheck.md): Runs a <a href="../../check/t-errorhandling-check.md">Check</a> and calls the constructor when the check succeeds.
- [`Refined.Refine.withChecks`](./m-refined-refine-withchecks.md): Runs the supplied <a href="../../check/t-errorhandling-check.md">Check</a> values with <code>Check.all</code> and calls the
 constructor when every check succeeds.
- [`Refined.Refine.nonBlankString`](./m-refined-refine-nonblankstring.md): Builds a non-blank string.
- [`Refined.Refine.trimmedString`](./m-refined-refine-trimmedstring.md): Builds a string that has no leading or trailing whitespace.
- [`Refined.Refine.boundedString`](./m-refined-refine-boundedstring.md): Builds a string whose length is within an inclusive range.
- [`Refined.Refine.slug`](./m-refined-refine-slug.md): Builds an ASCII slug.
- [`Refined.Refine.positiveInt`](./m-refined-refine-positiveint.md): Builds a positive integer.
- [`Refined.Refine.nonNegativeInt`](./m-refined-refine-nonnegativeint.md): Builds a non-negative integer.
- [`Refined.Refine.nonZeroInt`](./m-refined-refine-nonzeroint.md): Builds a non-zero integer.
- [`Refined.Refine.negativeInt`](./m-refined-refine-negativeint.md): Builds a negative integer.
- [`Refined.Refine.nonPositiveInt`](./m-refined-refine-nonpositiveint.md): Builds a non-positive integer.
- [`Refined.Refine.nonEmptyList`](./m-refined-refine-nonemptylist.md): Builds a non-empty list from a sequence.
- [`Refined.Refine.nonEmptyArray`](./m-refined-refine-nonemptyarray.md): Builds a non-empty array from a sequence.
- [`Refined.Refine.distinctList`](./m-refined-refine-distinctlist.md): Builds a distinct list from a sequence.
- [`Refined.Refine.boundedList`](./m-refined-refine-boundedlist.md): Builds a bounded list from a sequence.
- [`Refined.Refine.boundedArray`](./m-refined-refine-boundedarray.md): Builds a bounded array from a sequence.
- [`Refined.Refine.dateTimeOffsetRange`](./m-refined-refine-datetimeoffsetrange.md): Builds a date and time range where <code>Start &lt;= End</code>.
- [`Refined.Refine.dateOnlyRange`](./m-refined-refine-dateonlyrange.md): Builds a date-only range where <code>Start &lt;= End</code>.
- [`Refined.Refine.exactlyOne`](./m-refined-refine-exactlyone.md): Extracts the only item from a sequence.
- [`Refined.Refine.atMostOne`](./m-refined-refine-atmostone.md): Extracts zero or one item from a sequence.
