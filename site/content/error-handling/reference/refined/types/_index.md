---
title: "Types"
type: docs
---

Errors and refined value types defined by `Axial.Refined`.

- [`Refined.NonBlankString`](./t-refined-nonblankstring.md): A string that is not null, empty, or whitespace.
- [`Refined.PositiveInt`](./t-refined-positiveint.md): An integer greater than zero.
- [`Refined.NonNegativeInt`](./t-refined-nonnegativeint.md): An integer greater than or equal to zero.
- [`Refined.NonZeroInt`](./t-refined-nonzeroint.md): An integer that is not zero.
- [`Refined.PositiveInt64`](./t-refined-positiveint64.md): A 64-bit integer greater than zero.
- [`Refined.NonNegativeInt64`](./t-refined-nonnegativeint64.md): A 64-bit integer greater than or equal to zero.
- [`Refined.NonZeroInt64`](./t-refined-nonzeroint64.md): A 64-bit integer that is not zero.
- [`Refined.PositiveDecimal`](./t-refined-positivedecimal.md): A decimal greater than zero.
- [`Refined.NonNegativeDecimal`](./t-refined-nonnegativedecimal.md): A decimal greater than or equal to zero.
- [`Refined.NonZeroDecimal`](./t-refined-nonzerodecimal.md): A decimal that is not zero.
- [`Refined.FiniteFloat`](./t-refined-finitefloat.md): A double-precision float that is neither infinite nor <code>NaN</code>.
- [`Refined.FiniteFloat32`](./t-refined-finitefloat32.md): A single-precision float that is neither infinite nor <code>NaN</code>.
- [`Refined.UnitInterval`](./t-refined-unitinterval.md): A finite double between zero and one inclusive.
- [`Refined.NonEmptyList`](./t-refined-nonemptylist.md): A list that contains at least one item.
- [`Refined.NonEmptyArray`](./t-refined-nonemptyarray.md): An array that contains at least one item.
- [`Refined.DistinctList`](./t-refined-distinctlist.md): A list with no duplicate items, preserving first-seen order.
- [`Refined.Interval`](./t-refined-interval.md): An inclusive range of ordered values where <code>Lower &lt;= Upper</code>.
- [`Refined.Bounded`](./t-refined-bounded.md): A value paired with the inclusive interval it is known to lie within.
