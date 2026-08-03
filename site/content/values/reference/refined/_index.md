---
title: "Refined"
weight: 20
type: docs
---

`Axial.Refined` supplies invariant-carrying values and the operations that justify them. A type earns its place by making a partial operation total, guaranteeing a property later operations rely on, or removing a branch from consumers — validation that carries no invariant past the boundary belongs in `Constraint` instead. `Refinement` couples checking, total construction, and a total reverse projection.

## Refined types

- [`Refined.NonBlankString`](./types/t-refined-nonblankstring.md): A string that is not null, empty, or whitespace.
- [`Refined.FiniteFloat`](./types/t-refined-finitefloat.md): A double-precision float that is neither infinite nor <code>NaN</code>.
- [`Refined.FiniteFloat32`](./types/t-refined-finitefloat32.md): A single-precision float that is neither infinite nor <code>NaN</code>.
- [`Refined.UnitInterval`](./types/t-refined-unitinterval.md): A finite double between zero and one inclusive.
- [`Refined.NonEmptyList`](./types/t-refined-nonemptylist.md): A list that contains at least one item.
- [`Refined.NonEmptyArray`](./types/t-refined-nonemptyarray.md): An array that contains at least one item.
- [`Refined.DistinctList`](./types/t-refined-distinctlist.md): A list with no duplicate items, preserving first-seen order.
- [`Refined.Interval`](./types/t-refined-interval.md): An inclusive range of ordered values where <code>Lower &lt;= Upper</code>.
- [`Refined.Bounded`](./types/t-refined-bounded.md): A value paired with the inclusive interval it is known to lie within.

## Text

- [`Refined.Text.nonBlankString`](./text/m-refined-text-nonblankstring.md):

## Collection

- [`Refined.Collection.nonEmptyList`](./collection/m-refined-collection-nonemptylist.md):
- [`Refined.Collection.nonEmptyArray`](./collection/m-refined-collection-nonemptyarray.md):
- [`Refined.Collection.distinctList`](./collection/m-refined-collection-distinctlist.md):

## Interval

- [`Refined.Interval.between`](./m-refined-interval-between.md):
 Builds the smallest interval containing both values, ordering them as needed.
 Total — this is the constructor to reach for first.

- [`Refined.Interval.create`](./m-refined-interval-create.md):
 Builds an interval from a pair the caller asserts is already ordered, failing when
 it is not. Use this at a boundary, where an inverted pair is a caller error worth
 reporting rather than silently repairing; use <code>between</code> when either order is
 acceptable input.

- [`Refined.Interval.lower`](./m-refined-interval-lower.md): Returns the inclusive lower bound.
- [`Refined.Interval.upper`](./m-refined-interval-upper.md): Returns the inclusive upper bound.
- [`Refined.Interval.duration`](./m-refined-interval-duration.md): Returns how long an interval of instants lasts. Total and non-negative.
- [`Refined.Interval.widthInt`](./m-refined-interval-widthint.md):
 Returns the distance between the bounds. Total, and widened to 64 bits because the
 width of <code>Int32.MinValue .. Int32.MaxValue</code> does not fit an <code>int</code>.

- [`Refined.Interval.widthDecimal`](./m-refined-interval-widthdecimal.md): Returns the distance between the bounds. Never negative.
- [`Refined.Interval.singleton`](./m-refined-interval-singleton.md): Builds the interval containing exactly one value. Total.
- [`Refined.Interval.contains`](./m-refined-interval-contains.md): Returns whether the value lies within the inclusive bounds.
- [`Refined.Interval.intersect`](./m-refined-interval-intersect.md):
 Returns the shared portion of two intervals, or <code>None</code> when they are disjoint.
 The option is the honest representation of an empty result.

- [`Refined.Interval.overlaps`](./m-refined-interval-overlaps.md): Returns whether the two intervals share at least one value.
- [`Refined.Interval.clamp`](./m-refined-interval-clamp.md): Restricts a value to the interval's bounds. Total.
- [`Refined.Interval.span`](./m-refined-interval-span.md): Returns the smallest interval containing both inputs, gap included. Total.

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

- [`Refined.Refinement`](./t-refined-refinement.md): Admission into an invariant-carrying value, and its total reverse projection.
- [`Refined.Refinement.define`](./m-refined-refinement-define.md): Defines a refinement from one constraint, a constructor, and the reverse projection.
- [`Refined.Refinement.create`](./m-refined-refinement-create.md): Constructs a refined value, reporting why the raw value was not admitted.
- [`Refined.Refinement.underlying`](./m-refined-refinement-underlying.md): Returns the canonical underlying representation of a refined value.
- [`Refined.Refinement.constraint'`](./m-refined-refinement-constraint.md): Returns the constraint the refinement admits by.

## Invariant-preserving operations

- [`Refined.NonBlankString.value`](./m-refined-nonblankstring-value.md): Returns the underlying string value.
- [`Refined.NonBlankString.create`](./m-refined-nonblankstring-create.md): Admits text that is not null, empty, or whitespace.
- [`Refined.NonBlankString.append`](./m-refined-nonblankstring-append.md): Concatenates two inhabited strings. Total — the result is still inhabited.
- [`Refined.NonBlankString.trim`](./m-refined-nonblankstring-trim.md): Trims surrounding whitespace. Total — trimming inhabited text leaves it inhabited.
- [`Refined.NonBlankString.split`](./m-refined-nonblankstring-split.md):  Splits on a separator, discarding blank segments. Returns a non-empty list because
 inhabited text always yields at least one inhabited segment.
- [`Refined.NonEmptyList.toList`](./m-refined-nonemptylist-tolist.md): Returns the refined value as a standard list.
- [`Refined.NonEmptyList.create`](./m-refined-nonemptylist-create.md): Admits a non-empty list, reporting the same failure the refinement does.
- [`Refined.NonEmptyList.cons`](./m-refined-nonemptylist-cons.md): Prepends an item to a standard list.
- [`Refined.NonEmptyList.map`](./m-refined-nonemptylist-map.md): Applies a mapping to every item. Non-emptiness is preserved.
- [`Refined.NonEmptyList.head`](./m-refined-nonemptylist-head.md): Returns the first item. Total.
- [`Refined.NonEmptyList.last`](./m-refined-nonemptylist-last.md): Returns the final item. Total.
- [`Refined.NonEmptyList.reduce`](./m-refined-nonemptylist-reduce.md): Combines every item with an associative operation. Total — no seed required.
- [`Refined.NonEmptyList.traverseResult`](./m-refined-nonemptylist-traverseresult.md):  Applies a fallible mapping to every item, accumulating every failure rather than
 stopping at the first.
- [`Refined.NonEmptyList.groupBy`](./m-refined-nonemptylist-groupby.md):  Groups items by a key. Every group is non-empty by construction — a group only
 exists because something fell into it — so the values keep their type rather than
 degrading to a list the caller has to re-check.
- [`Refined.NonEmptyList.chunkBySize`](./m-refined-nonemptylist-chunkbysize.md):
 Splits into consecutive runs of the given size. Total: a size below one is treated
 as one, where <code>List.chunkBySize</code> raises, and both the outer list and every
 chunk stay non-empty.

- [`Refined.NonEmptyList.zip`](./m-refined-nonemptylist-zip.md):
 Pairs items positionally, truncating to the shorter input. Total — unlike
 <code>List.zip</code>, which raises when the lengths differ.

- [`Refined.NonEmptyList.filter`](./m-refined-nonemptylist-filter.md): Filters the items, returning a standard list because emptiness is possible.
- [`Refined.NonEmptyList.tryFilter`](./m-refined-nonemptylist-tryfilter.md): Filters the items, returning <code>None</code> when nothing survives.
- [`Refined.DistinctList.toMap`](./m-refined-distinctlist-tomap.md):
 Builds a map from a distinct list of pairs, failing when two pairs share a key.

- [`Refined.DistinctList.toSet`](./m-refined-distinctlist-toset.md):
 Builds a set. Total and lossless — this is the operation that justifies the type,
 because distinct items always produce a set of the same size, while
 <code>Set.ofList</code> on an ordinary list silently collapses duplicates.

- [`Refined.UnitInterval.multiply`](./m-refined-unitinterval-multiply.md):  Multiplies two proportions. Total and closed — this is the operation the type
 exists for, and the only closed multiplication in the package.
- [`Refined.UnitInterval.complement`](./m-refined-unitinterval-complement.md): Returns the distance to one. Total and closed.
- [`Refined.UnitInterval.lerp`](./m-refined-unitinterval-lerp.md):
 Interpolates between two values by this proportion. Total, and guaranteed to stay
 within the two endpoints because the proportion cannot leave <code>[0, 1]</code>.

- [`Refined.UnitInterval.inverseLerp`](./m-refined-unitinterval-inverselerp.md):
 Returns the proportion a value sits at between two bounds — the inverse of
 <code>lerp</code>. Clamped into range, so it is total. Degenerate bounds, where the two
 are equal, give zero rather than dividing by it.

- [`Refined.FiniteFloat.create`](./m-refined-finitefloat-create.md): Admits a finite double, rejecting infinities and <code>NaN</code>.
- [`Refined.FiniteFloat.negate`](./m-refined-finitefloat-negate.md): Negates the value. Total — negation cannot leave the finite range.
- [`Refined.FiniteFloat.average`](./m-refined-finitefloat-average.md):  Returns the arithmetic mean. Computed by dividing before summing, so a list whose
 total would overflow still averages successfully.
- [`Refined.Bounded.clamp`](./m-refined-bounded-clamp.md):  Restricts a value into the bounds. Total — this is the constructor to reach for
 first, because an out-of-range input has an obvious correct answer.

## Refine facade

- [`Refined.Refine.nonBlankString`](./refine/m-refined-refine-nonblankstring.md):
- [`Refined.Refine.finiteFloat`](./refine/m-refined-refine-finitefloat.md):
- [`Refined.Refine.unitInterval`](./refine/m-refined-refine-unitinterval.md):
- [`Refined.Refine.interval`](./refine/m-refined-refine-interval.md):
- [`Refined.Refine.nonEmptyList`](./refine/m-refined-refine-nonemptylist.md):
- [`Refined.Refine.nonEmptyArray`](./refine/m-refined-refine-nonemptyarray.md):
- [`Refined.Refine.distinctList`](./refine/m-refined-refine-distinctlist.md):
