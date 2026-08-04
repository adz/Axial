---
weight: 20
title: Match selected parts of data
type: docs
description: Check paths, partial objects, lists, alternatives, types, and predicates.
---

# Match selected parts of data

Matching checks only the values relevant to a test. Unmentioned fields and items can vary without breaking it.

```fsharp
open Axial.Data
open Data.Syntax

let actual =
    data [
        "customer" => [
            "id" => "c-123"
            "name" => "Ada"
            "address" => [ "city" => "Adelaide"; "postcode" => 5000 ]
        ]
        "roles" => [ "author"; "billing"; "admin" ]
        "timeline" => [ "created"; "checked"; "activated" ]
        "events" => [ [ "id" => "e-1" ]; [ "id" => "e-2" ] ]
        "values" => [ 1; 2; 3 ]
        "total" => 19.95m
    ]
```

## Check selected paths

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

## Match a partial object

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

The description appears in mismatch output, so use wording that explains the failed requirement.

`Data.Number "19.95"` satisfies this predicate. `Data.Number "0"` produces a mismatch whose expected description is
`a positive number token`; `Data.Text "19.95"` also fails because the predicate requires a number shape.

```fsharp
Data.tryMatch [ at "total" positiveNumber ] (data [ "total" => 19.95m ])
// => Ok ()

Data.tryMatch [ at "total" positiveNumber ] (data [ "total" => 0 ])
// => Error [ { Path = DataPath.parse "total"; Expected = "a positive number token"; ... } ]
```

## Complete matching vocabulary

| Form | What it accepts |
| --- | --- |
| `at path pattern` | A present value at the path that satisfies the pattern. |
| `absent path` | No value at the path. |
| a literal such as `"Ada"` | That exact value. |
| `exactly value` | An explicit exact recursive pattern. |
| `containing fields` | An object with at least the listed matching fields. |
| `containingItems patterns` | A list containing each pattern in any order; occurrences are consumed once. |
| `inOrder patterns` | A list containing the patterns as an ordered subsequence. |
| `allItems pattern` | A list where every item matches. |
| `someItem pattern` | A list where at least one item matches. |
| `any` | Any present value. |
| `anyText` | Any text value. |
| `anyNumber` | Any number token. |
| `oneOf patterns` | A value matching at least one alternative. |
| `satisfying description predicate` | A value accepted by a custom predicate. |

```fsharp
Data.tryMatch [
    at "customer.name" (oneOf [ exactly "Ada"; exactly "Grace" ])
    at "customer.id" anyText
    at "values" (allItems anyNumber)
] actual
// => Ok ()
```

`matching expectations actual` returns `unit` or raises `DataMatchException`. `Data.tryMatch expectations actual`
returns `Result<unit, DataMismatch list>` and accumulates mismatches from every expectation.
