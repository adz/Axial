---
weight: 10
title: Trusted Construction
type: docs
description: ActiveModel ergonomics with F# trusted construction.
---


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
type Signup = { Email: string; Age: int }

let signupSchema =
    Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
    |> Schema.fieldWith
        [ SchemaConstraint.required; SchemaConstraint.maxLength 254; SchemaConstraint.email ]
        "email" _.Email Value.text
    |> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.``int``
    |> Schema.build

let parsed = Input.parse signupSchema raw

match parsed.Result with
| Ok signup -> signup                  // trusted: constraints already hold
| Error diagnostics -> diagnostics     // no Signup value exists
```

The declaration density matches ActiveModel — external name, getter, constraints in one line per field — but the type
system carries the guarantee forward: every `Signup` in the program passed its schema.

## What "Trusted" Buys

- downstream code never re-checks `Email` length or `Age` range
- constructors run only after every argument parsed and passed its constraints, so smart constructors can assume
  trusted arguments
- failed parses keep the raw input for redisplay instead of a half-valid object

## Where Constraints Live

- value requirements that always hold → schema constraints (this page)
- invariants across fields that define the model's meaning → constructor results with `Schema.buildResult`
  (see [Schema Boundaries]({{< relref "/validation/schema-boundaries/" >}}))
- workflow-dependent requirements → [Rules And Policies](../rules-and-policies/)
