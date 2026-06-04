---
title: "STM"
weight: 120
type: docs
---

This page shows the STM surface for composable atomic state transitions. STM is for cases where several transactional references must be read and updated as one operation, or where a workflow should wait until state satisfies a condition. Build transactions with `TRef` reads and writes, compose them before execution, then cross back into `Flow` with `STM.atomically`. Use `Ref` for one independent mutable value; use STM when correctness depends on a group of values changing together.

**Note**: The current implementation uses a global synchronizing lock for coordination and is available on .NET only.

## Core types

- [`TRef`](./t-tref.md):
- [`STM`](./t-stm.md):

## Module functions

- [`TRef.make`](./m-tref-make.md): Creates a new <a href="t-tref.md">TRef</a> with the initial value within an STM transaction.
- [`TRef.get`](./m-tref-get.md): Reads the current value of the transactional reference within a transaction.
- [`TRef.set`](./m-tref-set.md): Sets the value of the transactional reference within a transaction.
- [`TRef.update`](./m-tref-update.md): Updates the value of the transactional reference within a transaction using the supplied function.
- [`STM.atomically`](./m-stm-atomically.md):
 Executes an STM transaction atomically within a flow while preserving retry/orElse coordination.


## Builder

- [`stm`](./p-stm-stm.md):
 The <code>stm { }</code> computation expression for building atomic transactions.
