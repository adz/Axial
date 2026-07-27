---
weight: 10
title: Order Reference Tutorial
description: Decode text, construct refined values, and return a domain record.
---

# Order Reference Tutorial

This tutorial converts an order id and customer reference into a domain record whose fields cannot hold unchecked
values.

## Define the domain

```fsharp
open Axial.Check
open Axial.Parse
open Axial.Refined
open Axial.Result

type OrderId = OrderId of PositiveInt
type CustomerRef = CustomerRef of Slug

type OrderReference =
    { Id: OrderId
      Customer: CustomerRef }
```

## Define boundary errors

```fsharp
type OrderReferenceError =
    | InvalidOrderIdText of ParseError
    | InvalidOrderId of CheckFailure list
    | InvalidCustomerReference of CheckFailure list
```

## Decode and construct

```fsharp
let createOrderReference rawId rawCustomer =
    result {
        let! parsedId = Parse.int rawId |> Result.mapError InvalidOrderIdText
        let! positiveId = Refine.positiveInt parsedId |> Result.mapError InvalidOrderId
        let! customer = Refine.slug rawCustomer |> Result.mapError InvalidCustomerReference

        return
            { Id = OrderId positiveId
              Customer = CustomerRef customer }
    }
```

Each operation keeps its meaning visible: `Parse.int` decodes text, while the two `Refine` functions construct
invariant-carrying values.

## Read values at an output boundary

```fsharp
let orderIdValue (reference: OrderReference) =
    let (OrderId id) = reference.Id
    PositiveInt.value id
```

## Next

- [Parse](/error-handling/parse/)
- [Built-in Refined Values](../../catalog/)
- [Compose Parse and Refinement](../../composition/)
- [Define Refined Types](../../domain-values/)
