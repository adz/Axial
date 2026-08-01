---
weight: 20
title: Test produced JSON
type: docs
description: Choose exact comparison, sparse proofs, or recursive patterns for JSON output.
---

# Test produced JSON

Parse the response once, then choose the narrowest proof that describes the behavior under test.

```fsharp
open Axial
open Axial.Data.Syntax

let actual = Data.Json.parse responseText
```

## Prove selected paths

Use `at` and `absent` when only a few observations matter:

```fsharp
actual
|> matching [
    at "customer.name" "Ada"
    at "customer.id" anyText
    absent "error"
]
```

Every expectation is checked against the same root. Failures accumulate instead of stopping after the first mismatch.

The example succeeds and returns `unit`. It does not compare any unmentioned response field.

For a result-returning assertion:

```fsharp
let result =
    Data.tryMatch [
        at "customer.name" "Grace"
        absent "customer.id"
    ] actual
```

`result` is `Error` containing two `DataMismatch` values: one at `customer.name` with the actual text `"Ada"`, and one
at `customer.id` with the generated identifier that was expected to be absent.

```fsharp
result
// => Error [
//      { ExpectationIndex = 0; Path = DataPath.parse "customer.name"; Actual = Some (Data.Text "Ada"); ... }
//      { ExpectationIndex = 1; Path = DataPath.parse "customer.id"; Actual = Some (Data.Text "c-123"); ... }
//    ]
```

## Prove a partial object

Use `containing` when related evidence should read as one shape:

```fsharp
actual
|> matching [
    at "customer" (
        containing [
            "name" => "Ada"
            "address" => containing [
                "city" => "Adelaide"
            ]
        ])
]
```

Extra fields are allowed. Required fields must be present and match their literal or recursive pattern.

This succeeds for an object such as `{"name":"Ada","address":{"city":"Adelaide","postcode":5000},"id":"c-123"}`.
Neither `postcode` nor `id` is rejected because the pattern does not mention them at their respective object levels.

```fsharp
Data.tryMatch [
    at "customer" (containing [
        "name" => "Ada"
        "address" => containing [ "city" => "Adelaide" ]
    ])
] actual
// => Ok ()
```

## Choose list semantics explicitly

```fsharp
actual
|> matching [
    at "roles" (containingItems [ "admin"; "author" ])
    at "timeline" (inOrder [ "created"; "activated" ])
    at "events" (allItems (containing [ "id" => anyText ]))
    at "values" (someItem anyNumber)
]
```

`containingItems` consumes actual occurrences, so an actual item cannot satisfy two expected occurrences.

`inOrder` allows unrelated values between expected items. It does not reorder the actual list.

For `roles = ["author","billing","admin"]`, `containingItems [ "admin"; "author" ]` succeeds regardless of order.
For `timeline = ["created","checked","activated"]`, `inOrder [ "created"; "activated" ]` succeeds.

`containingItems [ "admin"; "admin" ]` fails when `admin` occurs once. `inOrder [ "activated"; "created" ]` fails
because those values occur in the opposite order.

```fsharp
Data.tryMatch [ at "roles" (containingItems [ "admin"; "author" ]) ] actual
// => Ok ()

Data.tryMatch [ at "roles" (containingItems [ "admin"; "admin" ]) ] actual
// => Error [ { Path = DataPath.parse "roles[1]"; Expected = "a matching list item"; ... } ]
```

## Use a predicate for a local rule

```fsharp
let positiveNumber =
    satisfying "a positive number token" (function
        | Data.Number token -> decimal token > 0m
        | _ -> false)

actual
|> matching [
    at "total" positiveNumber
]
```

The description appears in mismatch output. Keep larger reusable value constraints in `Axial.Check` or typed parsing in
`Axial.Schema`.

`Data.Number "19.95"` satisfies this predicate. `Data.Number "0"` produces a mismatch whose expected description is
`a positive number token`; `Data.Text "19.95"` also fails because the predicate requires a number shape.

```fsharp
Data.tryMatch [ at "total" positiveNumber ] (data [ "total" => 19.95m ])
// => Ok ()

Data.tryMatch [ at "total" positiveNumber ] (data [ "total" => 0 ])
// => Error [ { Path = DataPath.parse "total"; Expected = "a positive number token"; ... } ]
```

## Compare the complete tree

```fsharp
match Data.compare expected actual with
| Ok () -> ()
| Error differences -> failwithf "%A" differences
```

Exact comparison is suitable when every field and item belongs to the contract. `Data.diff expected actual` returns
focused differences without wrapping them in `Result`.

For `expected = data [ "plan" => "pro" ]` and `actual = data [ "plan" => "free" ]`, the result is:

```fsharp
Error [
    {
        Path = DataPath.parse "plan"
        Expected = Some(Data.Text "pro")
        Actual = Some(Data.Text "free")
        Cause = DataDifferenceCause.DifferentValue
    }
]
```
