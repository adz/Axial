---
title: Reading the Type
description: Read the success, expected failure, and environment channels of Flow.
---

# Reading the Type

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

Short aliases are abbreviations for the same three-parameter type. Each one fixes the channels it leaves out:

| Alias | Expands to | Meaning |
| --- | --- | --- |
| `Flow<'value>` | `Flow<unit, Never, 'value>` | No environment and no typed failure |
| `Flow<'error, 'value>` | `Flow<unit, 'error, 'value>` | Typed failure, no environment |
| `EnvFlow<'env, 'value>` | `Flow<'env, Never, 'value>` | Environment, no typed failure |
| `ExnFlow<'value>` | `Flow<unit, exn, 'value>` | Recoverable exceptions as typed failures |
| `ExnEnvFlow<'env, 'value>` | `Flow<'env, exn, 'value>` | Environment and recoverable exceptions |

`unit` in the environment channel means the workflow reads nothing. `Never` is an error type with no values, so a
`Flow<'value>` or `EnvFlow<'env, 'value>` cannot fail with an expected error. Writing the alias and writing the
expansion produce the same type, so the two forms are interchangeable in a signature:

```fsharp
let render : Flow<string> = Flow.succeed "ok"
let renderExpanded : Flow<unit, Never, string> = render
```

Start by reading the full shape. Use an alias when it makes a real signature shorter without hiding information.

## Go Further

- [Flow API reference](/api.html) maps the construction, environment, composition,
  execution, resource, and concurrency functions.
- [Troubleshooting Types](/the-flow-type/troubleshooting-types.html) explains the compiler errors
  produced when environment or error channels do not line up.
