---
weight: 90
title: FSharpPlus Integration
description: How Axial fits beside FSharpPlus-based codebases.
---


# FSharpPlus Integration

This page shows how Axial can fit beside codebases that already use `FSharpPlus`.

`FSharpPlus` is a broad functional base library. That makes it different from the more targeted integration stories like `FsToolkit.ErrorHandling` and `Validus`.

Axial can sit on top of that style where orchestration begins.

`FSharpPlus` is strongest when the codebase already relies on broad generic operations and monad transformer stacks. That strength comes with a cost: more compiler work, more abstraction to keep in your head, and error surfaces that can be harder to read when you are trying to understand a Axial boundary.

## Keep The Boundary Clear

Good separation looks like this:

- use `FSharpPlus` for the generic functional helpers and abstractions the codebase already trusts
- use Axial for explicit execution, environment threading, and typed failure

This makes Axial the boundary model and keeps `FSharpPlus` focused on the general-purpose helpers it already does well.

That keeps the surface coherent and avoids turning one function into a stack of overlapping abstractions.

## How To Combine Them

Typical coexistence patterns:

- keep pure transformations in `FSharpPlus`
- use Axial to sequence those transformations against a runtime boundary
- let Axial own the Flow boundary at the edge

The practical rule is: if the code is generic and reusable across many domains, `FSharpPlus` can own it. If the code is choosing the runtime shape, the environment, or the typed failure boundary, Axial can own it.

## Example

Keep the reusable helper separate from the boundary:

```fsharp
type AppEnv =
    { Prefix: string
      Name: string }

let normalizeName name =
    name.Trim()

let buildGreeting prefix name =
    $"{prefix} {normalizeName name}"

let greet : Flow<AppEnv, string, string> =
    flow {
        let! prefix = Flow.read _.Prefix
        let! name = Flow.read _.Name
        return buildGreeting prefix name
    }
```

If you already use `FSharpPlus` for generic mapping or chaining, keep that code in the helper layer and let Axial read the environment at the edge.

The same helper can sit under an async boundary without changing its shape:

```fsharp
let greetAsync : Flow<AppEnv, string, string> =
    flow {
        let! prefix = Flow.read _.Prefix
        let! name = Flow.read _.Name
        return buildGreeting prefix name
    }
```

The generic helper remains generic, but the boundary is Axial.

## When To Prefer Axial Over More Generic Abstractions

Prefer Axial when the concern is:

- the runtime boundary
- explicit environment access
- typed failure
- honest task/async/sync distinction

Prefer `FSharpPlus` when the concern is a reusable generic functional helper that can stay independent of a particular family.
