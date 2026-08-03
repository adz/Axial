---
weight: 40
title: Define Refined Types
description: Couple a portable constraint to total construction and projection.
---

# Define Refined Types

A refined type is a private wrapper, a checked constructor, and one canonical way to recover the underlying value.
This page is the reference for that machinery.

Before reaching for it, decide whether the concept deserves a type at all. Checked construction is how a value is
admitted, not a reason on its own: if nothing downstream becomes total or loses a branch, the rule belongs in a
[constraint]({{< relref "/error-handling/constraint/" >}}) on the primitive instead. Numeric ranges are the clearest
example — F# cannot carry "greater than zero" through arithmetic, so a refined number costs more at every use site
than it saves. [When not to make a type](../catalog/#when-not-to-make-a-type) draws the line, and
[Customer Id](../tutorials/customer-id/) works a full example through.

## Define the wrapper and Value projection

```fsharp
open Axial.Constraint
open Axial.Refined

type ContactEmail =
    private
    | ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value
```

## Guard construction with a refinement

Use `Refinement.define` when one portable constraint describes admission:

```fsharp
module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.define
            Constraint.email
            ContactEmail
            value

    let create raw =
        Refinement.create refinement raw
```

`ContactEmail.create` is now the only way in. Construction returns check failures directly:

```fsharp
let email : Result<ContactEmail, Violation> =
    ContactEmail.create rawEmail
```

## Combine portable constraints

`defineAll` requires at least one constraint and checks every constraint against the same original value:

```fsharp
module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.defineAll
            [ Constraint.present
              Constraint.email
              Constraint.maxLength 254 ]
            ContactEmail
            value

    let create raw = Refinement.create refinement raw
```

The same constraint values provide executable checks and `ConstraintDetails` metadata.

## Use a metadata-free check

Use `defineWithCheck` for an invariant that has no portable constraint description:

```fsharp
type EvenInt = private EvenInt of int

module EvenInt =
    let value (EvenInt value) = value

    let private even value =
        if value % 2 = 0 then Ok ()
        else Error [ Violation.Custom "even" ]

    let refinement =
        Refinement.defineWithCheck even EvenInt value
```

## Projection law

For every successful construction, the reverse projection returns the supplied underlying value:

```fsharp
let result =
    ContactEmail.create "ada@example.com"
    |> Result.map (Refinement.underlying ContactEmail.refinement)

// Ok "ada@example.com"
```

Choose one concrete underlying representation. For collection refinements, prefer `'a list` or `'a array` rather than
an arbitrary `seq<'a>`.

## Give the type its operations

A wrapper that only checks on the way in leaves callers unwrapping it at first use. What makes the type worth having
is the family of operations that preserve its invariant, so the fact stays true without being rechecked:

```fsharp
module ContactEmail =
    // ... as above

    /// Total: lower-casing inhabited, well-formed text leaves it inhabited and well-formed.
    let normalise (input: ContactEmail) = ContactEmail(value input |> fun text -> text.ToLowerInvariant())
```

If you cannot write an operation like that, the concept is probably a constraint rather than a type.

Continue with [Schema Integration](../schema/).
