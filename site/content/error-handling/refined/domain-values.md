---
weight: 40
title: Define Refined Types
description: Wrap a raw value, enforce its invariant, and make it available to Refine.from and refine {}.
type: docs
---


A refined type prevents invalid values from entering ordinary application code. Its union case is private, and its
smart constructor is the only function that can create it.

This guide starts with an ordinary F# wrapper and smart constructor. It then makes that constructor available to
`Refine.from` and `refine { }`.

## Wrap the raw value

Start with a private wrapper and a function that returns the stored representation:

```fsharp
open Axial.ErrorHandling
open Axial.ErrorHandling.CheckDSL
open Axial.Refined

type ContactEmail =
    private
    | ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value
```

Code outside this file can read a `ContactEmail` through `ContactEmail.value`, but it cannot call the private
`ContactEmail` union case.

## Put the invariant in a smart constructor

The smart constructor returns the wrapper only after the raw string passes every check:

```fsharp
module ContactEmail =
    let value (ContactEmail value) = value

    let create (raw: string) : Result<ContactEmail, RefinementError> =
        Refine.withCheck
            "ContactEmail"
            (Check.all [
                present
                email
                maxLength 254
            ])
            ContactEmail
            raw
```

This is already a complete domain API:

```fsharp
let email : Result<ContactEmail, RefinementError> =
    ContactEmail.create rawEmail
```

Functions that accept `ContactEmail` no longer repeat blank, format, or length checks. A successful construction is the
evidence those checks ran.

## Define a reusable refinement

`Refinement<'raw, 'value>` stores the smart constructor and the function that reads its raw representation:

```fsharp
module ContactEmail =
    // value and create as above

    let refinement : Refinement<string, ContactEmail> =
        Refinement.define create value
```

The functions can also be called through the refinement:

- `Refinement.create ContactEmail.refinement rawEmail` runs `ContactEmail.create`.
- `Refinement.inspect ContactEmail.refinement email` returns the stored string.

## Add type-directed refinement

Expose the refinement through a static `Refinement` member:

```fsharp
type ContactEmail with
    static member Refinement(_: string, _: ContactEmail) =
        ContactEmail.refinement
```

Keep the extension in the same file as the type. The expected result type now supplies the destination to
`Refine.from`:

```fsharp
let email : Result<ContactEmail, RefinementError> =
    Refine.from rawEmail
```

`Refine.from` resolves `Refinement<string, ContactEmail>` at compile time and runs `ContactEmail.create`. It does not
scan assemblies or use runtime reflection.

The dispatch key is the pair of types: `string -> ContactEmail`. There can be only one unnamed refinement for that
pair. If an application accepts both a strict company address and a general address, model them as different result
types or expose named functions such as `ContactEmail.createCompany`.

Two static contributions for the same source and destination make resolution ambiguous and fail compilation. Axial
does not select one by declaration order. Built-in `string -> int` parsing follows the same rule; a different integer
interpretation needs a different destination type or an explicitly named parser.

## Use it in `refine {}`

The computation expression uses the same static refinement:

```fsharp
let createContact rawEmail rawPriority =
    refine {
        let! (email: ContactEmail) = rawEmail
        let! (priority: PositiveInt) = rawPriority
        return email, priority
    }
```

The annotation on the left supplies the destination type. Each `let!` runs the matching refinement and stops the block
on the first `RefinementError`.

`Refine.from` remains the shorter form for one value. `refine {}` is useful when later construction depends on several
successful values.

## What changes across the codebase

Without the wrapper, each consumer receives `string` and must remember which checks ran. Form parsing, JSON decoding,
command construction, database imports, and tests can each grow another copy of the rule.

With `ContactEmail`:

- boundary code constructs it through `ContactEmail.create`, `Refine.from`, or `refine { }`;
- application functions accept `ContactEmail` and contain no email-format branch;
- code that needs the original representation uses `Refinement.inspect`;
- a changed invariant is implemented in `ContactEmail.create`;
- tests for application functions construct valid emails once and focus on application behavior;
- tests for invalid email text stay beside the refinement.

The type removes the unchecked state from function signatures. The refinement keeps construction and inspection
together.

Continue with [Schema Integration](../schema/) if the refined type is a field in an `Axial.Schema` model.
