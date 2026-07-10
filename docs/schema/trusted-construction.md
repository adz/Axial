---
weight: 10
title: Trusted Construction
type: docs
description: ActiveModel ergonomics with F# trusted construction.
---

# Trusted Construction

Rails ActiveModel made one thing easy: declare the fields and constraints in one place, then let the framework parse
params, collect errors, and redisplay input. Axial keeps that ergonomic while keeping F#'s core promise — **an invalid
model is never constructed**.

## Side By Side

ActiveModel validates a mutable object after construction:

```ruby
class Signup
  include ActiveModel::Model
  attr_accessor :email, :age

  validates :email, presence: true, length: { maximum: 254 }
  validates :age, numericality: { greater_than_or_equal_to: 13 }
end

signup = Signup.new(params)   # exists even when invalid
signup.valid?                 # errors discovered afterwards
```

Axial declares the same constraints on a schema, and parsing either produces the model or refuses to construct it:

```fsharp
type Signup =
    private
        { Email: string
          Age: int }

    /// The only other place `Signup` may legitimately be constructed — a static member has access to its own
    /// type's private fields by definition, so this and the schema below are the two authorized construction paths.
    static member Schema : Schema<Signup> =
        Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> Schema.fieldWith
            [ SchemaConstraint.required; SchemaConstraint.maxLength 254; SchemaConstraint.email ]
            "email" _.Email Value.text
        |> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.int
        |> Schema.build

let parsed = Model.parse Signup.Schema raw

match parsed.Result with
| Ok signup -> signup                  // trusted: constraints already hold
| Error diagnostics -> diagnostics     // no Signup value exists
```

The declaration density matches ActiveModel — external name, getter, constraints in one line per field — but the type
system carries the guarantee forward: every `Signup` in the program passed its schema.

Two things make that guarantee real rather than aspirational. First, `Signup`'s fields are `private` — without that,
nothing stops `{ Email = ""; Age = 10 }` being written directly anywhere in the program, schema or no schema. Second,
declaring the schema as a `static member` on `Signup` itself, rather than in a separate module, keeps the type and
its one sanctioned construction path in the same place — no second name to import, and the schema has the same
privileged access to `Signup`'s private fields that a hand-written smart constructor would.

`private` alone only blocks *other* files and modules — F# privacy is scoped to the enclosing module/file, so code
in the same file as `Signup` could still write a second, competing `{ Email = ...; Age = ... }` literal that bypasses
`Signup.Schema` entirely. If that matters for a given type, put it in its own file with a matching `.fsi` signature
file that only exposes `Schema` (and whatever else is meant to be public) — signature files hide implementation
details from every other file, including files in the same assembly, which closes the gap `private` leaves open.

## What "Trusted" Buys

- downstream code never re-checks `Email` length or `Age` range
- constructors run only after every argument parsed and passed its constraints, so smart constructors can assume
  trusted arguments
- failed parses keep the raw input for redisplay instead of a half-valid object

## Where Constraints Live

- value requirements that always hold → schema constraints (this page)
- invariants across fields that define the model's meaning → constructor results with `Schema.buildResult`
  (see [Schema Boundaries]({{< relref "/error-handling/validation/schema-boundaries.md" >}}))
- workflow-dependent requirements → [Rules](../rules/)
