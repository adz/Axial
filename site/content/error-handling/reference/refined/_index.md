---
title: "Refined"
weight: 50
type: docs
---

`Axial.Refined` supplies invariant-carrying values and the operations that justify them. A type earns its place by making a partial operation total, guaranteeing a property later operations rely on, or removing a branch from consumers — validation that carries no invariant past the boundary belongs in `Constraint` instead. `Refinement` couples checking, total construction, and a total reverse projection.

## Refined types

- [`Refined.NonBlankString`](./types/t-refined-nonblankstring.md): A string that is not null, empty, or whitespace.
- [`Refined.PositiveInt`](./types/t-refined-positiveint.md): An integer greater than zero.
- [`Refined.NonNegativeInt`](./types/t-refined-nonnegativeint.md): An integer greater than or equal to zero.
- [`Refined.NonZeroInt`](./types/t-refined-nonzeroint.md): An integer that is not zero.
- [`Refined.PositiveInt64`](./types/t-refined-positiveint64.md): A 64-bit integer greater than zero.
- [`Refined.NonNegativeInt64`](./types/t-refined-nonnegativeint64.md): A 64-bit integer greater than or equal to zero.
- [`Refined.NonZeroInt64`](./types/t-refined-nonzeroint64.md): A 64-bit integer that is not zero.
- [`Refined.PositiveDecimal`](./types/t-refined-positivedecimal.md): A decimal greater than zero.
- [`Refined.NonNegativeDecimal`](./types/t-refined-nonnegativedecimal.md): A decimal greater than or equal to zero.
- [`Refined.NonZeroDecimal`](./types/t-refined-nonzerodecimal.md): A decimal that is not zero.
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

## Numeric

- [`Refined.Numeric.positiveInt`](./numeric/m-refined-numeric-positiveint.md):
- [`Refined.Numeric.nonNegativeInt`](./numeric/m-refined-numeric-nonnegativeint.md):
- [`Refined.Numeric.nonZeroInt`](./numeric/m-refined-numeric-nonzeroint.md):
- [`Refined.Numeric.positiveInt64`](./numeric/m-refined-numeric-positiveint64.md):
- [`Refined.Numeric.nonNegativeInt64`](./numeric/m-refined-numeric-nonnegativeint64.md):
- [`Refined.Numeric.nonZeroInt64`](./numeric/m-refined-numeric-nonzeroint64.md):
- [`Refined.Numeric.positiveDecimal`](./numeric/m-refined-numeric-positivedecimal.md):
- [`Refined.Numeric.nonNegativeDecimal`](./numeric/m-refined-numeric-nonnegativedecimal.md):
- [`Refined.Numeric.nonZeroDecimal`](./numeric/m-refined-numeric-nonzerodecimal.md):

## Collection

- [`Refined.Collection.nonEmptyList`](./collection/m-refined-collection-nonemptylist.md):
- [`Refined.Collection.nonEmptyArray`](./collection/m-refined-collection-nonemptyarray.md):
- [`Refined.Collection.distinctList`](./collection/m-refined-collection-distinctlist.md):

## Interval

- [`Refined.Interval.between`](./m-refined-interval-between.md):  Builds the smallest interval containing both values, ordering them as needed.
 Total — this is the constructor to reach for first.
- [`Refined.Interval.create`](./m-refined-interval-create.md):
 Builds an interval from a pair the caller asserts is already ordered, failing when
 it is not. Use this at a boundary, where an inverted pair is a caller error worth
 reporting rather than silently repairing; use <code>between</code> when either order is
 acceptable input.

- [`Refined.Interval.lower`](./m-refined-interval-lower.md): Returns the inclusive lower bound.
- [`Refined.Interval.upper`](./m-refined-interval-upper.md): Returns the inclusive upper bound.
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

- [`Refined.Refinement`](./t-refined-refinement.md):  Defines admission into an invariant-carrying value and its total reverse projection.
- [`Refined.Refinement.define`](./m-refined-refinement-define.md):  Defines a refinement from one portable constraint.
- [`Refined.Refinement.defineAll`](./m-refined-refinement-defineall.md):  Defines a refinement from one or more portable constraints.
- [`Refined.Refinement.defineWithCheck`](./m-refined-refinement-definewithcheck.md):  Defines a metadata-free refinement from an executable check.
- [`Refined.Refinement.create`](./m-refined-refinement-create.md):  Constructs a refined value after its check succeeds.
- [`Refined.Refinement.underlying`](./m-refined-refinement-underlying.md):  Returns the canonical underlying representation.
- [`Refined.Refinement.constraints`](./m-refined-refinement-constraints.md):  Returns portable constraints retained by the refinement.

## Invariant-preserving operations

- [`Refined.NonBlankString.value`](./m-refined-nonblankstring-value.md): Returns the underlying string value.
- [`Refined.NonBlankString.create`](./m-refined-nonblankstring-create.md): Admits text that is not null, empty, or whitespace.
- [`Refined.NonBlankString.append`](./m-refined-nonblankstring-append.md): Concatenates two inhabited strings. Total — the result is still inhabited.
- [`Refined.NonBlankString.trim`](./m-refined-nonblankstring-trim.md): Trims surrounding whitespace. Total — trimming inhabited text leaves it inhabited.
- [`Refined.NonBlankString.split`](./m-refined-nonblankstring-split.md):  Splits on a separator, discarding blank segments. Returns a non-empty list because
 inhabited text always yields at least one inhabited segment.
- [`Refined.PositiveInt.value`](./m-refined-positiveint-value.md): Returns the underlying integer value.
- [`Refined.PositiveInt.create`](./m-refined-positiveint-create.md): Admits a positive integer.
- [`Refined.PositiveInt.add`](./m-refined-positiveint-add.md): Adds exactly, reporting overflow rather than wrapping to a negative value.
- [`Refined.PositiveInt.saturatingAdd`](./m-refined-positiveint-saturatingadd.md): Adds, clamping at the largest representable value. Total.
- [`Refined.PositiveInt.toNonNegative`](./m-refined-positiveint-tononnegative.md): Widens to a value that also admits zero. Total.
- [`Refined.NonZeroInt.divide`](./m-refined-nonzeroint-divide.md):
 Divides by a divisor that cannot be zero, so division by zero is unreachable.
 Still reports overflow, which occurs only for <code>Int32.MinValue / -1</code>.

- [`Refined.NonEmptyList.toList`](./m-refined-nonemptylist-tolist.md): Returns the refined value as a standard list.
- [`Refined.NonEmptyList.create`](./m-refined-nonemptylist-create.md): Admits a non-empty list, reporting the same failure the refinement does.
- [`Refined.NonEmptyList.cons`](./m-refined-nonemptylist-cons.md): Prepends an item to a standard list.
- [`Refined.NonEmptyList.map`](./m-refined-nonemptylist-map.md): Applies a mapping to every item. Non-emptiness is preserved.
- [`Refined.NonEmptyList.head`](./m-refined-nonemptylist-head.md): Returns the first item. Total.
- [`Refined.NonEmptyList.last`](./m-refined-nonemptylist-last.md): Returns the final item. Total.
- [`Refined.NonEmptyList.reduce`](./m-refined-nonemptylist-reduce.md): Combines every item with an associative operation. Total — no seed required.
- [`Refined.NonEmptyList.traverseResult`](./m-refined-nonemptylist-traverseresult.md):  Applies a fallible mapping to every item, accumulating every failure rather than
 stopping at the first.
- [`Refined.NonEmptyList.filter`](./m-refined-nonemptylist-filter.md): Filters the items, returning a standard list because emptiness is possible.
- [`Refined.NonEmptyList.tryFilter`](./m-refined-nonemptylist-tryfilter.md): Filters the items, returning <code>None</code> when nothing survives.
- [`Refined.DistinctList.toMap`](./m-refined-distinctlist-tomap.md): Builds a map from a distinct list of pairs. Total and lossless.
- [`Refined.UnitInterval.multiply`](./m-refined-unitinterval-multiply.md):  Multiplies two proportions. Total and closed — this is the operation the type
 exists for, and the only closed multiplication in the package.
- [`Refined.UnitInterval.complement`](./m-refined-unitinterval-complement.md): Returns the distance to one. Total and closed.
- [`Refined.UnitInterval.lerp`](./m-refined-unitinterval-lerp.md):
 Interpolates between two values by this proportion. Total, and guaranteed to stay
 within the two endpoints because the proportion cannot leave <code>[0, 1]</code>.

- [`Refined.FiniteFloat.create`](./m-refined-finitefloat-create.md): Admits a finite double, rejecting infinities and <code>NaN</code>.
- [`Refined.FiniteFloat.negate`](./m-refined-finitefloat-negate.md): Negates the value. Total — negation cannot leave the finite range.
- [`Refined.FiniteFloat.average`](./m-refined-finitefloat-average.md):  Returns the arithmetic mean. Computed by dividing before summing, so a list whose
 total would overflow still averages successfully.
- [`Refined.Bounded.clamp`](./m-refined-bounded-clamp.md):  Restricts a value into the bounds. Total — this is the constructor to reach for
 first, because an out-of-range input has an obvious correct answer.

## Refine facade

- [`Refined.Refine.nonBlankString`](./refine/m-refined-refine-nonblankstring.md):
- [`Refined.Refine.positiveInt`](./refine/m-refined-refine-positiveint.md):
- [`Refined.Refine.nonNegativeInt`](./refine/m-refined-refine-nonnegativeint.md):
- [`Refined.Refine.nonZeroInt`](./refine/m-refined-refine-nonzeroint.md):
- [`Refined.Refine.positiveInt64`](./refine/m-refined-refine-positiveint64.md):
- [`Refined.Refine.nonNegativeInt64`](./refine/m-refined-refine-nonnegativeint64.md):
- [`Refined.Refine.nonZeroInt64`](./refine/m-refined-refine-nonzeroint64.md):
- [`Refined.Refine.positiveDecimal`](./refine/m-refined-refine-positivedecimal.md):
- [`Refined.Refine.nonNegativeDecimal`](./refine/m-refined-refine-nonnegativedecimal.md):
- [`Refined.Refine.nonZeroDecimal`](./refine/m-refined-refine-nonzerodecimal.md):
- [`Refined.Refine.finiteFloat`](./refine/m-refined-refine-finitefloat.md):
- [`Refined.Refine.unitInterval`](./refine/m-refined-refine-unitinterval.md):
- [`Refined.Refine.interval`](./refine/m-refined-refine-interval.md):
- [`Refined.Refine.nonEmptyList`](./refine/m-refined-refine-nonemptylist.md):
- [`Refined.Refine.nonEmptyArray`](./refine/m-refined-refine-nonemptyarray.md):
- [`Refined.Refine.distinctList`](./refine/m-refined-refine-distinctlist.md):
