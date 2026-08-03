---
weight: 20
title: Customer Id Tutorial
description: Define a refined type of your own, decide whether it earns its place, and give it a schema.
---

# Customer Id Tutorial

The built-in types cover shapes common to every domain. This tutorial defines one specific
to yours, and — just as importantly — shows how to decide whether it should be a type at
all.

```fsharp
open Axial.Constraint
open Axial.Refined
```

## Decide whether it earns a type

A refined type is worth defining when the invariant does something for the code that
receives it. Ask what becomes total, or what branch disappears:

| Candidate | Verdict |
|---|---|
| `CustomerId` — a positive account number | **Type.** Lookup, ordering, and equality all rely on it, and an id of `0` is a bug you want to catch once. |
| `EmailAddress` — matches an email pattern | **Constraint.** Nothing downstream is total because of it; you unwrap it to send mail. |
| `ShippingWeight` — positive, and summed across a parcel | **Constraint.** The moment you add two of them, F# cannot carry "positive" through, so the type turns every sum into a `Result`. |
| `NormalisedName` — trimmed and lower-cased | **Neither.** That is a transformation, so it belongs in [Parse]({{< relref "/values/parse/" >}}). |

Only the first changes what later code can assume. The second is real validation with no
downstream consequence; the third is validation that arithmetic immediately undoes. Both
stay constraints on the underlying value:

```fsharp
field "email" _.Email {
    constrain Constraint.present
    constrain Constraint.email
}
```

## Define the type

A refined type is a private wrapper, one canonical projection, and a refinement:

```fsharp
type CustomerId =
    private
    | CustomerId of int

    /// The canonical underlying representation.
    member this.Value =
        let (CustomerId value) = this
        value

    override this.ToString() =
        string this.Value

module CustomerId =
    /// Admission and its reverse projection, packaged together.
    let refinement =
        Refinement.define
            (Constraint.greaterThan 0) // the rule over the underlying int
            CustomerId                 // wrap a value that passed
            _.Value                    // unwrap again, always

    let create value = Refinement.create refinement value

    let value (input: CustomerId) = input.Value
```

The case is private, so `CustomerId.create` is the only way in:

```fsharp
CustomerId.create 42   // Ok
CustomerId.create 0    // Error [ OutOfRange (GreaterThan "0", Some "0") ]
```

Use `Refinement.defineAll` when several constraints describe admission, or
`Refinement.defineWithCheck` for an invariant with no portable description. See
[Define Refined Types](../../domain-values/) for both.

## Give it the operations that justify it

This is the step that separates a useful type from a wrapper. `CustomerId` is a key, so
what it owes callers is lookup and identity, not arithmetic:

```fsharp
module CustomerId =
    // ... as above

    /// Total: distinct ids stay distinct, so no entry can be lost.
    let index (customers: DistinctList<CustomerId>) = DistinctList.toSet customers

    /// Total: ids are ordered, so a range of them is an interval.
    let range (first: CustomerId) (second: CustomerId) = Interval.between first second
```

Both work because the invariant is a fact about the value, not about the moment of
construction — and neither involves arithmetic, which is where F# stops being able to
carry the invariant for you. If you cannot write an operation like these, that is good
evidence the concept should be a constraint instead.

## Use it in domain code

```fsharp
let loadCustomer (id: CustomerId) =
    // No guard: id is known to be above zero.
    repository.load id.Value
```

Keep the raw type at input and storage boundaries, and the refined type in between.

## Give it a schema

`Refinement.constraints` exposes the same rule to Schema and other interpreters, so a
boundary describes the type without restating it:

```fsharp
open Axial.Schema

let customerIdSchema : Schema<CustomerId> =
    Schema.int |> Schema.refine CustomerId.refinement
```

Parsing checks the `int`, constructs the `CustomerId`, and reports failures at the field's
path. Encoding projects back through `Value`. The emitted JSON Schema carries
`exclusiveMinimum: 0` because the constraint travelled with the refinement — you did not
write the rule twice.

To resolve the type in a bare field without a `withSchema`, register it once as described
in [Refined Schemas]({{< relref "/schema/refined-values/" >}}), which works the same
example through the Schema DSL and shows where schema-local constraints fit alongside it.

## Next

- [Order Totals](../order-totals/) — the built-in types used in anger.
- [Define Refined Types](../../domain-values/) — the full `Refinement` reference.
- [Schema Integration](../../schema/) — applying refinements at structured boundaries.
- [Refined Schemas]({{< relref "/schema/refined-values/" >}}) — the Schema-side view.
