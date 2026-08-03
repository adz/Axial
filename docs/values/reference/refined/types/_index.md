---
title: "Types"
---

Errors and refined value types defined by `Axial.Refined`.

- [`Refined.NonBlankString`](./t-refined-nonblankstring.md): A string that is not null, empty, or whitespace.
- [`Refined.FiniteFloat`](./t-refined-finitefloat.md): A double-precision float that is neither infinite nor <code>NaN</code>.
- [`Refined.FiniteFloat32`](./t-refined-finitefloat32.md): A single-precision float that is neither infinite nor <code>NaN</code>.
- [`Refined.UnitInterval`](./t-refined-unitinterval.md): A finite double between zero and one inclusive.
- [`Refined.NonEmptyList`](./t-refined-nonemptylist.md): A list that contains at least one item.
- [`Refined.NonEmptyArray`](./t-refined-nonemptyarray.md): An array that contains at least one item.
- [`Refined.DistinctList`](./t-refined-distinctlist.md): A list with no duplicate items, preserving first-seen order.
- [`Refined.Interval`](./t-refined-interval.md): An inclusive range of ordered values where <code>Lower &lt;= Upper</code>.
- [`Refined.Bounded`](./t-refined-bounded.md): A value paired with the inclusive interval it is known to lie within.
