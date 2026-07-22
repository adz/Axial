---
title: "NonEmptyList"
type: docs
---

This page shows the `NonEmptyList` value helpers.

- [`Refined.NonEmptyList.toList`](./m-refined-nonemptylist-tolist.md): Returns the refined value as a standard list.
- [`Refined.NonEmptyList.create`](./m-refined-nonemptylist-create.md): Builds a non-empty list from a sequence.
- [`Refined.NonEmptyList.cons`](./m-refined-nonemptylist-cons.md): Prepends a head item to a list, producing a non-empty list without failure.
- [`Refined.NonEmptyList.map`](./m-refined-nonemptylist-map.md): Transforms each item while preserving non-emptiness.
- [`Refined.NonEmptyList.filter`](./m-refined-nonemptylist-filter.md): Filters the list, returning a standard list because filtering can remove every item.
- [`Refined.NonEmptyList.tryFilter`](./m-refined-nonemptylist-tryfilter.md): Filters the list and re-certifies that at least one item remains.
