---
title: "STM"
weight: 120
type: docs
---

This page shows the STM surface for composable atomic state transitions. STM is for cases where several transactional references must be read and updated as one operation, or where a workflow should wait until state satisfies a condition. Build transactions with `TRef` reads and writes, compose them before execution, then cross back into `Flow` with `STM.atomically`. Use `Ref` for one independent mutable value; use STM when correctness depends on a group of values changing together.

**Note**: The current implementation uses a global synchronizing lock for coordination and is available on .NET only.

## Core types

- [`Flow.TRef`](./t-flow-tref.md):
- [`Flow.STM`](./t-flow-stm.md):

## Module functions

- [`Flow.TRef.make`](./m-flow-tref-make.md): Creates a new <a href="https://learn.microsoft.com/dotnet/api/axial.tref-1">TRef</a> with the initial value within an STM transaction.
- [`Flow.TRef.get`](./m-flow-tref-get.md): Reads the current value of the transactional reference within a transaction.
- [`Flow.TRef.set`](./m-flow-tref-set.md): Sets the value of the transactional reference within a transaction.
- [`Flow.TRef.update`](./m-flow-tref-update.md): Updates the value of the transactional reference within a transaction using the supplied function.
- [`Flow.STM.atomically`](./m-flow-stm-atomically.md):
 Executes an STM transaction atomically within a flow while preserving retry/orElse coordination.


## Builder

- [`stm`](./p-flow-stm-stm.md):
 The <code>stm { }</code> computation expression for building atomic transactions.
