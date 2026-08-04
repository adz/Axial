---
weight: 25
title: Numbers
type: docs
description: How numeric values become Data.Number tokens and when their exact spelling matters.
---

# Numbers

`Data.Number` stores a number as text so conversion does not discard precision or exponent notation. Human-readable and
JSON rendering both write that token without quotes.

```fsharp
open Axial.Data
open Data.Syntax
```

## Integers

`int` and `int64` use ordinary base-10 digits with an optional leading minus sign.

```fsharp
let value = data [ "count" => 5000; "change" => -12L ]

Data.render value
// => "{ count: 5000, change: -12 }"
```

## Decimals

`decimal` uses `.` as the decimal separator regardless of the machine's locale. The conversion retains the scale that
the decimal value carries, including trailing zeros.

```fsharp
let value = data [ "price" => 19.9500m ]

Data.render value
// => "{ price: 19.9500 }"
```

## Floating-point values

`float` uses a round-trip token: it writes enough digits for parsing the token to recover the same finite floating-point
value. `NaN`, positive infinity, and negative infinity are rejected because JSON cannot represent them.

```fsharp
let value = data [ "ratio" => 0.1 ]

Data.render value
// => "{ ratio: 0.1 }"
```

## Exact tokens with `num`

Use `num` when the spelling is part of the value you need to retain. It accepts one valid JSON number and keeps the
token unchanged.

```fsharp
let value = data [ "measurement" => num "1.2300e+4" ]

Data.render value
// => "{ measurement: 1.2300e+4 }"
```

The same JSON-number grammar is checked on .NET and Fable. Invalid tokens such as `"01"`, `"NaN"`, `"Infinity"`,
leading `+` signs, surrounding whitespace, and incomplete fractions or exponents raise `ArgumentException`.

## Exact comparison

Exact comparison compares number tokens, not their mathematical values.

```fsharp
Data.compare (num "1") (num "1.0")
// => Error [
//      { Path = DataPath.empty
//        Expected = Some (Data.Number "1")
//        Actual = Some (Data.Number "1.0")
//        Cause = DataDifferenceCause.DifferentValue }
//    ]
```

Use `anyNumber` or a predicate when a match should accept several numeric spellings.
