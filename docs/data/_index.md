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

`Axial.Data` represents the meaning and shape of unowned structured data. It provides one source-neutral tree for
nulls, primitive values, lists, and objects, with no dependency on Schema, Flow, a serializer, or a runtime service.

Use it between a source adapter and the code that assigns an application-owned type: for example, test fixtures,
configuration fragments, JSON-like documents, or boundary input awaiting schema parsing.

`Data` is a structured-value model, not a source syntax tree. It distinguishes text, numbers, Booleans, nulls, lists,
and objects, but it does not model whitespace, comments, source locations, or other format-specific syntax. It is not a
promise of exact source-document round-tripping.

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

`Data.Number` currently stores a lexical token rather than narrowing every number to `decimal` or `float`. This lets
source adapters carry arbitrary-size integers, decimal precision, and exponent notation through the structured-value
tree. The token is the number's portable storage, not a claim that `Data` models the source document's complete syntax.
The DSL turns `42` into `Data.Number "42"`, while `"42"` remains `Data.Text "42"`.

`Data.Object` preserves field order and duplicate names. Consumers decide whether duplicates are meaningful, rejected,
or resolved according to a boundary format's rules.
