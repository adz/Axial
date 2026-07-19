# Axial.Data

`Axial.Data` is an independent F# package for the meaning and shape of unowned structured data. Use its `Data` type
between a source adapter and the code that assigns an application-owned type. It represents nulls, text, numbers,
Booleans, lists, and ordered object fields without depending on Schema, Flow, or a JSON library.

`Data` is a structured-value model, not a source syntax tree. It does not model whitespace, comments, source locations,
or other format-specific syntax. Numbers currently use lexical storage so adapters can carry values outside the range
or precision of a single CLR numeric type without narrowing them.

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
