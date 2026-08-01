# Axial.Data

`Axial.Data` is an independent F# package for immutable structured values. Use one owned tree to author fixtures,
derive test cases, consume JSON, and prove selected parts of produced data.

```fsharp
open Axial
open Axial.Data.Syntax

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
    |> patch [
        set "address.postcode" 5001
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

Install it directly with `dotnet add package Axial.Data`, or receive it through `Axial.Schema` or `Axial`.

See the repository documentation for tutorials, produced-data testing, case matrices, JSON conversion, and the complete
API reference.
