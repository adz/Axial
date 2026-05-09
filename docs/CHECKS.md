---
weight: 10
title: Pure Checks
description: Reusable predicate layer with the Check module.
---

# Pure Checks

`Check<'value>` is the foundation of FsFlow validation. It is a reusable predicate layer that returns `Result<'value, unit>`.

- If the predicate is true, it returns `Ok value`.
- If the predicate is false, it returns `Error ()`.

By returning `unit` on failure, `Check` stays agnostic about your application's error types. You decide what the error means at the boundary where you use it.

## The Two Shapes of Check

1.  **Value-Preserving**: `Check<'value>` - returns the input value on success.
    ```fsharp
    let check = Check.notBlank // string -> Result<string, unit>
    ```
2.  **Gate**: `Check<unit>` - returns `unit` on success (useful for yes/no questions).
    ```fsharp
    let check = Check.okIf (age > 18) // unit -> Result<unit, unit>
    ```

## Attaching Errors with `Check.orError`

Use `Check.orError` to turn a unit failure into a meaningful application error.

```fsharp
type ValidationError =
    | NameRequired
    | InvalidAge

let validateName name =
    name |> Check.notBlank |> Check.orError NameRequired

let validateAge age =
    age |> Check.okIf (age > 0) |> Check.orError InvalidAge
```

## Common Helpers

The `Check` module includes many built-in helpers:

- `Check.notBlank`, `Check.notNull`, `Check.notEmpty`
- `Check.okIf`, `Check.failIf`
- `Check.equal`, `Check.notEqual`
- `Check.okIfSome`, `Check.okIfValueSome`

## Why use Checks?

- **Reusability**: Write a check once, use it in `result {}`, `validate {}`, or any Flow.
- **Composition**: You can combine checks using `Check.and` and `Check.or`.
- **Decoupling**: Pure logic doesn't need to know about your logging or your database.
