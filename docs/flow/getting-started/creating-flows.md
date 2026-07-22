---
weight: 4
title: Creating Flows
description: Create successful, failed, Task-backed, and Async-backed Flow descriptions.
---

# Creating Flows

`Flow.succeed` creates a description that succeeds with a value:

```fsharp
let greeting : Flow<string> =
    Flow.succeed "Hello"
```

`Flow.fail` creates an expected typed failure:

```fsharp
type LoadError = UserNotFound

let missing : Flow<LoadError, User> =
    Flow.fail UserNotFound
```

Neither value runs when it is created.

Use `Flow.fromTask` or `Flow.fromAsync` when the operation is already represented by a Task or Async and thrown
exceptions are defects:

```fsharp
let readText : Flow<string> =
    Flow.fromTask (File.ReadAllTextAsync "message.txt")
```

Use an `attempt` constructor when thrown exceptions are expected interop failures that callers should handle:

```fsharp
let readText : ExnFlow<string> =
    Flow.attemptTask (File.ReadAllTextAsync "message.txt")
```

The distinction is deliberate. `fromTask` preserves an unexpected exception as a defect; `attemptTask` places it in
the typed error channel.

The [Task and Async interop guide]({{< relref "/flow/core-concepts/task-async-interop/" >}}) covers cancellation and
all supported carriers.
