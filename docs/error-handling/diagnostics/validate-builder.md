---
weight: 30
title: Validate CE
description: Accumulating validation with the validate { } builder.
---

# Validate CE

Use `validate {}` when independent checks should report all their errors together instead of stopping after the
first failure.

## What the keywords do

Suppose the block calls these functions:

```fsharp
let checkName (name: string) : Result<string, RegError> = ...
let checkEmail (email: string) : Result<string, RegError> = ...
let checkAccount (input: Input) : Result<unit, RegError> = ...
let buildRegistration name email : Validation<Registration, RegError> = ...
```

`let!` binds a successful value to the name on its left. Sibling `and!` bindings run independently and collect their
errors. `do!` binds a step returning `unit`, and `return!` uses another validation as the block's result.

```fsharp
validate {
    let! name = checkName input.Name
    and! email = checkEmail input.Email
    do! checkAccount input
    return! buildRegistration name email
}
```

Here is the same block with the left- and right-hand types shown:

```fsharp
validate {
    let! (name: string) =
        (checkName input.Name: Result<string, RegError>)

    and! (email: string) =
        (checkEmail input.Email: Result<string, RegError>)

    do! (checkAccount input: Result<unit, RegError>)

    return!
        (buildRegistration name email: Validation<Registration, RegError>)
}
// Validation<Registration, RegError>
```

## Accumulating with `and!`

The key to accumulation is the `and!` keyword. Steps joined by `and!` are evaluated independently, and their errors are merged into a `Diagnostics` graph.

```fsharp
type Registration = { Name: string; Email: string }
type RegError = NameRequired | EmailRequired

open Axial.ErrorHandling.CheckDSL

let validateRegistration input =
    validate {
        let! name = input.Name |> present |> orError NameRequired
        and! email = input.Email |> present |> orError EmailRequired
        return { Name = name; Email = email }
    }

let outcome = validateRegistration { Name = ""; Email = "" }
// outcome = Validation (Error {
//   Errors = [NameRequired; EmailRequired]
//   Children = []
// })
```

If both fields are blank, the result contains a `Diagnostics` object with both `NameRequired` and `EmailRequired`.

## Sequential Steps in `validate {}`

Standard `let!` and `do!` inside a `validate {}` block still short-circuit. This is useful for "gate" checks that must pass before other validation can proceed.

```fsharp
validate {
    // Stop immediately if the whole object is null
    let! input = input |> Result.notNullOr ObjectMissing
    
    // These run only if input was not null, but they run independently of each other
    let! name = input.Name |> present |> orError NameRequired
    and! email = input.Email |> present |> orError EmailRequired
    
    return { Name = name; Email = email }
}
```

## Relationship with Result

[`Validation<'value, 'error>`]({{< relref "/error-handling/reference/error-handling/t-validation-validation.md" >}}) is structurally similar to `Result<'value, Diagnostics<'error>>`. You can convert between them easily:

- Use [`Validation.toResult`]({{< relref "/error-handling/reference/error-handling/m-validation-validation-toresult.md" >}}) to get a standard result back.
- Use [`Validation.fromResult`]({{< relref "/error-handling/reference/error-handling/m-validation-validation-fromresult.md" >}}), the canonical result-to-validation bridge, to start an accumulating block from an existing result.

Choose [`validate {}`]({{< relref "/error-handling/reference/error-handling/builders-validate.md" >}}) when independent
steps should accumulate diagnostics. If the same operation also uses Flow, convert or bind the resulting value at the
point where the two concerns meet; neither builder determines the application's overall structure.

## Nested Scopes

To build a structured report (e.g., for JSON APIs), use the [`validate.key`]({{< relref "/error-handling/reference/error-handling/m-validation-validation-key.md" >}}), `validate.index`, and `validate.name` helpers. These prefix any diagnostics produced inside the block.

```fsharp
let validateCustomer customer =
    validate.key "customer" {
        let! name = 
            validate.name "Name" {
                return! customer.Name |> present |> orError "Required"
            }
        return name
    }

let v = validateCustomer { Name = "" }
// v = Validation (Error {
//   Errors = []
//   Children = [
//     Key "customer" -> {
//       Errors = []
//       Children = [
//         Name "Name" -> { Errors = ["Required"]; Children = [] }
//       ]
//     }
//   ]
// })
```

Using `Diagnostics.toString v` would render:
```text
customer:
  Name:
  - Required
```

## When to use `validate {}`

- **Forms and User Input**: Where the user wants to see all errors at once.
- **Complex Documents**: Where you need to point failures back to specific paths or indices.
- **Independent Rules**: When rules can be checked in any order.

To learn more about the structure of the accumulated errors, see [Diagnostics Graph](./diagnostics/).
