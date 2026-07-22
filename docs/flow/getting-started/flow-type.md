---
weight: 3
title: The Flow Type
description: Read the success, expected failure, and environment channels of Flow.
---

# The Flow Type

The full Flow type has three parameters:

```fsharp
Flow<'env, 'error, 'value>
```

Read them from left to right:

| Parameter | Meaning |
| --- | --- |
| `'env` | Dependencies supplied when the workflow runs |
| `'error` | Expected failures the caller can handle |
| `'value` | The value produced on success |

For example:

```fsharp
let loadUser (id: UserId) : Flow<AppEnv, LoadUserError, User> = ...
```

The type does not mean that work has started. A Flow is an immutable, cold description. Each execution interprets the
description with an environment and produces an outcome.

Short aliases remove a channel that is not used:

| Alias | Shape |
| --- | --- |
| `Flow<'value>` | No environment and no typed failure |
| `Flow<'error, 'value>` | Typed failure, no environment |
| `EnvFlow<'env, 'value>` | Environment, no typed failure |
| `ExnFlow<'value>` | Recoverable exceptions as typed failures |
| `ExnEnvFlow<'env, 'value>` | Environment and recoverable exceptions |

Start by reading the full shape. Use an alias when it makes a real signature shorter without hiding information.

## Go Further

- [Flow API reference]({{< relref "/flow/reference/flow/" >}}) maps the construction, environment, composition,
  execution, resource, and concurrency functions.
- [Troubleshooting Types]({{< relref "/flow/core-concepts/troubleshooting-types/" >}}) explains the compiler errors
  produced when environment or error channels do not line up.
