---
title: "Bind"
weight: 76
---

This page shows the `Bind` helpers used when a source needs its error assigned or mapped immediately before `flow { }` binds it. Use `Bind.error` for option or value-option absence and unit-error failures such as `Result<'value, unit>` or `Flow<'env, unit, 'value>`. Use `Bind.mapError` when the source already carries a meaningful error that must be wrapped or translated into the surrounding flow error. The helpers return a `BindError` marker for the flow builder. Do not use `Bind` as a general Result adapter; in pure code use `Result.mapError`, `Result.orError`, or `Validation.mapError`.

## Core type

- [`Flow.BindError`](./t-flow-binderror.md):
 A marker that adapts a source error before <code>flow { }</code> binds it.


## Module functions

- [`Flow.Bind.error`](./m-flow-bind-error.md): Assigns an error to a missing or unit-error source before <code>flow { }</code> binds it.
- [`Flow.Bind.mapError`](./m-flow-bind-maperror.md): Maps an existing source error before <code>flow { }</code> binds it.
