# Axial.Data

`Axial.Data` is an independent F# package for portable structured values. Its `Data` type represents nulls, text,
number tokens, Booleans, lists, and ordered object fields without depending on Schema, Flow, or a JSON library.

```fsharp
open Axial
open Axial.Data.Syntax

let customer: Data =
    data [
        "name" => "Ada"
        "age" => 42
        "tags" => [ "fsharp"; "schema" ]
        "address" =>
            data [
                "city" => "Adelaide"
                "postcode" => 5000
            ]
    ]
```

The `=>` operator converts supported primitives and lists recursively. The discriminated union remains available for
direct construction and pattern matching through qualified cases such as `Data.Text`, `Data.List`, and `Data.Object`.

See the repository documentation for conversion rules and the complete API.
