---
weight: 20
title: Troubleshooting Types
---

# Troubleshooting Types

This page shows the compiler errors that usually mean you picked the wrong workflow family or crossed a wrapper boundary in the wrong place.

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

## Error: This Expression Was Expected To Have Type `Flow<...>` But Here Has Type `Async<...>` Or `Task<...>`

That usually means the workflow family is too small for the runtime shape you are binding.

Example:

```fsharp
flow {
    let! value = async { return 42 }
    return value
}
```

[`flow {}`]({{< relref "builders-flow.md" >}}) is sync-only.

Fix it by moving to the honest boundary:

- use `flow {}` for `Async`
- use `flow {}` for `.NET Task`

Example:

```fsharp
let workflow : Flow<unit, string, int> =
    flow {
        let! value = async { return 42 }
        return value
    }
```

## Error: The Flow Requires A Different Environment Type

This usually means you wrote a smaller workflow against one env type and are trying to run it inside a larger env.

Example:

```fsharp
type SmallEnv = { Prefix: string }
type BigEnv = { App: SmallEnv; RequestId: string }

let greet : Flow<SmallEnv, string, string> =
    flow {
        let! prefix = Flow.read _.Prefix
        return $"{prefix} world"
    }
```

If you want to run it in `BigEnv`, derive the smaller local env from the outer one:

```fsharp
let greetInBigEnv : Flow<BigEnv, string, string> =
    greet |> Flow.localEnv _.App
```

The same rule applies to `Flow.localEnv` and `Flow.localEnv`.

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

The same pattern exists for all workflow types.

## Error: ColdTask Does Not Match `Task`

`Flow.fromTask` and direct `flow { let! ... }` support `ColdTask<'value>` for delayed task work.

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

- ensure the appropriate `flow {}` or `taskFlow {}` builder is used
- add a type annotation to disambiguate `let!` overloads
- derive a smaller local environment with `localEnv`
- move back to plain Result until the real workflow boundary appears

## Next

Read [`docs/GETTING_STARTED.md`](./GETTING_STARTED.md) for the workflow-family overview,
[`docs/TASK_ASYNC_INTEROP.md`](./TASK_ASYNC_INTEROP.md) for the binding surface,
and [`examples/FsFlow.MaintenanceExamples/Program.fs`](https://github.com/adz/FsFlow/blob/main/examples/FsFlow.MaintenanceExamples/Program.fs)
for runnable versions of these shapes.
