---
title: "DataEdit.rename"
linkTitle: "rename"
weight: 2309
type: docs
---

Describes renaming an existing object field without moving it.

## Signature

<div class="fsdocs-usage">
<code><span>DataEdit.rename&#32;<span>path&#32;name</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `name` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-dataedit.md">DataEdit</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">DataEdit</span><span class="pn">.</span><span class="id">rename</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;displayName&quot;</span> <span class="c">// one DataEdit</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomicsTypes.fs#L108-108)
