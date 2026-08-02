---
title: "DataEdit.insert"
linkTitle: "insert"
weight: 2308
---

Describes inserting an item at a valid list index.

## Signature

<div class="fsdocs-usage">
<code><span>DataEdit.insert&#32;<span>path&#32;index&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `index` | <code>int</code> |  |
| `value` | <code>^a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-dataedit.md">DataEdit</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">DataEdit</span><span class="pn">.</span><span class="id">insert</span> <span class="s">&quot;roles&quot;</span> <span class="n">1</span> <span class="s">&quot;admin&quot;</span> <span class="c">// one DataEdit</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomicsTypes.fs#L102-102)
