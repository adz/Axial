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
open Axial
open Data.Syntax
```

## Parse JSON text

`Data.Json.parse` reads one complete JSON value from a string and returns its `Data` tree. It preserves object field
order, duplicate field names, and the spelling of number tokens.

```fsharp
let value = Data.Json.parse """{"amount":1.20e+3,"active":true}"""

value
// => Data.Object [
//      "amount", Data.Number "1.20e+3"
//      "active", Data.Bool true
//    ]
```

Invalid JSON raises `JsonException`. `parse` does not decode the fields into application-specific record types; it
only converts JSON syntax into the corresponding `Data` cases.

`Data.Json.render` and `Data.Json.renderIndented` produce JSON text. `Data.render` and `Data.renderIndented` instead
produce a concise human-readable display.

```fsharp
Data.Json.render value
// => "{\"amount\":1.20e+3,\"active\":true}"
```

## Copy from System.Text.Json

Use `Data.ofJsonElement` or `Data.ofJsonDocument` when JSON has already been parsed.

```fsharp
use document = System.Text.Json.JsonDocument.Parse("""{"name":"Ada"}""")
let value = Data.ofJsonDocument document

Data.Json.render value
// => "{\"name\":\"Ada\"}"
```

The returned `Data` is a copy and remains usable after the document is disposed.

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
