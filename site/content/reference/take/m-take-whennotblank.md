---
title: "Take.whenNotBlank"
linkTitle: "whenNotBlank"
weight: 2102
type: docs
---

Keeps the string when it is not blank.

## Signature

<div class="fsdocs-usage">
<code><span>Take.whenNotBlank&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../check/t-check.md">Check</a>&lt;string&gt;</span></code> | <code>Ok value</code> for non-blank strings; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;Ada&quot;</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">whenNotBlank</span> <span class="c">// Ok &quot;Ada&quot;</span>
 <span class="s">&quot;&quot;</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">whenNotBlank</span> <span class="c">// Error ()</span>
</code></pre>
