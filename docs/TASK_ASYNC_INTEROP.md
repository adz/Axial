---
weight: 30
title: Task and Async Interop
description: Direct binding rules for async and task work in FsFlow.
---


# Task and Async Interop

This page shows the direct binding surface for async work and helps you choose the right FsFlow family.

Task-oriented APIs on this page belong to the main `FsFlow` package.
The core package includes sync, async, and task concepts together.

## The Main Rule

Choose the boundary shape that matches the runtime shape of the code itself:

- Flow for synchronous boundaries
- direct `Async` binding when the computation already uses F#
- direct `.NET Task` binding when the computation is task-oriented

Use interop to cross boundaries.
Avoid keeping a task-oriented boundary in Flow just because a helper can be adapted.

Use Check for reusable predicates, `Check.orError` for pure failure-to-error bridging,
`Guard.Of` / `Guard.MapError` when the source is already effect-shaped and you want the CE to
bind the source while preserving its attached error,
Result for fail-fast validation, and Validation plus [`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}) when sibling failures should accumulate.

## Preferred Style Inside Computation Expressions

Inside `flow {}`, prefer direct binding:

```fsharp
flow {
    let! user = loadUser
    do! validateUser user
    let! suffix = coldSuffix
    return user.Name + suffix
}
```

The builder already binds Result, Flow, `Async`, `Task`, `ValueTask`, and `ColdTask` where supported.
That means normal docs examples should prefer direct `let!` binding over explicit bridge helpers unless the point of the example is the bridge API itself.

## Direct Binds At A Glance

These are the values you can usually drop directly into `let!` in each builder.
The option and value-option cases bind directly only when the computation error type is `unit`.

| Builder | Binds directly |
| --- | --- |
| `flow {}` | `Flow<'env, 'error, 'value>`, `Result<'value, 'error>`, `option<'value>` when `error = unit`, `voption<'value>` when `error = unit`, `Async<'value>`, `Async<Result<'value, 'error>>`, `Task<'value>`, `Task<Result<'value, 'error>>`, `ValueTask<'value>`, `ValueTask<Result<'value, 'error>>`, `ColdTask<'value>`, `ColdTask<Result<'value, 'error>>` |

### `flow {}`

The builder binds:

- `Flow<'env, 'error, 'value>`
- `Result<'value, 'error>`
- `Option<'value>` when the error type is `unit`
- `ValueOption<'value>` when the error type is `unit`

Use [`flow {}`]({{< relref "/reference/flow/builders-flow.md" >}}) when the body is synchronous.
Use `<!>` for mapping a pure function over a Result or flow value.
Use `<*>` only when the function is already inside the same Result or flow shape.

Example:

```fsharp
let computation : Flow<unit, string, string> =
    flow {
        let! a = async { return "a" }
        let! b = Task.FromResult "b"
        return a + b
    }
```

## When Explicit Lifting Still Matters

For some types you only get the direct bind when the computation error type can stay `unit`.
Use an explicit lift when you want to choose the error value yourself.

For example, `option<'value>` can bind directly in a `unit`-error computation:

```fsharp
let maybeName : string option = None

let autoLifted : Flow<unit, unit, string> =
    flow {
        let! name = maybeName
        return name
    }
```

If you want a typed error such as `"name is required"`, use `Flow.fromOption` instead:

```fsharp
let maybeName : string option = None

let typedError : Flow<unit, string, string> =
    flow {
        let! name = maybeName |> Flow.fromOption "name is required"
        return name
    }
```

Another approach to the same shape is to use `Check.orError` directly:

```fsharp
let typedError : Flow<unit, string, string> =
    flow {
        let! name = maybeName |> Check.okIfSome |> Check.orError "name is required"
        return name
    }
```

For applicative code, keep the distinction clear: `<!>` lifts a pure function into the same shape as the value, and `<*>` applies a function that has already been lifted.

## When To Choose Flow For Async

Prefer Flow when:

- the outer application code already uses `Async`
- you want to stay in core `FsFlow`
- `Async` is the execution model for the computation

## When To Choose Flow For Task

Prefer Flow when:

- the public boundary is `.NET Task`
- task interop is central to the computation
- runtime cancellation belongs in execution
- `Task` is the execution model for the computation.

Use `Flow.Runtime` for shared operational helpers like `sleep`, `timeout`, `retry`, and `useWithAcquireRelease`.

Use `FsFlow.Check` for pure `Result<'value, unit>` validation.
Use `Check.orError` when you want to turn a unit failure into a domain error.
Use Validation and [`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}) when the checks should accumulate.

The builder binds Result directly, so extra bridge calls are only needed when the error value itself needs a different conversion shape.
When the source itself should bind directly in `flow`, wrap it with `Guard.Of` or remap the existing error with `Guard.MapError`. The source stays visible to the CE; Guard only packages the failure value.

## `ColdTask<'value>`

`ColdTask<'value>` is the delayed task shape used by the task surface:

```fsharp
CancellationToken -> Task<'value>
```

Use it when a helper can stay task-based but delayed until the boundary runs.

Example:

```fsharp
let readAll path : ColdTask<string> =
    ColdTask(fun ct -> System.IO.File.ReadAllTextAsync(path, ct))

let computation : Flow<unit, string, string> =
    flow {
        let! text = readAll "config.json"
        return text
    }
```

## Hot `Task` And `ValueTask` Versus ColdTask

Binding a started `Task<'value>` or `ValueTask<'value>` is not the same as binding a `ColdTask<'value>`.

Started task inputs are hot:

- the work may already be running before the boundary starts
- rerunning the boundary re-awaits the same started work
- the current cancellation token cannot be pushed into that work after the fact

ColdTask inputs are cold:

- the work starts when the boundary runs
- rerunning the boundary starts the work again from scratch
- the current cancellation token is passed into the ColdTask factory

Use a direct `Task` or `ValueTask` bind when you intentionally want to reuse existing started work.

Use ColdTask when the task helper is part of the boundary effect and can stay delayed, restartable, and cancellation-aware.

Example with a started task:

```fsharp
let started = Task.FromResult 42

let computation : Flow<unit, string, int> =
    flow {
        let! value = started
        return value
    }
```

Example with delayed task work:

```fsharp
let loadValue : ColdTask<int> =
    ColdTask(fun cancellationToken ->
        Task.FromResult 42)

let computation : Flow<unit, string, int> =
    flow {
        let! value = loadValue
        return value
    }
```

Read [`docs/SEMANTICS.md`](./SEMANTICS.md) when you need the exact rerun and cancellation behavior.

## Choosing Quickly

Use:

- `Flow` for all execution boundaries. The unified type supports sync, `Async`, and `Task` orchestration.
- `ColdTask<'value>` when a task helper can stay delayed, rerunnable, and cancellable at run time.

## Next

Read [`docs/GETTING_STARTED.md`](./GETTING_STARTED.md) for the family overview,
[`docs/TROUBLESHOOTING_TYPES.md`](./TROUBLESHOOTING_TYPES.md) when the compiler complains,
and [`docs/SEMANTICS.md`](./SEMANTICS.md) for the exact runtime behavior.
