---
title: Diagnostics
---

A mergeable validation graph that carries local errors and nested child branches.


```fsharp
type Diagnostics<'error>
```


## Remarks

<para>
`Errors` holds the application errors attached exactly to the current node, while
`Children` holds nested branches keyed by `PathSegment`.
</para>
<para>
This structure allows hierarchical validation to stay navigable before flattening.
Use `flatten` to convert it into a linear list.
</para>


## Information

- **Module**: Global
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L40)

