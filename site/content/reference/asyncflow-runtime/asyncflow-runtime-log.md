---
title: log
type: docs
---

Writes a log entry using the writer provided by the environment.


```fsharp
let log (writer: 'env -> LogEntry -> unit) (level: LogLevel) (message: string) : AsyncFlow<'env, 'error, unit>
```




## Parameters

- `writer`: The logging function extracted from the environment.
- `level`: The log level.
- `message`: The message.

## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L429)

