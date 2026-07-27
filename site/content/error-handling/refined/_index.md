---
weight: 40
title: Refined
type: docs
description: Construct invariant-carrying values from already-typed underlying values.
---


`Axial.Refined` provides smart constructors for types whose private representation records that a check passed. A
smart constructor is useful when accepting a plain `int`, `string`, or collection everywhere would force every caller
to remember the same rule. Construction checks the value once; code that receives the refined type can rely on that
invariant.

In Axial, refinement is mainly the pair of operations around a private wrapper:

- **guard construction** — check an underlying value, then wrap it only on success;
- **project the value** — unwrap it through its canonical `Value` member or module `value` function.

A `Refinement<'underlying,'refined>` packages the checks or constraints, the total wrapping function, and the total
reverse projection. It does not parse text or normalize the input. `Axial.Refined` depends only on `Axial.Check`.

```sh
dotnet add package Axial.Refined
```

```fsharp
open Axial.Check
open Axial.Refined
```

## Use a smart constructor

```fsharp
let quantity : Result<PositiveInt, CheckFailure list> =
    Refine.positiveInt 3

let name : Result<NonBlankString, CheckFailure list> =
    Refine.nonBlankString "Ada"

let tags : Result<NonEmptyList<string>, CheckFailure list> =
    Refine.nonEmptyList [ "fsharp"; "schema" ]
```

After successful construction, downstream functions can require `PositiveInt` instead of repeatedly checking `int`.
Read the canonical underlying representation through the matching `Value` member or module `value` function:

```fsharp
let printQuantity (quantity: PositiveInt) =
    printfn "%d" quantity.Value
```

## Wrap and unwrap an application type

```fsharp
type CustomerId =
    private
    | CustomerId of int

    member this.Value =
        let (CustomerId value) = this
        value

module CustomerId =
    let refinement =
        Refinement.define
            (Constraint.greaterThan 0)
            CustomerId
            _.Value

    let create value =
        Refinement.create refinement value
```

`CustomerId.create` is the public smart constructor. `Refinement.create` checks the `int` before invoking the private
`CustomerId` wrapper. `id.Value`—or equivalently `Refinement.underlying CustomerId.refinement id`—unwraps a
constructed value without failure. `Refinement.constraints` exposes the same portable rules to Schema and other
interpreters.

This makes application signatures carry useful facts:

```fsharp
let loadCustomer (id: CustomerId) =
    // id is already known to be greater than zero
    repository.load id.Value
```

Keep the raw type at input and storage boundaries. Use the refined type in domain code where the invariant matters.

## Compose with parsing

Parsing changes representation; refinement admits a subset of an already-typed value. Keep both operations visible:

```fsharp
open Axial.Parse
open Axial.Result

type QuantityError =
    | InvalidInteger of ParseError
    | InvalidQuantity of CheckFailure list

let quantity raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidInteger
        let! quantity = Refine.positiveInt parsed |> Result.mapError InvalidQuantity
        return quantity
    }
```

## Read next

1. [Parse](/error-handling/parse/) covers serialized primitive input.
2. [Built-in Refined Values](./catalog/) covers supplied domain types.
3. [Compose Parse and Refinement](./composition/) shows application-error mapping.
4. [Define Refined Types](./domain-values/) defines a private type and reusable refinement.
5. [Schema Integration](./schema/) applies refinements at structured boundaries.
