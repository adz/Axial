---
title: "Ref"
type: docs
---

The `Ref` module provides thread-safe mutable state handles.

## Core type

- [`FsFlow.Ref`](./t-ref.md): Represents a handle to a mutable reference that can be updated atomically.

## Module functions

- [`FsFlow.RefModule.make`](./m-refmodule-make.md): Creates a new `Ref` with the initial value.
- [`FsFlow.RefModule.get`](./m-refmodule-get.md): Reads the current value of the reference.
- [`FsFlow.RefModule.set`](./m-refmodule-set.md): Sets the value of the reference to the specified value.
- [`FsFlow.RefModule.update`](./m-refmodule-update.md): Updates the value of the reference using the supplied function.
- [`FsFlow.RefModule.modify`](./m-refmodule-modify.md): Updates the value of the reference using the supplied function and returns a derived value.

