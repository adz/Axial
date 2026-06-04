---
title: "Check.notBlank"
linkTitle: "notBlank"
weight: 2247
---

Returns the string when it is not blank.

## Signature

<div class="fsdocs-usage">
<code><span>Check.notBlank&#32;<span>str</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `str` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;string&gt;</span></code> | A <a href="t-check.md">Check</a> containing the non-blank string; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">notBlank</span> <span class="c">// Ok &quot;hello&quot;</span>
 <span class="s">&quot;  &quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">notBlank</span> <span class="c">// Error ()</span>
</code></pre>
