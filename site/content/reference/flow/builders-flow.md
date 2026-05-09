---
title: flow
type: docs
---

The sync-only `flow { }` computation expression.


```fsharp
let flow
```


## Remarks

<para>
Use this builder when the boundary is synchronous and you want explicit environment
reads without introducing async or task scheduling.
</para>
<para>
It is the simplest builder in the library and is a good default for pure composition
and deterministic orchestration.
</para>
<para>
Use `Guard.Of` for check-like sources such as `option`, `voption`,
`bool`, and `Result&lt;_, unit&gt;`. The CE then binds the resulting
source value directly while the supplied error stays attached to the failure path.
</para>
<para>
Use `Guard.MapError` when the source already carries an error and you want to keep the
same source shape while changing the error type.
</para>


## Information

- **Module**: `Builders`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Builders.fs#L658)

## Examples

```fsharp
let greeting =
    flow {
        let! name = Flow.read (fun env -> env.Name)
        return $"Hello, {name}"
    }
```

