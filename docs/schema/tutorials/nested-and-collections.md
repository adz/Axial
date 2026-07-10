---
weight: 15
title: Nested Models And Collections Tutorial
description: Parse an order with a nested address and repeated line items.
---

# Nested Models And Collections Tutorial

This tutorial parses an order that contains a nested address and a collection of line items. Nested schemas and item
schemas are ordinary built schemas — composition is just `Value.nested` and `Value.many`.

## Declare The Schemas

```fsharp
open Axial.Schema
open Axial.Schema

type Address = { Street: string; City: string }
type Item = { Sku: string; Quantity: int }

type Order =
    { Address: Address
      Items: Item list }

let addressSchema =
    Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
    |> Schema.fieldWith [ SchemaConstraint.required ] "street" _.Street Value.text
    |> Schema.fieldWith [ SchemaConstraint.required ] "city" _.City Value.text
    |> Schema.build

let itemSchema =
    Schema.recordFor<Item, _> (fun sku quantity -> { Sku = sku; Quantity = quantity })
    |> Schema.fieldWith [ SchemaConstraint.required ] "sku" _.Sku Value.text
    |> Schema.fieldWith [ SchemaConstraint.greaterThan 0 ] "quantity" _.Quantity Value.int
    |> Schema.build

let orderSchema =
    Schema.recordFor<Order, _> (fun address items -> { Address = address; Items = items })
    |> Schema.fieldWith [ SchemaConstraint.required ] "address" _.Address (Value.nested addressSchema)
    |> Schema.fieldWith [ SchemaConstraint.minCount 1 ] "items" _.Items (Value.many itemSchema)
    |> Schema.build
```

## Adapt Nested Input

Configuration-style keys carry nesting with `:` separators and numeric collection indexes; JSON-like input nests
naturally. Both produce the same shape:

```fsharp
let raw =
    RawInput.ofConfiguration
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
let parsed = Model.parse orderSchema raw

parsed.ErrorsFor "items[1].quantity"   // quantity 0 fails greaterThan 0
parsed.ErrorsFor "items[0].sku"        // [] — the first item is fine
```

Nested diagnostics are prefixed with the field name (`address.city`), collection diagnostics with the item index
(`items[1].quantity`), and the raw values redisplay by the same paths:

```fsharp
RawInput.redisplayPath "items[1].quantity" parsed.Input   // "0"
```

## Count Constraints

Collection constraints (`minCount`, `maxCount`, `count`, `distinct`) attach to the collection field itself and report
on the collection path (`items`), separately from per-item errors.

## Next

- [Rules And Policies](../rules-in-a-workflow/) to accept the parsed order into a workflow.
- [Input Sources](../../input-sources/) for the full adapter catalog.
