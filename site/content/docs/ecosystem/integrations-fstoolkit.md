---
weight: 10
title: FsToolkit.ErrorHandling
description: Comparing Axial and FsToolkit.ErrorHandling models and how they work together.
type: docs
---



If you are coming from `FsToolkit.ErrorHandling`, you will find that Axial is orthogonal rather
than a direct replacement. While both libraries help with result-based programming, Axial
focuses on a unified execution model that carries environments and runtime policies.

## The Model Difference

`FsToolkit.ErrorHandling` provides a broad toolbox of helpers for working with Result, 
`AsyncResult`, and `TaskResult` as separate, wrapped types.

Axial provides a single, scalable progression:

[Check]({{< relref "/reference/check/" >}}) -> [Result]({{< relref "/reference/result/" >}}) -> [Validation]({{< relref "/reference/validation/" >}}) -> [Flow]({{< relref "/reference/flow/" >}})

In Axial, the environment and runtime concerns are baked into the computation, allowing you to
write orchestration logic that remains agnostic of whether the underlying work is sync or async
until it hits the application boundary.

## How Things Map

If you use these FsToolkit patterns, here is how they correspond to Axial:

| FsToolkit.ErrorHandling | Axial |
| --- | --- |
| [Result]({{< relref "/reference/result/" >}}).requireTrue | `condition |> Check.isTrue |> Check.orError error` |
| [Result]({{< relref "/reference/result/" >}}).requireSome | `opt |> Check.takeSome |> Check.orError error` |
| `asyncResult { }` | `flow {}` |
| `taskResult { }` | `flow {}` |
| [Validation]({{< relref "/reference/validation/" >}}) helpers | [Validation]({{< relref "/reference/validation/" >}}) and [`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}) |

## New Things You Get

By using Axial for your orchestration layer, you gain several benefits not present in
standard result wrappers:

1.  **Unified Environment**: Every flow has access to an explicit `'env`, removing the need
    to manually thread dependencies through every function.
2.  **Runtime Policies**: Retries, timeouts, and logging are first-class citizens in the `Flow.Runtime` module.
3.  **Task Temperature**: Built-in support for ColdTask, ensuring tasks only start when 
    the flow is actually executed.
4.  **Diagnostics Graph**: A structured, path-aware error graph for complex validation that
    goes beyond a flat list of errors.

## Getting the Most Benefit

You will get the most benefit from Axial by using it at your **application boundaries** (e.g., 
API handlers, background jobs) while keeping your **pure domain logic** in plain Result 
functions.

- **Keep existing pure helpers**: If you have a library of Result transformation helpers
  from FsToolkit, keep using them! Axial's [`flow {}`]({{< relref "/reference/flow/builders-flow.md" >}}) builders bind Result directly.
- **Move orchestration**: Use Flow when you need to combine those pure functions with I/O, configuration, or operational policies.

## Semantic Boundary

Axial flows are short-circuiting by default. If your current FsToolkit usage leans on
independent validation that should report multiple errors, use [`Validation`]({{< relref "/reference/validation/" >}}) and
[`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}) to maintain that explicit concern.

```fsharp
let validateUser cmd =
    validate {
        let! name = requireName cmd.Name
        and! email = requireEmail cmd.Email
        return { cmd with Name = name; Email = email }
    }
```

This ensures that the "accumulating" vs "fail-fast" semantics remain clear in your code.
