---
title: logWith
description: API reference for AsyncFlow.Runtime.logWith
---

# logWith

Writes a log entry using a message produced from the environment.


```fsharp
let logWith (writer: 'env -> LogEntry -> unit) (level: LogLevel) (messageFactory: 'env -> string) : AsyncFlow<'env, 'error, unit>
```




## Parameters

- `writer`: The logging function extracted from the environment.
- `level`: The log level.
- `messageFactory`: The function to produce the message from the environment.

## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L451)

