---
title: "Check.equalTo"
linkTitle: "equalTo"
weight: 2416
---

Returns success when the actual value equals the expected value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.equalTo&#32;<span>expected&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> | The expected value. |
| `actual` | <code>'value</code> | The actual value. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when equal; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">actual</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">equalTo</span> <span class="id">expected</span>
</code></pre>
