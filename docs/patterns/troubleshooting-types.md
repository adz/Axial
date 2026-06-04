---
weight: 20
title: Troubleshooting Types
---

# Troubleshooting Types

This page shows the compiler errors that usually mean you crossed a wrapper boundary in the wrong place.

Most FsFlow type errors are not exotic.
The compiler usually sees one wrapper shape and you intended another.

## Error: A Unique Overload For Method `Bind` Could Not Be Determined

This usually happens when the compiler cannot tell which wrapper shape a `let!` value should use.

Example:

```fsharp
let nested : Async<Async<Result<int, string>>> =
    async {
        return async { return Ok 42 }
    }

let workflow : Flow<unit, string, int> =
    flow {
        let! next = nested
        let! value = next
        return value
    }
```

The second `let!` is ambiguous.

Fix it with a type annotation:

```fsharp
let workflow : Flow<unit, string, int> =
    flow {
        let! next = nested
        let! (value: int) = next
        return value
    }
```

## Error: The Flow Requires A Different Environment Type

This usually means you wrote a smaller workflow against one env type (or a specific service contract) and are trying to run it inside a larger env.

Example 1: Records

```fsharp
type SmallEnv = { Prefix: string }
type BigEnv = { App: SmallEnv; RequestId: string }

let greet : Flow<SmallEnv, string, string> =
    flow {
        let! prefix = Flow.read _.Prefix
        return $"{prefix} world"
    }

// Run in BigEnv using localEnv
let greetInBigEnv : Flow<BigEnv, string, string> =
    greet |> Flow.localEnv _.App
```

Example 2: Services

If a helper requires `IHas<IDatabase>` but you are running it in an environment that doesn't implement it, the compiler will error.

```fsharp
let helper : Flow<#IHas<IDatabase>, _, _> = ...

// This fails if AppEnv doesn't implement IHas<IDatabase>
let run (env: AppEnv) = helper.ToTask(env)
```

Fix it by implementing the interface on your environment type.


## Error: `Option` Or `ValueOption` Does Not Match Your Error Type

Implicit option binding only works when the workflow error type is `unit`.

This fails:

```fsharp
let workflow : Flow<unit, string, int> =
    flow {
        let! value = Some 42
        return value
    }
```

Use an explicit adapter when you want a custom error:

```fsharp
let workflow : Flow<unit, string, int> =
    Some 42
    |> Flow.fromOption "missing value"
```

## Error: ColdTask Does Not Match `Task`

Direct `flow { let! ... }` binding supports `ColdTask<'value>` for delayed task work.

`ColdTask<'value>` means:

```fsharp
CancellationToken -> Task<'value>
```

Wrap the factory explicitly:

```fsharp
let load : ColdTask<int> =
    ColdTask(fun _ -> Task.FromResult 42)
```

If you already have a started `Task<'value>`, bind it directly in `flow {}` instead.

## When Type Errors Usually Mean A Boundary Problem

If the compiler error mentions one of these shapes, check the boundary first:

- `Result<...>`
- `Async<...>`
- `Async<Result<...>>`
- `Task<...>`
- `Task<Result<...>>`
- `Flow<...>`

Most fixes are one of:

- add a type annotation to disambiguate `let!` overloads
- derive a smaller local environment with `localEnv`
- use `Guard.Of` or `Guard.MapError` to bridge existing error-bearing sources
- move back to plain Result until the real workflow boundary appears
