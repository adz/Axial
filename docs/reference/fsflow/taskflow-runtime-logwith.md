---
title: TaskFlow.Runtime.logWith
linkTitle: logWith
---

Writes a log message computed from the current environment.


```fsharp
let logWith (writer: 'env -> LogEntry -> unit) (level: LogLevel) (messageFactory: 'env -> string) : TaskFlow<'env, 'error, unit>
```




## Information

- **Module**: `TaskFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L636)

