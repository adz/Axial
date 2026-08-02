---
weight: 25
title: Numbers
type: docs
description: How numeric values become Data.Number tokens and when their exact spelling matters.
---


`Data.Number` stores a number as text so conversion does not discard precision or exponent notation. Rendering writes
that token as a JSON number, without quotes.

```fsharp
open Axial
open Data.Syntax
```

## Integers

`int` and `int64` use ordinary base-10 digits with an optional leading minus sign.

```fsharp
let value = data [ "count" => 5000; "change" => -12L ]

Data.render value
// => "{\"count\":5000,\"change\":-12}"
```

## Decimals

`decimal` uses `.` as the decimal separator regardless of the machine's locale. The conversion retains the scale that
the decimal value carries, including trailing zeros.

```fsharp
let value = data [ "price" => 19.9500m ]

Data.render value
// => "{\"price\":19.9500}"
```

## Floating-point values

`float` uses a round-trip token: it writes enough digits for parsing the token to recover the same finite floating-point
value. `NaN`, positive infinity, and negative infinity are rejected because JSON cannot represent them.

```fsharp
let value = data [ "ratio" => 0.1 ]

Data.render value
// => "{\"ratio\":0.1}"
```

## Exact tokens with `num`

Use `num` when the spelling is part of the value you need to retain. It accepts one valid JSON number and keeps the
token unchanged.

```fsharp
let value = data [ "measurement" => num "1.2300e+4" ]

Data.render value
// => "{\"measurement\":1.2300e+4}"
```

Invalid JSON numbers such as `"01"`, `"NaN"`, and `"Infinity"` raise `ArgumentException`.

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
