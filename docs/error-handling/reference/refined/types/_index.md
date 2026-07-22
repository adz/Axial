---
title: "Types"
---

This page shows the error and refined value types in `Axial.Refined`.

- [`Refined.ParseError`](./t-refined-parseerror.md): Primitive parse failures returned by <code>Parse</code> helpers.
- [`Refined.RefinementError`](./t-refined-refinementerror.md): Structural failures returned by built-in refinement constructors and the <code>refine { }</code> builder.
- [`Refined.NonBlankString`](./t-refined-nonblankstring.md): A string that is not null, empty, or whitespace.
- [`Refined.TrimmedString`](./t-refined-trimmedstring.md): A string that has no leading or trailing whitespace.
- [`Refined.BoundedString`](./t-refined-boundedstring.md): A string whose length is within a caller-supplied inclusive range.
- [`Refined.Slug`](./t-refined-slug.md): An ASCII slug containing lowercase letters, digits, and hyphens.
- [`Refined.PositiveInt`](./t-refined-positiveint.md): An integer greater than zero.
- [`Refined.NonNegativeInt`](./t-refined-nonnegativeint.md): An integer greater than or equal to zero.
- [`Refined.NonZeroInt`](./t-refined-nonzeroint.md): An integer that is not zero.
- [`Refined.NegativeInt`](./t-refined-negativeint.md): An integer less than zero.
- [`Refined.NonPositiveInt`](./t-refined-nonpositiveint.md): An integer less than or equal to zero.
- [`Refined.NonEmptyList`](./t-refined-nonemptylist.md): A list that contains at least one item.
- [`Refined.NonEmptyArray`](./t-refined-nonemptyarray.md): An array that contains at least one item.
- [`Refined.DistinctList`](./t-refined-distinctlist.md): A list with no duplicate items, preserving first-seen order.
- [`Refined.BoundedList`](./t-refined-boundedlist.md): A list whose count is within a caller-supplied inclusive range.
- [`Refined.BoundedArray`](./t-refined-boundedarray.md): An array whose count is within a caller-supplied inclusive range.
- [`Refined.DateTimeOffsetRange`](./t-refined-datetimeoffsetrange.md): A date and time range where <code>Start &lt;= End</code>.
- [`Refined.DateOnlyRange`](./t-refined-dateonlyrange.md): A date-only range where <code>Start &lt;= End</code>.
