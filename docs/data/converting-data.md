---
weight: 30
title: Convert data and parse JSON
type: docs
description: Convert JSON, .NET values, configuration, and command-line input into and out of Data.
---

# Convert data and parse JSON

`Data` is a tree of objects, lists, text, numbers, Booleans, and null. Conversion functions copy other representations
into that tree. Extraction and rendering functions take values back out.

```fsharp
open Axial.Data
open Data.Syntax
```

## Parse JSON text portably

Install `Axial.Schema.Json` when JSON text must be parsed the same way on .NET and Fable. `Json.parseData` reads one
complete JSON value into a `Data` tree while preserving object field order, duplicate field names, and number-token
spelling.

```fsharp
open Axial.Schema.Json

let value = Json.parseData """{"amount":1.20e+3,"active":true}"""

value
// => Data.Object [
//      "amount", Data.Number "1.20e+3"
//      "active", Data.Bool true
//    ]
```

Invalid JSON raises `JsonCodecException`. `parseData` does not decode fields into application-specific record types;
it only converts JSON syntax into the corresponding `Data` cases.

`Data.Json.render` and `Data.Json.renderIndented` produce JSON text. `Data.render` and `Data.renderIndented` instead
produce a concise human-readable display.

```fsharp
Data.Json.render value
// => "{\"amount\":1.20e+3,\"active\":true}"
```

## Use the native .NET parser

On .NET 8+, use `Data.ofJsonElement` or `Data.ofJsonDocument` when JSON has already been parsed with
`System.Text.Json`. These conversion functions are intentionally .NET-only.

```fsharp
use document = System.Text.Json.JsonDocument.Parse("""{"name":"Ada"}""")
let value = Data.ofJsonDocument document

Data.Json.render value
// => "{\"name\":\"Ada\"}"
```

The returned `Data` is a copy and remains usable after the document is disposed.

## Use the native JavaScript parser under Fable

Under Fable, pass the result of the host's `JSON.parse` to `Data.ofJsonValue`:

```fsharp
open Fable.Core

let value = JS.JSON.parse """{"name":"Ada","active":true}""" |> Data.ofJsonValue
```

Native JSON parsing is convenient when its normal JavaScript semantics are acceptable. It discards duplicate object
fields and converts numbers to JavaScript numbers, so it cannot preserve the original number-token spelling. Use
`Json.parseData` from `Axial.Schema.Json` when those distinctions matter.

## Convert F# and .NET values

| Input | Function | Result |
| --- | --- | --- |
| `Map<string, Data>` | `Data.objectOfMap` | Object fields ordered by map key. |
| `(string * Data) list` | `Data.objectOfList` | Object fields in list order, including duplicates. |
| `Map<string, string>` | `Data.ofMap` | Object of text or null values. |
| `IDictionary<string, string>` | `Data.ofDictionary` | Object of text or null values. |
| `seq<string * string>` | `Data.ofNameValues` | Repeated names become lists. |
| `NameValueCollection` | `Data.ofNameValueCollection` | Repeated names become lists. |
| command-line arguments | `Data.ofCliArgs` | Parsed option names and their supplied values. |
| flattened configuration pairs | `Data.ofConfiguration` | Nested objects and lists from colon-separated keys. |
| configuration key/value pairs | `Data.ofConfigurationPairs` | The .NET configuration form, including null values. |

## Read values from Data

Use paths to retrieve nested data. `tryFindPath` returns `None` for a missing path; `lookupPath` returns `Data.Null`.
The parsed-path equivalents are `tryFind` and `lookup`.

```fsharp
let value = data [ "customer" => [ "name" => "Ada" ] ]

Data.tryFindPath "customer.name" value
// => Some (Data.Text "Ada")

Data.lookupPath "customer.missing" value
// => Data.Null
```

Shape-specific extractors return `Some` only for the named case:

```fsharp
Data.tryText (Data.Text "Ada")             // => Some "Ada"
Data.tryBool (Data.Bool true)               // => Some true
Data.tryNumberToken (Data.Number "1.20")   // => Some "1.20"
Data.tryList (Data.List [])                 // => Some []
Data.tryObject (Data.Object [])             // => Some []
```

`tryRedisplay`, `redisplay`, and their `At` and `Path` forms turn scalar values back into form-style text. Use JSON
rendering when the output must remain structured JSON.
