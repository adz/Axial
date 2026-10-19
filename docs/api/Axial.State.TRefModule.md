---
title: TRef
api:
  family: Axial.State.TRefModule
  sections:
    - id: create
      title: Create refs
      order: 10
      members: ["make"]
      facets:
        task: [create]
    - id: read-write
      title: Read and write
      order: 20
      members: ["get", "set", "getAndSet"]
      facets:
        task: [read-write]
    - id: update
      title: Update atomically
      order: 30
      members: ["update", "modify", "getAndUpdate", "updateAndGet"]
      facets:
        task: [update]
---
# TRef

`TRef<'T>` is a transactional reference: a variable read and written inside an `STM` transaction. Read the
[STM guide](/concurrency-and-state/stm.html) for `retry` and `orElse` behavior.

## Signature reference

| Function | Signature | Description |
| :--- | :--- | :--- |
| `make` | `'T -> STM<TRef<'T>>` | Creates a new transactional reference. |
| `get` | `TRef<'T> -> STM<'T>` | Reads the value of a `TRef` within a transaction. |
| `set` | `'T -> TRef<'T> -> STM<unit>` | Sets the value of a `TRef` within a transaction. |
| `update` | `('T -> 'T) -> TRef<'T> -> STM<unit>` | Atomically updates a `TRef` within a transaction. |
