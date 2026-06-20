---
title: "BindError"
weight: 76
---

This page shows the `BindError` marker used when a source needs its error assigned or mapped immediately before `flow { }` binds it. Use `BindError.withError` for option or value-option absence and unit-error failures such as `Result<'value, unit>` or `Flow<'env, unit, 'value>`. Use `BindError.map` when the source already carries a meaningful error that must be wrapped or translated into the surrounding flow error. Do not use `BindError` as a general Result adapter; in pure code use `Check.withError`, `Result.mapError`, or `Validation.mapError`.

## Core type

- [`Flow.BindError`](./t-flow-binderror.md): Pipeable helpers for assigning or mapping errors before a source is bound by <code>flow { }</code>.

## Module functions

- [`Flow.BindError.withError`](./m-flow-binderror-witherror.md): Assigns an error to a missing or unit-error source before <code>flow { }</code> binds it.
- [`Flow.BindError.map`](./m-flow-binderror-map.md): Maps an existing source error before <code>flow { }</code> binds it.
