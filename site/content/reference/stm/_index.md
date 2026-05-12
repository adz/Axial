---
title: "STM"
type: docs
---

The `STM` module provides composable atomic transactions.

## Core types

- [`FsFlow.TRef`](./t-tref.md): Represents a transactional reference that can be updated atomically within an `STM` transaction.
- [`FsFlow.STM`](./t-stm.md): Represents a transactional operation that can be composed and executed atomically.

## Module functions

- [`FsFlow.TRefModule.make`](./m-trefmodule-make.md): Creates a new `TRef` with the initial value.
- [`FsFlow.TRefModule.get`](./m-trefmodule-get.md): Reads the current value of the transactional reference within a transaction.
- [`FsFlow.TRefModule.set`](./m-trefmodule-set.md): Sets the value of the transactional reference within a transaction.
- [`FsFlow.TRefModule.update`](./m-trefmodule-update.md): Updates the value of the transactional reference within a transaction using the supplied function.
- [`FsFlow.STM.atomically`](./m-stm-atomically.md): Executes an STM transaction atomically within a flow.

## Builder

- [`FsFlow.StmBuilders.stm`](./p-stmbuilders-stm.md): The `stm { }` computation expression for building atomic transactions.

