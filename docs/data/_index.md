---
weight: 5
title: Data
type: docs
notoc: true
description: Portable structured values for documents, boundaries, fixtures, and adapters.
menu:
  main:
    weight: 3
---

# Data

`Axial.Data` provides one source-neutral tree for nulls, primitive values, lists, and objects. It has no dependency on
Schema, Flow, a serializer, or a runtime service.

Use it when a value has document structure but does not yet belong to an application-owned type: test fixtures,
configuration fragments, JSON-like documents, or input passed to an adapter.

## Install

```sh
dotnet add package Axial.Data
```

## Construct data

Open the namespace for the `Data` type and opt into the construction syntax separately:

```fsharp
open Axial
open Axial.Data.Syntax

let customer: Data =
    data [
        "name" => "Ada"
        "age" => 42
        "enabled" => true
        "tags" => [ "fsharp"; "schema" ]

        "address" =>
            data [
                "city" => "Adelaide"
                "postcode" => 5000
            ]
    ]
```

`=>` converts supported primitives and recursively converts lists. A nested object remains explicit through `data`,
so a list is never guessed to be an object.

The supported conversions are `string`, `bool`, `int`, `int64`, `decimal`, `float`, `Guid`, `DateTimeOffset`, `Data`,
and recursively supported lists. `DateOnly` is also supported on .NET 8 and later. Numeric values use invariant
formatting. Dates, timestamps, and GUIDs become canonical text values.

## Use the DU directly

The cases require qualification, so `Data.List` does not conflict with the F# `List` module:

```fsharp
let direct =
    Data.Object [
        "name", Data.Text "Ada"
        "scores",
        Data.List [
            Data.Number "10"
            Data.Number "20"
        ]
    ]

match direct with
| Data.List items -> List.map string items
| Data.Object fields -> fields |> List.map fst
| Data.Text text -> [ text ]
| Data.Number token -> [ token ]
| Data.Bool value -> [ string value ]
| Data.Null -> []
```

`Data.Number` stores the lexical token rather than narrowing every number to `decimal` or `float`. This preserves
arbitrary-size integers, decimal precision, and exponent notation. The DSL turns `42` into `Data.Number "42"`, while
`"42"` remains `Data.Text "42"`.

`Data.Object` preserves field order and duplicate names. Consumers decide whether duplicates are meaningful, rejected,
or resolved according to a boundary format's rules.
