---
weight: 25
title: Define Refined Types
description: Wrap a raw value, enforce its invariant, and reuse the same refinement in functions, Refine.from, refine {}, and Schema.
---

# Define Refined Types

A refined type prevents invalid values from entering ordinary application code. Its union case is private, and its
smart constructor is the only function that can create it.

This guide builds one refinement and then uses it through every Axial entry point. The invariant stays in one place.

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

## Describe both directions once

`Refinement<'raw, 'value>` stores the smart constructor and the projection back to its raw representation:

```fsharp
module ContactEmail =
    // value and create as above

    let refinement : Refinement<string, ContactEmail> =
        Refinement.define create value
```

The two directions serve different work:

- `Refinement.create ContactEmail.refinement rawEmail` runs `ContactEmail.create`.
- `Refinement.inspect ContactEmail.refinement email` returns the string needed for encoding, redisplay, and schema
  checking.

Keeping them together prevents parsers and encoders from growing separate, loosely related adapters.

## Add type-directed refinement

Expose the descriptor through a static `Refinement` member:

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

`Refine.from` resolves `Refinement<string, ContactEmail>` at compile time and runs its `create` direction. It does not
scan assemblies or use runtime reflection.

The dispatch key is the pair of types: `string -> ContactEmail`. There can be only one unnamed refinement for that
pair. If an application accepts both a strict company address and a general address, model them as different result
types or expose named functions such as `ContactEmail.createCompany`.

## Use it in `refine {}`

The computation expression uses the same static descriptor:

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

## Use the same descriptor in Schema

`Schema.refine` accepts the descriptor directly:

```fsharp
open Axial.Schema

let contactEmailSchema : Schema<ContactEmail> =
    Schema.text
    |> Schema.refine ContactEmail.refinement
```

Inside a record schema, the parameterless `refine` operation resolves the same descriptor from the raw field schema
and the getter's result type:

```fsharp
type Signup =
    { Email: ContactEmail
      Age: int }

let signupSchema =
    schema<Signup> {
        field "email" _.Email {
            withSchema Schema.text
            constrain Constraint.required
            refine
        }

        field "age" _.Age
        construct (fun email age -> { Email = email; Age = age })
    }
```

The email field starts as text because that is the boundary representation. `constrain` records portable text
metadata. `refine` then changes the field value from `string` to `ContactEmail`. The constructor receives
`ContactEmail`, so invalid text cannot reach `Signup`.

One descriptor now covers direct construction, type-directed construction, dependent construction, schema parsing,
schema checking, and encoding. Adding another refined type repeats this small definition instead of adding adapters to
each boundary.
