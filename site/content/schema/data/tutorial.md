---
weight: 10
title: Build, vary, and prove structured data
type: docs
description: A complete Axial.Data workflow from one fixture to produced-data proofs.
---


This tutorial builds one customer fixture, derives requests from it, parses a produced JSON response, and proves the
parts that establish the behavior under test.

## Create the baseline

Open the structured value type and opt into its authoring syntax:

```fsharp
open Axial
open Axial.Data.Syntax

let customer =
    data [
        "name" => "Ada"
        "plan" => "free"
        "nickname" ?=> None
        "deletedAt" => nil

        "address" => [
            "city" => "Adelaide"
            "postcode" => 5000
        ]

        "roles" => [ "author" ]
    ]
```

Nested object fields use the same list syntax. `?=> None` omits `nickname`; `nil` keeps `deletedAt` as a present null.

`customer` therefore renders as:

```fsharp
Data.render customer
// => "{\"name\":\"Ada\",\"plan\":\"free\",\"deletedAt\":null,\"address\":{\"city\":\"Adelaide\",\"postcode\":5000},\"roles\":[\"author\"]}"
```

Ordinary numbers use invariant conversion. Preserve a deliberate token with `num`:

```fsharp
let invoice =
    data [
        "amount" => 19.95m
        "measurement" => num "1.2300e+4"
    ]
```

`invoice` contains `Data.Number "19.95"` for `amount` and the unchanged token
`Data.Number "1.2300e+4"` for `measurement`.

```fsharp
Data.lookupPath "amount" invoice
// => Data.Number "19.95"

Data.lookupPath "measurement" invoice
// => Data.Number "1.2300e+4"
```

## Derive a request

Apply strict edits instead of reconstructing the fixture:

```fsharp
let upgradeRequest =
    customer
    |> patch [
        set "plan" "pro"
        append "roles" "admin"
        remove "deletedAt"
    ]
```

Every target except the final field of `put` must exist. Edits run in order and the complete patch is atomic.

Use `Data.tryPatch` when edits came from dynamic input and should return structured failures.

`upgradeRequest` renders as:

```fsharp
Data.render upgradeRequest
// => "{\"name\":\"Ada\",\"plan\":\"pro\",\"address\":{\"city\":\"Adelaide\",\"postcode\":5000},\"roles\":[\"author\",\"admin\"]}"
```

The original `customer` still contains `plan: "free"`, one role, and the `deletedAt` field.

## Derive named cases

```fsharp
let nameCases =
    customer
    |> variants [
        variant "present" []
        variant "missing" [ remove "name" ]
        variant "blank" [ set "name" "" ]
        variant "wrong shape" [ set "name" [ "Ada" ] ]
    ]
```

Each `DataCase` carries its name and materialized value. Declaration order is preserved.

The result contains four cases named `valid`, `missing`, `blank`, and `wrong shape` in that order. Their `name` values
are respectively `"Ada"`, absent, `""`, and `Data.List [ Data.Text "Ada" ]`.

```fsharp
nameCases |> List.map (fun case -> case.Name, Data.tryFindPath "name" case.Value)
// =>
// [ ("valid", Some (Data.Text "Ada"))
//   ("missing", None)
//   ("blank", Some (Data.Text ""))
//   ("wrong shape", Some (Data.List [ Data.Text "Ada" ])) ]
```

## Parse produced JSON

```fsharp
let response =
    Data.Json.parse
        """{
          "customer": {
            "id": "c-123",
            "name": "Ada",
            "plan": "pro",
            "roles": ["author", "admin"]
          }
        }"""
```

The result owns its complete tree. It does not borrow the lifetime of a `JsonDocument`.

`Data.lookupPath "customer.id" response` returns `Data.Text "c-123"`. Rendering the response produces a stable JSON
value with the same field order and number tokens as the parsed tree.

```fsharp
Data.lookupPath "customer.id" response
// => Data.Text "c-123"
```

## Prove the behavior

Use paths for sparse evidence and `containing` when the evidence forms a coherent nested shape:

```fsharp
response
|> matching [
    at "customer.id" anyText

    at "customer" (
        containing [
            "name" => "Ada"
            "plan" => "pro"
            "roles" => containingItems [ "admin" ]
        ])

    absent "error"
]
```

Unmentioned object fields are allowed by `containing`. Literal values inside the pattern remain exact.

`matching` raises `DataMatchException` for an authored test. Use `Data.tryMatch` to receive every `DataMismatch` as a
value.

The example returns `unit`: all three expectations succeed even though the response contains the unmentioned `id`
field and the additional `author` role.

Changing the expected plan to `"free"` produces a mismatch at `customer.plan`. Adding `absent "customer.id"` produces
another mismatch because that path contains `Data.Text "c-123"`.

```fsharp
Data.tryMatch [ at "customer.plan" "free"; absent "customer.id" ] response
// => Error [
//      { ExpectationIndex = 0; Path = DataPath.parse "customer.plan"; Actual = Some (Data.Text "pro"); ... }
//      { ExpectationIndex = 1; Path = DataPath.parse "customer.id"; Actual = Some (Data.Text "c-123"); ... }
//    ]
```

## Compare the complete result

Use exact comparison when every field is part of the contract:

```fsharp
match Data.compare expected response with
| Ok () -> ()
| Error differences ->
    differences
    |> List.iter (fun difference ->
        printfn "%s" (DataPath.toString difference.Path))
```

`Data.diff` returns the same differences directly. Exact comparison observes number tokens, object order, duplicate
fields, list length, and list order.

For equal trees, `Data.compare` returns `Ok ()`. If the actual plan is `"free"`, it returns `Error` with a
`DataDifference` whose path is `customer.plan`, expected value is `Data.Text "pro"`, and actual value is
`Data.Text "free"`.

```fsharp
Data.compare
    (data [ "customer" => [ "plan" => "pro" ] ])
    (data [ "customer" => [ "plan" => "free" ] ])
// => Error [
//      { Path = DataPath.parse "customer.plan"
//        Expected = Some (Data.Text "pro")
//        Actual = Some (Data.Text "free")
//        Cause = DataDifferenceCause.DifferentValue }
//    ]
```
