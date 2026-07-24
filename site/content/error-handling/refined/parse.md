---
weight: 10
title: Parse
description: Convert serialized strings into primitive F# values.
type: docs
---


`Parse` converts strings into primitive values. Each parser returns `Result<'value, ParseError>`.

```fsharp
open Axial.Refined

let count : Result<int, ParseError> =
    Parse.int "12"
// Ok 12

let invalid : Result<int, ParseError> =
    Parse.int "twelve"
// Error (InvalidFormat ("int", "twelve"))
```

Use the named function for the destination type:

```fsharp
let enabled = Parse.bool "true"
let price = Parse.decimal "19.95"
let id = Parse.guid "89d45a4b-f634-4db0-9a41-7e8461957be1"
let created = Parse.dateTimeOffset "2026-07-24T09:30:00+09:30"
```

Numeric parsing uses the invariant culture. Empty or whitespace text returns `MissingValue`. Text with the wrong
format returns `InvalidFormat`, and a numeric value outside the destination type's range returns `OutOfRange`.

## Optional input

Optional parsers keep absent input separate from malformed text:

```fsharp
Parse.intOption None
// Ok None

Parse.intOption (Some "42")
// Ok (Some 42)

Parse.intOption (Some "bad")
// Error (InvalidFormat ("int", "bad"))
```

The `OrDefault` functions use a fallback only for `None`:

```fsharp
Parse.intOrDefault 80 None
// Ok 80

Parse.intOrDefault 80 (Some "bad")
// Error (InvalidFormat ("int", "bad"))
```

`Parse.optional` and `Parse.optionalOr` apply the same behavior to another parser.

## Continue with refinement

Parsing establishes the F# representation. A refinement can then establish a rule about the parsed value:

```fsharp
let quantity raw : Result<PositiveInt, RefinementError> =
    refine {
        let! value = Parse.int raw
        return! Refine.positiveInt value
    }
```

Continue with [Built-in Refined Values](../catalog/). The
[Parse API reference]({{< relref "/error-handling/reference/refined/parse/" >}}) lists every parser.
