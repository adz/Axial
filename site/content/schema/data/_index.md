---
weight: 5
title: Data
type: docs
notoc: true
description: Portable structured values for documents, fixtures, boundaries, and adapters.
menu:
  main:
    weight: 3
---


`Axial.Data` is one small type for structured values: a source-neutral tree of objects, lists, text,
numbers, booleans, and nulls. It has no dependencies and no opinions about where data came from or
what it will become — it exists for the gap in every program where data has a *shape* but not yet a
*type*.

You meet that gap constantly:

- **Test fixtures.** You want to state "a customer object with a name and two order lines" in a test
  without stringing JSON together or depending on a serializer.
- **Boundary input.** A form post, query string, CLI invocation, or configuration file has arrived,
  and you need one representation to inspect, log, or hand to whatever assigns it a real type.
- **Documents.** A JSON-like payload needs to be built, reshaped, or examined without committing to a
  concrete record type first.
- **Adapters.** Code that translates between formats needs a common middle shape so each side only
  knows about `Data`, not about the other side's library.

`Data` is a structured-*value* model, not a source syntax tree: it distinguishes text, numbers,
booleans, nulls, lists, and objects, but does not model whitespace, comments, or source locations,
and it does not promise byte-exact round-tripping of source documents.

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

`=>` converts supported primitives and recursively converts lists. A nested object remains explicit
through `data`, so a list is never guessed to be an object.

The supported conversions are `string`, `bool`, `int`, `int64`, `decimal`, `float`, `Guid`,
`DateTimeOffset`, `Data`, and recursively supported lists. `DateOnly` is also supported on .NET 8 and
later. Numeric values use invariant formatting. Dates, timestamps, and GUIDs become canonical text
values.

## Convert from a source

Constructors exist for the shapes boundaries actually hand you:

```fsharp
Data.ofMap (Map.ofList [ "name", "Ada"; "age", "42" ])   // map of raw strings
Data.ofNameValues [ "tag", "a"; "tag", "b" ]             // repeated names group into a list
Data.ofCliArgs [| "--name"; "Ada"; "--verbose" |]        // command-line arguments
Data.ofJsonDocument jsonDocument                         // System.Text.Json (also ofJsonElement)
Data.ofConfiguration configurationSection                // Microsoft.Extensions.Configuration
```

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

`Data.Number` stores a lexical token rather than narrowing every number to `decimal` or `float`. This
lets source adapters carry arbitrary-size integers, decimal precision, and exponent notation through
the structured-value tree. The token is the number's portable storage, not a claim that `Data` models
the source document's complete syntax. The DSL turns `42` into `Data.Number "42"`, while `"42"`
remains `Data.Text "42"`.

`Data.Object` preserves field order and duplicate names. Consumers decide whether duplicates are
meaningful, rejected, or resolved according to a boundary format's rules.

## Read values back by path

`Data.redisplay` and `Data.redisplayPath` recover the raw value at a path — useful when a form needs
to re-show exactly what the user typed, or a log needs the offending input fragment.

## Going further

- [API reference]({{< relref "/schema/reference/data/" >}}) — every constructor and helper, source-documented.
- [Using Data with the rest of Axial](with-axial.md) — feeding schemas, boundary parsing, and
  redisplay-aware error reporting.
