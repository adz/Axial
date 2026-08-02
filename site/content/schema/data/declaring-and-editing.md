---
weight: 20
title: Declare, render, and edit data
type: docs
description: Every literal, rendering, and immutable edit operation in Axial.Data.
---


Open `Axial` for `Data`. Open `Data.Syntax` for the concise operators and functions used on this page.

```fsharp
open Axial
open Data.Syntax
```

## Declare objects and lists

`data` builds an object from field declarations. `=>` adds a field. A nested field list becomes another object, while
a list of ordinary values becomes `Data.List`.

```fsharp
let customer =
    data [
        "name" => "Ada"
        "active" => true
        "address" => [
            "city" => "Adelaide"
            "postcode" => 5000
        ]
        "roles" => [ "author"; "admin" ]
    ]

Data.render customer
// => "{\"name\":\"Ada\",\"active\":true,\"address\":{\"city\":\"Adelaide\",\"postcode\":5000},\"roles\":[\"author\",\"admin\"]}"
```

Literal fields accept `Data`, `string`, `bool`, `int`, `int64`, `decimal`, finite `float`, `Guid`, `DateTimeOffset`,
`DateOnly` on .NET 8+, supported lists, and nested field lists.

## Omit a field or write null

`?=>` adds a `Some` value and omits `None`. `nil` writes a present JSON null.

```fsharp
let value =
    data [
        "nickname" ?=> None
        "deletedAt" => nil
    ]

Data.render value
// => "{\"deletedAt\":null}"
```

## Reuse object fields

`fields` returns the fields of an existing object so they can be included in a new declaration.

```fsharp
let address = data [ "city" => "Adelaide"; "postcode" => 5000 ]
let customer = data [ yield! fields address; "name" => "Ada" ]

Data.render customer
// => "{\"city\":\"Adelaide\",\"postcode\":5000,\"name\":\"Ada\"}"
```

## Render JSON

`Data.render` returns compact JSON. `Data.renderIndented` returns the same value with line breaks and indentation.
Both preserve object field order, duplicate fields, and number tokens.

```fsharp
Data.renderIndented (data [ "name" => "Ada" ])
// => "{\n  \"name\": \"Ada\"\n}"
```

## Edit without changing the original

Use a direct `Data` operation for one change. It returns the changed tree and leaves the original unchanged.

```fsharp
let renamed = customer |> Data.set "name" "Grace"

Data.lookupPath "name" renamed
// => Data.Text "Grace"

Data.lookupPath "name" customer
// => Data.Text "Ada"
```

Every edit is available directly:

| Direct operation | Result |
| --- | --- |
| `Data.set path value input` | Replace an existing value. |
| `Data.put path value input` | Replace a value, or add a missing final object field. |
| `Data.remove path input` | Remove an existing field or list item. |
| `Data.append path value input` | Add an item to the end of a list. |
| `Data.prepend path value input` | Add an item to the start of a list. |
| `Data.insert path index value input` | Add an item at a list index. |
| `Data.rename path newName input` | Rename an object field without moving it. |
| `Data.update path function input` | Replace a value with the function result. |

```fsharp
customer
|> Data.put "plan" "pro"
|> Data.append "roles" "admin"
|> Data.rename "address.city" "suburb"
|> Data.remove "active"
```

Direct operations raise `DataPatchException` if the target does not exist or has the wrong shape.

## Apply several edits atomically

`Data.patch` applies a list of edits in order. It returns a new value only when every edit succeeds.

```fsharp
let changed =
    customer
    |> Data.patch [
        set "name" "Grace"
        put "plan" "pro"
        append "roles" "admin"
    ]
```

Inside `Data.patch`, use the unqualified edit constructors from `Data.Syntax`:

| Operation | Result |
| --- | --- |
| `set path value` | Replace an existing value. |
| `put path value` | Replace a value, or add a missing final object field. |
| `remove path` | Remove an existing field or list item. |
| `append path value` | Add an item to the end of a list. |
| `prepend path value` | Add an item to the start of a list. |
| `insert path index value` | Add an item at a list index. |
| `rename path newName` | Rename an object field without moving it. |
| `update path function` | Replace a value with the function result. |

Every operation except a missing final field handled by `put` requires its target to exist. If an edit fails, none of
the edits are returned as a partial result.

`Data.patch` raises `DataPatchException`. `Data.tryPatch` returns the failure instead:

```fsharp
Data.tryPatch [ append "name" "Grace" ] customer
// => Error [
//      { EditIndex = 0
//        Path = "name"
//        Message = "Expected a list but found text." }
//    ]
```
