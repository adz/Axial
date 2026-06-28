---
title: "Refined"
weight: 500
---

This page shows the `Axial.Refined` surface for turning untrusted boundary data into stronger structural values. Use `Parse` to convert serialized strings into primitive values, `Refine` to construct built-in refined values such as `NonBlankString`, `PositiveInt`, and `NonEmptyList`, and `refine { }` to sequence fail-fast parsing and refinement before workflow execution.

## Errors and refined types

- [`Refined.ParseError`](./t-refined-parseerror.md): Primitive parse failures returned by <code>Parse</code> helpers.
- [`Refined.RefinementError`](./t-refined-refinementerror.md): Structural failures returned by built-in refinement constructors and the <code>refine { }</code> builder.
- [`Refined.NonBlankString`](./t-refined-nonblankstring.md): A string that is not null, empty, or whitespace.
- [`Refined.PositiveInt`](./t-refined-positiveint.md): An integer greater than zero.
- [`Refined.NonEmptyList`](./t-refined-nonemptylist.md): A list that contains at least one item.

## Parse

- [`Refined.Parse.int`](./m-refined-parse-int.md): Parses a 32-bit integer.
- [`Refined.Parse.long`](./m-refined-parse-long.md): Parses a 64-bit integer.
- [`Refined.Parse.decimal`](./m-refined-parse-decimal.md): Parses a decimal number.
- [`Refined.Parse.float`](./m-refined-parse-float.md): Parses a double-precision floating point number.
- [`Refined.Parse.bool`](./m-refined-parse-bool.md): Parses a boolean.
- [`Refined.Parse.guid`](./m-refined-parse-guid.md): Parses a GUID.
- [`Refined.Parse.dateTime`](./m-refined-parse-datetime.md): Parses a date and time value.
- [`Refined.Parse.dateTimeOffset`](./m-refined-parse-datetimeoffset.md): Parses a date and time value with offset.
- [`Refined.Parse.dateOnly`](./m-refined-parse-dateonly.md): Parses a date-only value.
- [`Refined.Parse.timeOnly`](./m-refined-parse-timeonly.md): Parses a time-only value.
- [`Refined.Parse.enum`](./m-refined-parse-enum.md): Parses an enum value by name or numeric text.
- [`Refined.Parse.intOption`](./m-refined-parse-intoption.md): Parses an optional integer, returning <code>None</code> for missing or invalid text.
- [`Refined.Parse.boolOption`](./m-refined-parse-booloption.md): Parses an optional boolean, returning <code>None</code> for missing or invalid text.
- [`Refined.Parse.decimalOption`](./m-refined-parse-decimaloption.md): Parses an optional decimal, returning <code>None</code> for missing or invalid text.
- [`Refined.Parse.guidOption`](./m-refined-parse-guidoption.md): Parses an optional GUID, returning <code>None</code> for missing or invalid text.
- [`Refined.Parse.intOrDefault`](./m-refined-parse-intordefault.md): Parses an integer or returns the supplied fallback.
- [`Refined.Parse.boolOrDefault`](./m-refined-parse-boolordefault.md): Parses a boolean or returns the supplied fallback.
- [`Refined.Parse.decimalOrDefault`](./m-refined-parse-decimalordefault.md): Parses a decimal or returns the supplied fallback.

## Refine

- [`Refined.Refine.nonBlankString`](./m-refined-refine-nonblankstring.md): Builds a non-blank string.
- [`Refined.Refine.positiveInt`](./m-refined-refine-positiveint.md): Builds a positive integer.
- [`Refined.Refine.nonEmptyList`](./m-refined-refine-nonemptylist.md): Builds a non-empty list from a sequence.

## Builder

- [`refine`](./p-refined--refine.md): The fail-fast <code>refine { }</code> computation expression.
