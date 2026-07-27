---
weight: 40
title: Define Refined Types
description: Couple a portable constraint to total construction and projection.
---

# Define Refined Types

A refined type is a private wrapper with a smart constructor and one canonical way to recover its underlying value.
The smart constructor prevents invalid values from entering domain code; the projection lets adapters and persistence
code recover the ordinary representation without rechecking.

## Define the wrapper and Value projection

```fsharp
open Axial.Check
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

`ContactEmail.create` is now the smart constructor. Construction returns check failures directly:

```fsharp
let email : Result<ContactEmail, CheckFailure list> =
    ContactEmail.create rawEmail
```

## Combine portable constraints

`defineAll` requires at least one constraint and checks every constraint against the same original value:

```fsharp
module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.defineAll
            [ Constraint.required
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
        else Error [ CheckFailure.Custom "even" ]

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

Continue with [Schema Integration](../schema/).
