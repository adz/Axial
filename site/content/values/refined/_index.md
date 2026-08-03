---
weight: 40
title: Refined
type: docs
description: Domain types whose invariant removes work from the code that uses them.
---


`Axial.Refined` supplies types that carry an invariant, together with the operations that
invariant makes possible. Guarded construction is how a value is admitted, but it is not
the reason to reach for a refined type — a wrapper that only checks on the way in leaves
callers unwrapping it at first use, and the invariant buys nothing after the boundary.

The types here are chosen so that later code can be simpler:

```fsharp
let total = NonEmptyList.reduce (+) lines   // no seed, no empty case, no option
let largest = NonEmptyList.max lines        // total
let ratio = UnitInterval.multiply a b       // closed: still in [0, 1]
```

`NonEmptyList.max` returns a value rather than an option because the type makes the empty
case unrepresentable. That is the test a type has to pass to belong here: it should make a
partial operation total, guarantee a property later operations rely on, encode a
relationship between values, preserve an invariant across a useful family of operations,
or remove a branch from consumers rather than only from construction.

Validation that fails that test belongs in a constraint instead. Trimmed text and slugs
carry nothing forward — concatenating two trimmed strings is not trimmed — so they are
`Constraint.trimmed` and `Constraint.pattern` on an ordinary `string`, not types. See
[When not to make a type](./catalog/#when-not-to-make-a-type).

`Axial.Refined` depends only on `Axial.Constraint`. It does not
[parse text]({{< relref "/values/parse/" >}}) and does not normalize input.

```sh
dotnet add package Axial.Refined
```

```fsharp
open Axial.Constraint
open Axial.Refined
```

## Admission

Every built-in type has a constructor returning `Result<'refined, Violation>`:

```fsharp
let name : Result<NonBlankString, Violation> = Refine.nonBlankString "Ada"
let lines : Result<NonEmptyList<string>, Violation> = Refine.nonEmptyList [ "a"; "b" ]

Refine.nonBlankString "  "  // Error [ Blank ]
Refine.nonEmptyList []      // Error [ InvalidLength (MinimumLength 1, Some 0) ]
```

Some types also offer a **total** constructor, which is the one to prefer when the input
has an obvious correct reading:

```fsharp
let lines = NonEmpty(first, rest)              // cannot fail
let window = Interval.between finish start     // cannot fail: orders the pair
let ratio = UnitInterval.clamp 1.5             // cannot fail: clamps to 1.0
```

Read the underlying value through the `Value` member or the module's `value` function.

## Where the invariant pays

Contrast the same calculation over plain types and refined ones:

```fsharp
// plain: every caller re-establishes what is already true
let averageLine (lines: OrderLine list) =
    if List.isEmpty lines then None
    else Some (List.sumBy _.Total lines / decimal lines.Length)

// refined: the empty case cannot arise, so there is nothing to return an option for
let averageLine (lines: NonEmptyList<OrderLine>) =
    NonEmptyList.reduce (+) (NonEmptyList.map _.Total lines) / decimal (NonEmptyList.length lines)
```

[Order Totals](./tutorials/order-totals/) works this through on a realistic domain.

## There are no refined numbers

F# cannot propagate an invariant through arithmetic the way a refinement-typed language
can, so a `PositiveInt` would have to re-establish "greater than zero" at every step. With
unchecked integer arithmetic — `Int32.MaxValue + 1` is negative — that means returning
`Result` from addition, and a `Result` per arithmetic step is bulk that hides mistakes
rather than catching them.

Numeric ranges are constraints instead:

```fsharp
field "quantity" _.Quantity { constrain (Constraint.greaterThan 0) }
```

`FiniteFloat` is the exception that proves the rule: it is worth having because `NaN` and
infinity silently destroy an **aggregate** — `List.average [ 12.5; 3.0; nan; 8.25 ]` is
`NaN` — not because of arithmetic or ordering. See
[the catalogue](./catalog/#floating-point).

## Define your own

The machinery is public, so an application type gets the same treatment:

```fsharp
type CustomerId =
    private
    | CustomerId of int

    member this.Value =
        let (CustomerId value) = this
        value

module CustomerId =
    let refinement = Refinement.define (Constraint.greaterThan 0) CustomerId _.Value
    let create value = Refinement.create refinement value
```

`Refinement.constraints` exposes the same rules to Schema and other interpreters, so the
type describes itself at a boundary without restating the rule.
[Customer Id](./tutorials/customer-id/) builds one end to end, including its schema.

## Read next

1. [Order Totals](./tutorials/order-totals/) uses the built-in types in anger.
2. [Built-in Refined Values](./catalog/) lists what each type buys you.
3. [Customer Id](./tutorials/customer-id/) defines a refined type of your own.
4. [Define Refined Types](./domain-values/) is the reference for `Refinement`.
5. [Schema Integration](./schema/) applies refinements at structured boundaries.
6. [Compose Parse and Refinement](./composition/) maps failures into application errors.
