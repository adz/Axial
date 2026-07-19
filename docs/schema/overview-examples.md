---
weight: 3
title: Schema Overview Examples
description: Short examples of constructor-last schemas, refined values, recursion, and interpretation.
---

# Schema Overview Examples

Open the constructor-last syntax in the module that owns schema declarations:

```fsharp
open Axial.Schema
open Axial.Schema.Syntax

type Address = { City: string }

let addressSchema =
    Schema.define<Address>
    |> field "city" _.City
    |> constrain (minLength 1)
    |> construct (fun city -> { City = city })
```

`field` infers built-in schemas and canonical schemas declared by user-owned or generated types, recursively through
option, list, and string-keyed map schemas. Use `fieldWith` when supplying an explicit value schema for that field:

```fsharp
type Customer =
    { Id: System.Guid
      Address: Address
      Labels: Map<string, string>
      Note: string option }

let customerSchema =
    Schema.define<Customer>
    |> field "id" _.Id
    |> fieldWith addressSchema "address" _.Address
    |> fieldWith (Schema.map<string>()) "labels" _.Labels
    |> field "note" _.Note
    |> construct (fun id address labels note ->
        { Id = id; Address = address; Labels = labels; Note = note })
```

Use `constructResult` for an invariant that needs several already-parsed fields:

```fsharp
type DateRange = { Start: System.DateOnly; End: System.DateOnly }

let rangeSchema =
    Schema.define<DateRange>
    |> field "start" _.Start
    |> field "end" _.End
    |> constructResult (fun start finish ->
        if start <= finish then Ok { Start = start; End = finish }
        else Error "Start must not follow end.")
```

Refined and union schemas are ordinary `Schema<'value>` values and therefore compose through `fieldWith`:

```fsharp
type Account = { Name: Axial.Refined.NonBlankString }

let accountSchema =
    Schema.define<Account>
    |> fieldWith RefinedSchemas.nonBlankString "name" _.Name
    |> construct (fun name -> { Name = name })
```

Recursive models defer only the recursive value lookup:

```fsharp
type Category = { Name: string; Children: Category list }

let rec categorySchema () =
    Schema.define<Category>
    |> field "name" _.Name
    |> fieldWith (Schema.listWith (Schema.defer categorySchema)) "children" _.Children
    |> construct (fun name children -> { Name = name; Children = children })
```

The completed `Schema<'model>` drives parsing, checking, inspection, JSON Schema, codecs, and test generation:

```fsharp
let parsed = Schema.parse customerSchema rawInput
let checked = Schema.check customerSchema customer
let description = Inspect.model customerSchema
let jsonSchema = JsonSchema.generate customerSchema
```
