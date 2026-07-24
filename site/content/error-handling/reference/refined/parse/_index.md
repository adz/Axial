---
title: "Parse"
type: docs
---

`Parse` functions convert serialized strings into primitive values.

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
- [`Refined.Parse.intOption`](./m-refined-parse-intoption.md): Parses an optional integer. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Refined.Parse.boolOption`](./m-refined-parse-booloption.md): Parses an optional Boolean. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Refined.Parse.decimalOption`](./m-refined-parse-decimaloption.md): Parses an optional decimal. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Refined.Parse.guidOption`](./m-refined-parse-guidoption.md): Parses an optional GUID. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Refined.Parse.intOrDefault`](./m-refined-parse-intordefault.md): Parses an optional integer, using the supplied fallback only when the input is absent.
- [`Refined.Parse.boolOrDefault`](./m-refined-parse-boolordefault.md): Parses an optional Boolean, using the supplied fallback only when the input is absent.
- [`Refined.Parse.decimalOrDefault`](./m-refined-parse-decimalordefault.md): Parses an optional decimal, using the supplied fallback only when the input is absent.
