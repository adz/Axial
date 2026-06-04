---
title: "Check.failIfEmpty"
linkTitle: "failIfEmpty"
weight: 2231
type: docs
---

Returns the sequence when it is not empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfEmpty&#32;<span>coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `coll` | <code><span>'a&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;<span>'a&#32;seq</span>&gt;</span></code> | A <a href="t-check.md">Check</a> containing the non-empty sequence; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span><span class="n">1</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfEmpty</span> <span class="c">// Ok [1]</span>
 <span class="pn">[</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfEmpty</span> <span class="c">// Error ()</span>
</code></pre>
