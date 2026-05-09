---
title: taskFlow
---

The .NET `taskFlow { }` computation expression.


```fsharp
let taskFlow
```


## Remarks

<para>
This builder enables using `let!`, `do!`, and other standard computation expression 
features with `TaskFlow`.
</para>
<para>
It supports seamless binding to many types:
<list type="bullet">
<item><description>`TaskFlow` (standard flow)</description></item>
<item><description>`AsyncFlow` (lifts to task-based flow)</description></item>
<item><description>`Flow` (lifts synchronous to task-based)</description></item>
<item><description>`Task` and `Task` (auto-wraps in Ok)</description></item>
<item><description>`ValueTask` and `ValueTask` (auto-wraps in Ok)</description></item>
<item><description>`Result` (lifts pure result to task-based flow)</description></item>
<item><description>`FSharpAsync` (auto-wraps in Ok)</description></item>
</list>
</para>
<para>
It also supports `Guard.Of` and `Guard.MapError` for inline
check-like sources and existing-error remapping before binding into the flow.
</para>


## Information

- **Module**: `TaskBuilders`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L1698)

## Examples

```fsharp
let getUser (id: int) = taskFlow {
    let! db = TaskFlow.read (fun env -> env.Db)
    let! user = db.FindUserAsync(id) |> Guard.Of (UserNotFound id)
    do! Task.Delay(100) // Bind to Task
    return user
}
```

