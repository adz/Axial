---
title: "DataEdit.set"
linkTitle: "set"
weight: 2303
type: docs
---

Describes replacing a value, or adding a missing final object field.

## Signature

<div class="fsdocs-usage">
<code><span>DataEdit.set&#32;<span>path&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `value` | <code>^a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-dataedit.md">DataEdit</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">DataEdit</span><span class="pn">.</span><span class="id">set</span> <span class="s">&quot;plan&quot;</span> <span class="s">&quot;pro&quot;</span> <span class="c">// one DataEdit</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomicsTypes.fs#L82-82)
