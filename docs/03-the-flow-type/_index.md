---
title: The Flow Type
description: Create, combine, and run Flow values.
---

# The Flow Type

A Flow is an immutable, cold description of work. Nothing runs until an execution interprets the description with an
environment:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let workflow : Flow<AppEnv, LoadUserError, User> =
    flow {
        let! loadUser = Flow.read _.LoadUser
        return! loadUser 42
    }

let completed = workflow |> Flow.startTask live
```

The three type parameters state the whole contract — what the workflow needs, how it can fail, and what it produces:

| Parameter | Meaning |
| --- | --- |
| `'env` | Dependencies supplied when the workflow runs |
| `'error` | Expected failures the caller can handle |
| `'value` | The value produced on success |

Aliases such as `Flow<'value>` and `EnvFlow<'env, 'value>` abbreviate the same type with unused channels fixed.

## In this section

1. [Reading the type](flow-type.html) — the three channels, the aliases, and what each alias expands to.
2. [Creating flows](creating-flows.html) — constructors for values, failures, and interop sources.
3. [Running flows](running-flows.html) — executions, outcomes, and boundary conversions.
4. [The flow builder](flow-ce.html) — `flow { }` binding rules for flows, tasks, and results.
5. [Combining flows](combining-flows.html) — sequencing, mapping, and channel transformations.
6. [Task and async interop](task-async-interop.html) — moving between Flow, `Task`, and `Async`.
7. [Troubleshooting types](troubleshooting-types.html) — the compiler errors produced when channels do not line up.
8. [Resources](resources.html) — acquiring something that must be released.
