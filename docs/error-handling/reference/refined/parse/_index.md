---
title: "Parse"
---

`Parse` functions convert serialized strings into primitive values.

- [`Parse.int`](./m-parse-parse-int.md): Parses a 32-bit integer.
- [`Parse.long`](./m-parse-parse-long.md): Parses a 64-bit integer.
- [`Parse.decimal`](./m-parse-parse-decimal.md): Parses a decimal number.
- [`Parse.float`](./m-parse-parse-float.md): Parses a double-precision floating point number.
- [`Parse.bool`](./m-parse-parse-bool.md): Parses a boolean.
- [`Parse.guid`](./m-parse-parse-guid.md): Parses a GUID.
- [`Parse.dateTime`](./m-parse-parse-datetime.md): Parses a date and time value.
- [`Parse.dateTimeOffset`](./m-parse-parse-datetimeoffset.md): Parses a date and time value with offset.
- [`Parse.dateOnly`](./m-parse-parse-dateonly.md): Parses a date-only value.
- [`Parse.timeOnly`](./m-parse-parse-timeonly.md): Parses a time-only value.
- [`Parse.enum`](./m-parse-parse-enum.md): Parses an enum value by name or numeric text.
- [`Parse.intOption`](./m-parse-parse-intoption.md): Parses an optional integer. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Parse.boolOption`](./m-parse-parse-booloption.md): Parses an optional Boolean. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Parse.decimalOption`](./m-parse-parse-decimaloption.md): Parses an optional decimal. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Parse.guidOption`](./m-parse-parse-guidoption.md): Parses an optional GUID. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.
- [`Parse.intOrDefault`](./m-parse-parse-intordefault.md): Parses an optional integer, using the supplied fallback only when the input is absent.
- [`Parse.boolOrDefault`](./m-parse-parse-boolordefault.md): Parses an optional Boolean, using the supplied fallback only when the input is absent.
- [`Parse.decimalOrDefault`](./m-parse-parse-decimalordefault.md): Parses an optional decimal, using the supplied fallback only when the input is absent.
