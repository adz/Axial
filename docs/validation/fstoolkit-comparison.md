---
weight: 90
title: FsToolkit.ErrorHandling
description: Comparing Axial and FsToolkit.ErrorHandling models and how they work together.
---


# FsToolkit.ErrorHandling

`FsToolkit.ErrorHandling` and Axial overlap around Result composition, but they have different scopes. FsToolkit
offers a broad set of combinators and computation expressions. Axial.ErrorHandling has a smaller Result surface
alongside constraints, accumulated diagnostics, and refined values. Either library—or both—may fit an application.

At data boundaries, Axial also offers the separate `Axial.Schema` package. A `Schema<'model>` is inspectable data: the
same declaration that parses input can emit the JSON Schema/OpenAPI contract
(`JsonSchema.generate`), compiles a JSON codec (`Json.compile`), drives UI metadata (`Inspect.model`), and
redisplays failed form input with path-aware errors. If validation only needs to run, combinators may be enough. When the
same facts must also be published, rendered, or serialized, a schema stops that knowledge from being
re-implemented per consumer.

## The Model Difference

`FsToolkit.ErrorHandling` provides a broad toolbox of helpers for working with Result, 
`AsyncResult`, and `TaskResult` as separate, wrapped types.

Axial separates these concerns by role: [Result]({{< relref "/validation/reference/result/" >}}) helpers
for standard fail-fast values, [Validation]({{< relref "/validation/reference/validation/" >}}) for
accumulated failures, and the separate [Flow]({{< relref "/flow/reference/flow/" >}}) package for effectful workflows.
[Check]({{< relref "/validation/reference/check/" >}}) and
[Refined]({{< relref "/validation/reference/refined/" >}}) cover reusable constraints and values whose
types record successful construction.

Those divisions are available choices, not an application architecture requirement.

## How Things Map

If you use these FsToolkit patterns, here is how they correspond to Axial:

| FsToolkit.ErrorHandling | Axial |
| --- | --- |
| [Result]({{< relref "/validation/reference/result/" >}}).requireTrue | `Result.requireTrue error condition` (same name) |
| [Result]({{< relref "/validation/reference/result/" >}}).requireSome | `opt |> Result.someOr error` |
| `asyncResult { }` | `flow {}` |
| `taskResult { }` | `flow {}` |
| [Validation]({{< relref "/validation/reference/validation/" >}}) helpers | [Validation]({{< relref "/validation/reference/validation/" >}}) and [`validate {}`]({{< relref "/validation/reference/validation/builders-validate.md" >}}) |

## Additional Axial options

Depending on the problem, other Axial packages provide capabilities outside the scope of standard result wrappers:

1.  **Unified Environment**: Every flow has access to an explicit `'env`, removing the need
    to manually thread dependencies through every function.
2.  **Runtime Policies**: Retries, timeouts, and logging are first-class citizens in the `Flow.Runtime` module.
3.  **Task Temperature**: Built-in support for ColdTask, ensuring tasks only start when 
    the flow is actually executed.
4.  **Diagnostics Graph**: A structured, path-aware error graph for complex validation that
    goes beyond a flat list of errors.

## Combining the libraries

There is no required migration boundary. Existing pure helpers can remain in place, and Axial types can be introduced
where their semantics are useful.

- **Keep existing pure helpers**: If you have a library of Result transformation helpers
  from FsToolkit, they can remain. Axial's [`flow {}`]({{< relref "/flow/reference/flow/builders-flow.md" >}}) builder binds Result directly.
- **Move orchestration**: Use Flow when you need to combine those pure functions with I/O, configuration, or operational policies.

## Semantic Boundary

Axial flows are short-circuiting by default. If your current FsToolkit usage leans on
independent validation that should report multiple errors, [`Validation`]({{< relref "/validation/reference/validation/" >}}) and
[`validate {}`]({{< relref "/validation/reference/validation/builders-validate.md" >}}) provide that alternative.

```fsharp
let validateUser cmd =
    validate {
        let! name = requireName cmd.Name
        and! email = requireEmail cmd.Email
        return { cmd with Name = name; Email = email }
    }
```

This ensures that the "accumulating" vs "fail-fast" semantics remain clear in your code.
