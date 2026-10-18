# STM

`STM<'T>` is a composable transaction: a value describing reads and writes that commit together. Compose transactions
with `stm { }` before running them atomically. Read the [STM guide](/concurrency-and-state/stm.html) for `retry` and
`orElse` behavior.

## Signature reference

| Function | Signature | Description |
| :--- | :--- | :--- |
| `retry` | `STM<'T>` | Requests that the current branch waits and reruns after a committed change. |
| `orElse` | `STM<'T> -> STM<'T> -> STM<'T>` | Falls back to a second branch when the first branch retries. |
| `atomically` | `STM<'T> -> Flow<'env, 'none, 'T>` | Executes a composed transaction as an atomic flow. |
