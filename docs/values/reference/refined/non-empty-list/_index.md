---
title: "NonEmptyList"
---

`NonEmptyList` functions construct, inspect, and transform non-empty lists.

- [`Refined.NonEmptyList.toList`](./m-refined-nonemptylist-tolist.md): Returns the refined value as a standard list.
- [`Refined.NonEmptyList.create`](./m-refined-nonemptylist-create.md): Admits a non-empty list, reporting the same failure the refinement does.
- [`Refined.NonEmptyList.cons`](./m-refined-nonemptylist-cons.md): Prepends an item to a standard list.
- [`Refined.NonEmptyList.map`](./m-refined-nonemptylist-map.md): Applies a mapping to every item. Non-emptiness is preserved.
- [`Refined.NonEmptyList.filter`](./m-refined-nonemptylist-filter.md): Filters the items, returning a standard list because emptiness is possible.
- [`Refined.NonEmptyList.tryFilter`](./m-refined-nonemptylist-tryfilter.md): Filters the items, returning <code>None</code> when nothing survives.
