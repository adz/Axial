---
weight: 8
title: Parse
description: Convert serialized strings into primitive values before refinement.
---

# Parse

`Parse` converts serialized strings into primitive values such as `int`, `decimal`, `Guid`, and `DateTimeOffset`.
Each function returns `Result<'value, ParseError>` and leaves structural domain checks to `Refine`.

```fsharp
open Axial.Refined

let quantity = Parse.int "12"
// Ok 12

let invalid = Parse.int "twelve"
// Error (InvalidFormat ...)
```

Optional parsers distinguish missing input from malformed input. `Parse.intOption None` returns `Ok None`; malformed
present text returns a `ParseError`.

Use `Parse` before `Refine` when input starts as text:

```fsharp
let positiveQuantity raw =
    refine {
        let! value = Parse.int raw
        return! Refine.positiveInt value
    }
```

See the [Parse API reference]({{< relref "/error-handling/reference/refined/parse/" >}}) for every supported primitive
and optional parser.
