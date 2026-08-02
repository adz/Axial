---
weight: 18
title: Explicit API and concise syntax
type: docs
description: The complete Data.Syntax vocabulary, its explicit equivalents, and a full example without opening it.
---


`Data.Syntax` is optional. It supplies short names and the `=>` and `?=>` operators. The underlying construction and
editing API remains available through `Data` and `DataEdit`.

## Without opening Data.Syntax

This example declares nested data, omits an optional field, applies an atomic patch, and renders the result without
opening `Data.Syntax`:

```fsharp
open Axial

let nickname : string option = None

let customer =
    Data.data [
        Data.assoc "name" "Ada"
        Data.optionalAssoc "nickname" nickname
        Data.assoc "deletedAt" Data.Null
        Data.assoc "address" (
            Data.data [
                Data.assoc "city" "Adelaide"
                Data.assoc "postcode" 5000
            ])
        Data.assoc "roles" [ "author" ]
    ]

let request =
    customer
    |> Data.patch [
        DataEdit.replace "address.postcode" 5001
        DataEdit.append "roles" "admin"
        DataEdit.remove "deletedAt"
    ]

Data.render request
// => "{ name: \"Ada\", address: { city: \"Adelaide\", postcode: 5001 }, roles: [\"author\", \"admin\"] }"
```

For a single edit, apply the direct `Data` operation instead:

```fsharp
customer |> Data.replace "name" "Grace"
```

## The same example with Data.Syntax

```fsharp
open Axial
open Data.Syntax

let customer =
    data [
        "name" => "Ada"
        "nickname" ?=> nickname
        "deletedAt" => nil
        "address" => [ "city" => "Adelaide"; "postcode" => 5000 ]
        "roles" => [ "author" ]
    ]

let request =
    customer
    |> Data.patch [
        replace "address.postcode" 5001
        append "roles" "admin"
        remove "deletedAt"
    ]
```

## Literal and edit mappings

| Concise syntax | Explicit API | Result |
| --- | --- | --- |
| `name => value` | `Data.assoc name value` | `DataField` |
| `name ?=> option` | `Data.optionalAssoc name option` | `DataField` |
| `nil` | `Data.Null` | `Data` |
| `num token` | `Data.number token` | `Data` |
| `data fields` | `Data.data fields` | `Data` |
| `fields value` | `Data.fields value` | `DataField list` |
| `set path value` | `DataEdit.set path value` | `DataEdit` |
| `replace path value` | `DataEdit.replace path value` | `DataEdit` |
| `remove path` | `DataEdit.remove path` | `DataEdit` |
| `append path value` | `DataEdit.append path value` | `DataEdit` |
| `prepend path value` | `DataEdit.prepend path value` | `DataEdit` |
| `insert path index value` | `DataEdit.insert path index value` | `DataEdit` |
| `rename path name` | `DataEdit.rename path name` | `DataEdit` |
| `update path function` | `DataEdit.update path function` | `DataEdit` |
| — | `Data.patch edits input` | changed `Data` |

`set` makes the final path contain the value, adding a missing final object field when necessary. `replace` requires
the target to exist, so a misspelled or unexpectedly absent path fails.

The matching and case-generation names also live in `Data.Syntax`. Without opening it, qualify them as
`Data.Syntax.at`, `Data.Syntax.containing`, `Data.Syntax.variant`, and so on.

## Remaining Data.Syntax names

| Area | Names |
| --- | --- |
| Paths | `at`, `absent` |
| Object patterns | `exactly`, `containing` |
| List patterns | `containingItems`, `inOrder`, `allItems`, `someItem` |
| General patterns | `any`, `anyText`, `anyNumber`, `oneOf`, `satisfying` |
| Matching | `matching` |
| Cases | `variant`, `variants`, `dimension`, `matrix` |

`Data.tryMatch` is always qualified because it is not part of the concise syntax.
