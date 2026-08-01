---
weight: 5
title: Data
type: docs
notoc: true
description: Immutable structured values for fixtures, boundaries, variations, and produced-data proofs.
---

# Data

`Axial.Data` gives F# code one owned representation for objects, lists, text, number tokens, Booleans, and nulls.
It stands alone from Schema and Flow.

Use it when data has a shape but should not yet be assigned an application type:

- author request, response, configuration, and event fixtures
- preserve malformed or partially supplied boundary input
- derive named variations and bounded Cartesian test matrices
- parse and render JSON without narrowing numeric tokens
- compare complete values or prove selected parts of produced output

## Install

`Axial.Data` installs with `Axial.Schema` and `Axial`, or independently:

```sh
dotnet add package Axial.Data
```

## One language from fixture to proof

```fsharp
open Axial
open Axial.Data.Syntax

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
    |> patch [
        set "plan" "pro"
        append "roles" "admin"
    ]

request
|> matching [
    at "name" "Ada"
    at "address" (containing [ "postcode" => 5000 ])
    at "roles" (containingItems [ "admin" ])
    absent "error"
]
```

The same conversions and paths serve literals, edits, generated cases, lookup, comparison, and matching.

In this example, `request` renders as
`{"name":"Ada","plan":"pro","address":{"city":"Adelaide","postcode":5000},"roles":["author","admin"]}`.
The three proofs return `unit`; extra fields and the extra `author` role do not fail partial patterns.

## Semantics worth knowing

`Data.Number "1"`, `Data.Number "1.0"`, and `Data.Number "1e0"` differ under exact comparison. Use `num` when a fixture
must state an exact number token.

Objects preserve field order and duplicate names. Strict path lookup and edits select the last duplicate occurrence.
Exact comparison observes every occurrence and its position.

`None` supplied with `?=>` omits a field. `nil` creates a present null field.

List patterns name their semantics: `containingItems` is unordered consumed containment, while `inOrder` is an ordered
subsequence. `allItems` and `someItem` quantify over the actual list.

## Learn and solve tasks

- [Tutorial: build, vary, and prove structured data](tutorial/)
- [How to test produced JSON](how-to-test-produced-json/)
- [How to build variations and matrices](how-to-build-test-cases/)
- [Using Data with Schema](with-axial/)
- [API reference]({{< relref "/schema/reference/data/" >}})
