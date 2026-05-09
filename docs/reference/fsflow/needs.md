---
title: Needs
linkTitle: Needs
---

Describes the capability contract for a single dependency.


```fsharp
type Needs<'dep>
```


## Remarks

Named cap-set interfaces inherit this contract once and then expose the dependency through a
member such as `Clock` or `Logger`. Workflow builders can accept any environment
that implements `Needs&lt;'dep&gt;`, which lets larger runtimes satisfy smaller
boundaries.


## Information

- **Module**: Global
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L88)

## Examples

```fsharp
type IClock =
    abstract UtcNow : unit -&gt; DateTimeOffset

type ClockCaps =
    inherit Needs&lt;IClock&gt;
    abstract Clock : IClock
```

