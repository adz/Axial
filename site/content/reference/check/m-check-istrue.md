---
title: "Check.isTrue"
linkTitle: "isTrue"
weight: 2400
type: docs
---

Returns success when the condition is true.

## Signature

<div class="fsdocs-usage">
<code><span>Check.isTrue&#32;<span>condition</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `condition` | <code>bool</code> | The boolean condition to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when true; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">true</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="c">// Ok ()</span>
 <span class="k">false</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="c">// Error ()</span>
</code></pre>
