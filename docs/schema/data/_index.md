---
weight: 5
title: Data
type: docs
notoc: true
description: Build, change, compare, and test structured data without repetitive constructors.
---

# Data

`Axial.Data` makes structured data concise to build and change. Its objects, lists, text, numbers, Booleans, and null
map directly to JSON, but the same model works well for test fixtures, configuration, command-line input, form values,
events, and other tree-shaped data.

Start with one readable value, derive related cases without copying it, and test either the complete result or only the
fields that matter.

This is useful when tests otherwise accumulate large JSON strings, nested constructors, or near-identical fixtures.
The data stays structured, edits identify exactly what changes, and failures point to the path that differs.

## Install

Install the package with:

```sh
dotnet add package Axial.Data
```

## Build, change, and check one value

```fsharp
open Axial
open Data.Syntax

let baseline =
    data [
        "name" => "Ada"
        "plan" => "free"
        "address" => [
            "city" => "Adelaide"
            "postcode" => 5000
        ]
        "roles" => [ "author" ]
    ]

let request =
    baseline
    |> Data.patch [
        replace "plan" "pro"
        append "roles" "admin"
    ]
```

`request` contains the changed plan and the additional role. `baseline` is unchanged.

```fsharp
Data.render request
// => "{ name: \"Ada\", plan: \"pro\", address: { city: \"Adelaide\", postcode: 5000 }, roles: [\"author\", \"admin\"] }"
```

Now check only the parts of the result that matter:

```fsharp
request
|> matching [
    at "name" "Ada"
    at "address" (containing [ "postcode" => 5000 ])
    at "roles" (containingItems [ "admin" ])
    absent "error"
]
// succeeds
```

If an expectation fails, `matching` raises `DataMatchException` with the mismatched path and values. Extra fields and
the additional `author` role are allowed because these patterns check only the values named here.

## Basic syntax

Use `data` to build a `Data` value. Lists represent both objects and lists. A list containing `name => value` fields
is an object; a list containing ordinary values is a list.

```fsharp
open Axial
open Data.Syntax

let person = data [ "name" => "Ada"; "active" => true ]
let roles = [ "author"; "admin" ]
```

Use `?=>` for an optional field. `Some value` includes the field; `None` leaves the field out altogether. Use
`name => nil` when the field must be present with a null value.

```fsharp
let nickname : string option = None
let person = data [ "nickname" ?=> nickname; "deletedAt" => nil ]

Data.render person
// => "{ deletedAt: null }"
```

## Learn and solve tasks

- [Tutorial: build, vary, and test structured data](tutorial/)
- [What Data represents](what-it-does/)
- [Explicit API and concise syntax](syntax/)
- [Numbers](numbers/)
- [Declare, render, and edit data](declaring-and-editing/)
- [Convert data and parse JSON](converting-data/)
- [Match selected parts of data](how-to-test-produced-json/)
- [Build variations and matrices](how-to-build-test-cases/)
- [Compare complete data](compare-data/)
- [API reference]({{< relref "/schema/reference/data/" >}})
