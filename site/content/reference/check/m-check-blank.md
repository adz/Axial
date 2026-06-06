---
title: "Check.blank"
linkTitle: "blank"
weight: 2415
type: docs
---

Returns success when the string is blank.

## Signature

<div class="fsdocs-usage">
<code><span>Check.blank&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for null, empty, or whitespace strings; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;  &quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">blank</span> <span class="c">// Ok ()</span>
 <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">blank</span> <span class="c">// Error ()</span>
</code></pre>
