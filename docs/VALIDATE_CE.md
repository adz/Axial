---
weight: 30
title: Validate CE
description: Accumulating validation with the validate { } builder.
---

# Validate CE

Use the `validate {}` computation expression when you have multiple independent checks and you want to **collect every failure** into a single report.

While the standard `result {}` or `flow {}` blocks "fail fast" (stopping at the first error), `validate {}` continues checking even after a failure occurs. This is often called "accumulating" semantics.

## Accumulating with `and!`

The key to accumulation is the `and!` keyword. Steps joined by `and!` are evaluated independently, and their errors are merged into a `Diagnostics` graph.

```fsharp
open FsFlow.Check

type Registration = { Name: string; Email: string }
type RegError = NameRequired | EmailRequired

let validateRegistration input =
    validate {
        let! name = input.Name |> notBlank |> orError NameRequired
        and! email = input.Email |> notBlank |> orError EmailRequired
        return { Name = name; Email = email }
    }
```
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
    let! input = input |> notNull |> orError ObjectMissing
    
    // These run only if input was not null, but they run independently of each other
    let! name = input.Name |> notBlank |> orError NameRequired
    and! email = input.Email |> notBlank |> orError EmailRequired
    
    return { Name = name; Email = email }
}
```

## Relationship with Result

[`Validation<'value, 'error>`]({{< relref "/reference/validation/t-validation.md" >}}) is structurally similar to `Result<'value, Diagnostics<'error>>`. You can convert between them easily:

- Use [`Validation.toResult`]({{< relref "/reference/validation/m-validationmodule-toresult.md" >}}) to get a standard result back.
- Use `Result.toValidation` to start an accumulating block from an existing result.

In general, use [`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}) at the "leaves" of your application (like form parsing) and [`flow {}`]({{< relref "/reference/flow/builders-flow.md" >}}) for the "branches" (the main business logic).

## Nested Scopes

To build a structured report (e.g., for JSON APIs), use the [`validate.key`]({{< relref "/reference/validation/m-validationmodule-key.md" >}}), `validate.index`, and `validate.name` helpers. These prefix any diagnostics produced inside the block.

```fsharp
let validateCustomer customer =
    validate.key "customer" {
        let! name = 
            validate.name "Name" {
                return! customer.Name |> notBlank |> orError "Required"
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

To learn more about the structure of the accumulated errors, see [Diagnostics Graph](../diagnostics/).
