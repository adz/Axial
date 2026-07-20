---
weight: 15
title: Nested Models And Collections Tutorial
description: Parse an order with a nested address and repeated line items.
type: docs
---


This tutorial parses an order that contains a nested address and a collection of line items. `Address` and `Item`
own their canonical schemas, so `Order` can infer both the nested field and the list item schema.

## Declare The Schemas

```fsharp
open Axial.Schema
open Axial.Schema.Syntax

type Address =
    { Street: string; City: string }

    static member Schema(_: Address) : Schema<Address> =
        Schema.define<Address>
        |> field "street" _.Street
        |> field "city" _.City
        |> construct (fun street city -> { Street = street; City = city })

type Item =
    { Sku: string; Quantity: int }

    static member Schema(_: Item) : Schema<Item> =
        Schema.define<Item>
        |> field "sku" _.Sku
        |> field "quantity" _.Quantity
        |> constrain (greaterThan 0)
        |> construct (fun sku quantity -> { Sku = sku; Quantity = quantity })

type Order =
    { Address: Address
      Items: Item list }

let orderSchema =
    Schema.define<Order>
    |> field "address" _.Address
    |> field "items" _.Items
    |> constrain (minCount 1)
    |> construct (fun address items -> { Address = address; Items = items })
```

## Adapt Nested Input

Configuration-style keys carry nesting with `:` separators and numeric collection indexes; JSON-like input nests
naturally. Both produce the same shape:

```fsharp
let raw =
    Data.ofConfiguration
        [ "address:street", "12 Analytical Way"
          "address:city", "London"
          "items:0:sku", "SKU-1"
          "items:0:quantity", "2"
          "items:1:sku", "SKU-2"
          "items:1:quantity", "0" ]
```

## Parse And Read Item Errors

Every item is parsed and every item error is kept — one bad line item does not hide the others:

```fsharp
let parsed = Schema.parse orderSchema raw

parsed.ErrorsFor "items[1].quantity"   // quantity 0 fails greaterThan 0
parsed.ErrorsFor "items[0].sku"        // [] — the first item is fine
```

Nested diagnostics are prefixed with the field name (`address.city`), collection diagnostics with the item index
(`items[1].quantity`), and the raw values redisplay by the same paths:

```fsharp
Data.redisplayPath "items[1].quantity" parsed.Input   // "0"
```

## Count Constraints

Collection constraints (`minCount`, `maxCount`, `count`, `distinct`) attach to the collection field itself and report
on the collection path (`items`), separately from per-item errors.

Use `fieldWith` instead when a nested field needs a schema that is local to its parent, when wrapping a third-party
type, or for recursive composition with `Schema.defer`.

## Next

- [Input Sources](../../input-sources/) for the full adapter catalog.
