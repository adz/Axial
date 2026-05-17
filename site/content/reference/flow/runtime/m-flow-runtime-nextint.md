---
title: "Flow.Runtime.nextInt"
linkTitle: "nextInt"
type: docs
---

Creates a random integer through the ambient runtime random generator.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.nextInt&#32;<span>minInclusive&#32;maxExclusive</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minInclusive` | <code>int</code> | The inclusive lower bound. |
| `maxExclusive` | <code>int</code> | The exclusive upper bound. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;int</span>&gt;</span></code> | A flow that returns a random integer in the specified range. |

