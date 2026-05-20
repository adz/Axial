---
title: "Check.okIfNonEmptyStr"
linkTitle: "okIfNonEmptyStr"
weight: 2242
type: docs
---

Returns the string when it is not null or empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfNonEmptyStr&#32;<span>str</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `str` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;string&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the non-empty string; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNonEmptyStr</span> <span class="c">// Ok &quot;hello&quot;</span>
 <span class="s">&quot;&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNonEmptyStr</span> <span class="c">// Error ()</span>
</code></pre>



