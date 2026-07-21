---
weight: 10
title: Using Data with Axial
type: docs
description: Feeding schemas, boundary parsing, and redisplay-aware error reporting.
---


`Axial.Data` stands alone, but it is also the input half of Axial's schema story: every schema parse
takes a `Data` value, and every source that can become `Data` can therefore feed a schema.

## Feeding a schema

```fsharp
open Axial
open Axial.Data.Syntax
open Axial.Schema

let input =
    data [
        "email" => "ada@example.com"
        "age" => 42
    ]

let parsed = Schema.parse signupSchema input
```

The same schema parses the same logical input regardless of source: `Data.ofJsonDocument` for a
request body, `Data.ofNameValues` for a form post, `Data.ofCliArgs` for a command line,
`Data.ofConfiguration` for settings. The schema never learns which one it was.

This is also why the builder syntax matters in tests: a fixture written with `data [ ... ]` exercises
the identical parse path as production JSON, with no serializer in the loop.

## Redisplay and error reporting

`Schema.parseRetainingInput` keeps the original `Data` alongside the parse result, so a failed form
round-trip can re-show exactly what the user typed next to each field error. The paths in schema
diagnostics (`address.city`, `lines[2].quantity`) address back into the input tree —
`Data.redisplayPath` (string form) or `Data.redisplayAt` (`DataPath` form) recovers the raw fragment
at any of them.

See [redisplay and field errors]({{< relref "/schema/redisplay-and-field-errors/" >}}) for the full
pattern.

## Boundary adapters

The HTTP server packages (`Axial.Schema.Http` and its host adapters) build `Data` from request
surfaces — route values, query strings, headers, JSON bodies — before any schema is involved. Writing
your own adapter for a new source means producing `Data`, nothing more; every schema and interpreter
downstream works unchanged.
