---
title: "Capabilities.Core.Random.nextInt"
linkTitle: "nextInt"
---

Reads a random integer from the ambient runtime.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.Core.Random.nextInt&#32;<span>minInclusive&#32;maxExclusive</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minInclusive` | <code>int</code> | The inclusive lower bound. |
| `maxExclusive` | <code>int</code> | The exclusive upper bound. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'e,&#32;int</span>&gt;</span></code> | A flow that produces a random integer. |

