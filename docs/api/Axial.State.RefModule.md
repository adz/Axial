# Ref

`Ref<'T>` holds a mutable value that concurrent workflows update atomically. Create one with
[`make`](#Axial.State.Ref.make), read it with [`get`](#Axial.State.Ref.get), and write it with
[`set`](#Axial.State.Ref.set) or the atomic update family. Read the [Ref guide](/concurrency-and-state/ref.html) for
the atomicity and get/set pitfalls.

## Signature reference

| Function | Signature | Description |
| :--- | :--- | :--- |
| `make` | `'T -> Flow<'env, 'none, Ref<'T>>` | Creates a new reference with an initial value. |
| `get` | `Ref<'T> -> Flow<'env, 'none, 'T>` | Reads the current value of the reference. |
| `set` | `'T -> Ref<'T> -> Flow<'env, 'none, unit>` | Sets the value of the reference to a new value. |
| `update` | `('T -> 'T) -> Ref<'T> -> Flow<'env, 'none, unit>` | Atomically updates the value using the supplied function. |
| `modify` | `('T -> 'v * 'T) -> Ref<'T> -> Flow<'env, 'none, 'v>` | Atomically updates the value and returns a derived result. |
| `getAndSet` | `'T -> Ref<'T> -> Flow<'env, 'none, 'T>` | Sets the value and returns the value it held before the update. |
| `getAndUpdate` | `('T -> 'T) -> Ref<'T> -> Flow<'env, 'none, 'T>` | Updates the value and returns the value it held before the update. |
| `updateAndGet` | `('T -> 'T) -> Ref<'T> -> Flow<'env, 'none, 'T>` | Updates the value and returns the value after the update. |

The [refs torture test](/concurrency-and-state/torture-tests/refs.html) runs every update operation from eight
fibers at once and checks that no update is lost and no two see the same value.
