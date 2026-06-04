---
title: "Check.failIfNotEmpty"
linkTitle: "failIfNotEmpty"
weight: 2230
type: docs
---

Returns success when the sequence is empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfNotEmpty&#32;<span>coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `coll` | <code><span>'a&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | A <a href="t-check.md">Check</a> that succeeds if the sequence is empty; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNotEmpty</span> <span class="c">// Ok ()</span>
 <span class="pn">[</span><span class="n">1</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNotEmpty</span> <span class="c">// Error ()</span>
</code></pre>
