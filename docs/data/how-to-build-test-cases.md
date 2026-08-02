---
weight: 30
title: Build variations and matrices
type: docs
description: Derive named test cases from one structured baseline.
---

# Build variations and matrices

Keep one representative baseline and describe each test case as a strict immutable edit.

The four functions on this page are `variant`, `variants`, `dimension`, and `matrix`.

```fsharp
open Axial
open Data.Syntax

let baseline =
    data [
        "name" => "Ada"
        "plan" => "free"
        "region" => "au"
        "roles" => [ "author" ]
    ]
```

## Build independent cases

```fsharp
let cases =
    baseline
    |> variants [
        variant "valid" []
        variant "missing name" [ remove "name" ]
        variant "blank name" [ replace "name" "" ]
        variant "wrong name shape" [ replace "name" [ "Ada" ] ]
    ]
```

`variants` rejects duplicate names and preserves declaration order.

The result has four `DataCase` values in the shown order:

```fsharp
valid             name = "Ada"
missing name      name is absent
blank name        name = ""
wrong name shape  name = ["Ada"]
```

## Build a Cartesian matrix

```fsharp
let cases =
    baseline
    |> matrix [
        dimension "plan" [
            variant "free" []
            variant "pro" [ replace "plan" "pro" ]
        ]

        dimension "region" [
            variant "AU" []
            variant "US" [ replace "region" "us" ]
        ]

        dimension "roles" [
            variant "none" [ replace "roles" [] ]
            variant "admin" [ replace "roles" [ "admin" ] ]
        ]
    ]
```

Names follow dimension order, such as `plan: pro / region: AU / roles: admin`.

The initial matrix limit is 256 combinations. The product is checked before cases are materialized.

This matrix produces eight cases. The first is
`plan: free / region: AU / roles: none`; its value has `plan = "free"`, `region = "au"`, and `roles = []`.

The last is `plan: pro / region: US / roles: admin`; its value has `plan = "pro"`, `region = "us"`, and
`roles = ["admin"]`.

```fsharp
cases |> List.map _.Name
// =>
// [ "plan: free / region: AU / roles: none"
//   "plan: free / region: AU / roles: admin"
//   "plan: free / region: US / roles: none"
//   "plan: free / region: US / roles: admin"
//   "plan: pro / region: AU / roles: none"
//   "plan: pro / region: AU / roles: admin"
//   "plan: pro / region: US / roles: none"
//   "plan: pro / region: US / roles: admin" ]
```

## Inspect dynamic patch failures

```fsharp
match Data.tryPatch edits baseline with
| Ok value -> runCase value
| Error failures ->
    failures
    |> List.iter (fun failure ->
        printfn "Edit %d at %s: %s" failure.EditIndex failure.Path failure.Message)
```

`Data.patch` raises `DataPatchException` with the same failures.

For `append "name" "Grace"`, the result is one failure at edit index `0`, path `name`, with a message stating that a
list was expected but text was found. The baseline remains unchanged.

```fsharp
Data.tryPatch [ append "name" "Grace" ] baseline
// => Error [
//      { EditIndex = 0
//        Path = "name"
//        Message = "Expected a list but found text." }
//    ]
```
