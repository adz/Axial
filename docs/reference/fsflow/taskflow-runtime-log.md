---
title: log
---

Writes a fixed log message through the environment-provided logger.


```fsharp
let log (writer: 'env -> LogEntry -> unit) (level: LogLevel) (message: string) : TaskFlow<'env, 'error, unit>
```




## Information

- **Module**: `TaskFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L621)

