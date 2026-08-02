# Axial.Data

`Axial.Data` makes structured data concise to build, change, compare, and test. Its data model maps directly to JSON
and also works well for fixtures, configuration, command-line input, form values, events, and other tree-shaped data.

```fsharp
open Axial
open Data.Syntax

let customer =
    data [
        "name" => "Ada"
        "address" => [
            "city" => "Adelaide"
            "postcode" => 5000
        ]
        "roles" => [ "author" ]
    ]

let promoted =
    customer
    |> Data.patch [
        replace "address.postcode" 5001
        append "roles" "admin"
    ]

promoted
|> matching [
    at "address" (containing [ "postcode" => 5001 ])
    at "roles" (containingItems [ "admin" ])
    absent "error"
]
```

`promoted` contains postcode `5001` and roles `["author","admin"]`; `customer` remains unchanged. The final
`matching` expression returns `unit` because every selected observation succeeds.

`Data.Number` preserves its lexical token. `Data.Object` preserves field order and duplicate names. Exact comparison,
patching, paths, and matching state their behavior rather than silently normalizing those distinctions.

Install it with `dotnet add package Axial.Data`.

`Data.Json.render` writes JSON on .NET and Fable. For portable JSON text parsing, add `Axial.Schema.Json` and use
`Json.parseData`. On .NET 8+, existing `JsonElement` and `JsonDocument` values can be copied with
`Data.ofJsonElement` and `Data.ofJsonDocument`; under Fable, pass a native `JSON.parse` result to `Data.ofJsonValue`.

See the repository documentation for tutorials, partial matching, case matrices, JSON conversion, and the complete
API reference.
