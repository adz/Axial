---
weight: 25
title: Domain Values
description: Define your own refined domain values and connect them to Refine.from.
---

# Domain Values

This page shows how to define your own domain value types and connect them to type-directed refinement.

A domain-specific type carries meaning that a catalog type such as `NonBlankString`, `Slug`, or `PositiveInt` cannot
express. Keep its invariant in one private type module, then expose the entry points used at the boundary.

## Wrap the value

Start with a private wrapper:

```fsharp
open Axial.ErrorHandling
open Axial.ErrorHandling.CheckDSL
open Axial.Refined

type ContactEmail =
    private
    | ContactEmail of string

    member this.Value =
        let (ContactEmail value) = this
        value
```

The constructor stays private, so other code cannot create a `ContactEmail` without running its checks.

## Add a smart constructor

The smart constructor contains the invariant:

```fsharp
module ContactEmail =
    let create (value: string) : Result<ContactEmail, RefinementError> =
        Refine.withCheck
            "ContactEmail"
            (Check.all [
                present
                email
                maxLength 254
            ])
            ContactEmail
            value
```

`ContactEmail.create` works without `Refine.from` or a computation expression:

```fsharp
let email = ContactEmail.create rawEmail
```

## Connect it to `Refine`

Add `RefineFrom` as a thin adapter over the smart constructor:

```fsharp
type ContactEmail with
    static member RefineFrom(value: string, _: ContactEmail) : Result<ContactEmail, RefinementError> =
        ContactEmail.create value
```

Keep this type extension in the same file as `ContactEmail`; F# then compiles it as part of the type. `RefineFrom`
defines the refinement from `string` to `ContactEmail`.

The expected result type gives `Refine.from` its destination:

let email : Result<ContactEmail, RefinementError> =
    Refine.from rawEmail
```

The static member provides one refinement for that source and destination pair. Two interpretations with the same
pair require named functions because their types do not distinguish them.

## Compose refinements

```fsharp
let createContact rawEmail rawPriority =
    refine {
        let! (email: ContactEmail) = rawEmail
        let! (priority: PositiveInt) = rawPriority
        return email, priority
    }
```

The block stops at the first failure. Code after the block receives only constructed values.

Schema integration is covered separately in [Relation to Schema](../schema/).
