---
title: "Take.whenNotEmpty"
linkTitle: "whenNotEmpty"
weight: 2100
type: docs
---

Keeps the collection when it is not empty.

## Signature

<div class="fsdocs-usage">
<code><span>Take.whenNotEmpty&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../check/t-check.md">Check</a>&lt;'collection&gt;</span></code> | <code>Ok values</code> for non-empty collections; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="n">1</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">whenNotEmpty</span> <span class="c">// Ok [1]</span>
 <span class="pn">[</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">whenNotEmpty</span> <span class="c">// Error ()</span>
</code></pre>
