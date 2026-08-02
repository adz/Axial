---
weight: 60
title: Compare complete data
type: docs
description: Compare every field and item, then inspect all structural differences.
---

# Compare complete data

Use exact comparison when every field and item belongs to the expected result.

```fsharp
open Axial
open Data.Syntax

let expected = data [ "name" => "Ada"; "plan" => "pro" ]
let actual = data [ "name" => "Ada"; "plan" => "free"; "extra" => true ]
```

## Return success or differences

`Data.compare` returns `Ok ()` when the trees are equal. Otherwise it returns every `DataDifference`.

```fsharp
Data.compare expected expected
// => Ok ()

Data.compare expected actual
// => Error [
//      { Path = DataPath.parse "plan"
//        Expected = Some (Data.Text "pro")
//        Actual = Some (Data.Text "free")
//        Cause = DataDifferenceCause.DifferentValue }
//      { Path = DataPath.parse "extra"
//        Expected = None
//        Actual = Some (Data.Bool true)
//        Cause = DataDifferenceCause.Unexpected }
//    ]
```

## Return the difference list directly

`Data.diff expected actual` returns the same list without wrapping it in `Result`.

```fsharp
Data.diff expected expected
// => []
```

## What exact means

Exact comparison observes:

- scalar values and scalar shapes
- number spelling, so `1`, `1.0`, and `1e0` differ
- list length, order, and every item
- object field order, field names, duplicate fields, and every field value

Difference causes are `Missing`, `Unexpected`, `DifferentValue`, `DifferentShape`, and `DifferentFieldName`.
Every difference includes a path plus the expected and actual values when present.
