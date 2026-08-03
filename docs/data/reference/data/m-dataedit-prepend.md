---
title: "DataEdit.prepend"
linkTitle: "prepend"
weight: 2307
---

Describes prepending an item to an existing list.

## Signature

<div class="fsdocs-usage">
<code><span>DataEdit.prepend&#32;<span>path&#32;value</span></span></code>
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

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">DataEdit</span><span class="pn">.</span><span class="id">prepend</span> <span class="s">&quot;roles&quot;</span> <span class="s">&quot;admin&quot;</span> <span class="c">// one DataEdit</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomicsTypes.fs#L98-98)
