---
title: "STM"
---

The `STM` module provides composable atomic transactions with `retry` / `orElse` coordination.

## Core types

- [`TRef`](./t-tref-1.md): 
 Represents a transactional reference that can be updated atomically within an <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-stm-1.html">STM</a> transaction.
 
- [`STM`](./t-stm-1.md): 
 Represents a transactional operation that can be composed, retried, and executed atomically.
 

## Module functions

- [`TRef.make`](./m-tref-make.md): Creates a new <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-tref-1.html">TRef</a> with the initial value.
- [`TRef.get`](./m-tref-get.md): Reads the current value of the transactional reference within a transaction.
- [`TRef.set`](./m-tref-set.md): Sets the value of the transactional reference within a transaction.
- [`TRef.update`](./m-tref-update.md): Updates the value of the transactional reference within a transaction using the supplied function.
- [`STM.atomically`](./m-stm-atomically.md): 
 Executes an STM transaction atomically within a flow while preserving retry/orElse coordination.
 

## Builder

- [`Stm.stm`](./p-stm-stm.md): 
 The <code>stm { }</code> computation expression for building atomic transactions.
 

