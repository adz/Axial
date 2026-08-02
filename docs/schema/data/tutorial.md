---
weight: 10
title: Build, vary, and test structured data
type: docs
description: Build one structured value, derive test cases, and check generated JSON.
---

# Build, vary, and test structured data

This tutorial builds one customer value, derives requests from it, parses a JSON response, and checks the parts that
matter to the test.

## Create the baseline

Open `Axial` for the `Data` type and module. Open `Data.Syntax` for the concise literal, edit, and matching syntax:

```fsharp
open Axial
open Data.Syntax

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

Render it with `Data.render`:

```fsharp
Data.render customer
// => "{\"name\":\"Ada\",\"plan\":\"free\",\"deletedAt\":null,\"address\":{\"city\":\"Adelaide\",\"postcode\":5000},\"roles\":[\"author\"]}"
```

Numbers follow these rules:

- `int` and `int64` become base-10 digits, such as `5000` and `-12`.
- `decimal` always uses `.` as its decimal separator, regardless of the machine's locale.
- `float` uses enough digits to read back as the same finite value. `NaN` and infinity are rejected because JSON
  cannot represent them.
- `num` validates and keeps the token exactly as written. Use it when spelling matters, such as `1.2300e+4`.

For example:

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
    |> Data.patch [
        set "plan" "pro"
        append "roles" "admin"
        remove "deletedAt"
    ]
```

Every target except the final field of `put` must exist. Edits run in order and the complete patch is atomic.

Use `Data.tryPatch` when edits came from dynamic input and should return structured failures.

Render the changed request with `Data.render`:

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

## Parse JSON output

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

The parsed value is independent of the `JsonDocument` used internally, so it remains valid after `parse` returns.

`Data.lookupPath "customer.id" response` returns `Data.Text "c-123"`. Rendering the response produces a stable JSON
value with the same field order and number tokens as the parsed tree.

```fsharp
Data.lookupPath "customer.id" response
// => Data.Text "c-123"
```

## Check the behavior

Use paths for individual checks and `containing` when several checks belong to the same object:

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

`matching` raises `DataMatchException` when a test fails. Use `Data.tryMatch` when the mismatches should be returned as
a value.

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
