---
title: Builders.asyncFlow
linkTitle: asyncFlow
---

The core `asyncFlow { }` computation expression.



## Remarks

<para>
Use this builder when the runtime boundary is async-first and you need to compose
`Async` work with the same explicit environment model as `Flow`.
</para>
<para>
It is the right landing point for async orchestration that still wants typed failures
instead of exceptions.
</para>
<para>
Use `Guard.Of` for check-like sources and `Guard.MapError` for
existing-error remapping before binding into the async CE. `Guard` keeps the source
visible to the CE and only packages the failure value.
</para>


## Information

- **Module**: `Builders`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Builders.fs#L688)

## Examples

```fsharp
let fetchProfile =
    asyncFlow {
        let! api = AsyncFlow.read (fun env -> env.Api)
        let! profile = api.LoadProfile()
        return profile
    }
```

