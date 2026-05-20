---
title: "Check.failIfNonEmptyStr"
linkTitle: "failIfNonEmptyStr"
weight: 2244
type: docs
---

Returns success when the string is null or empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfNonEmptyStr&#32;<span>str</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `str` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds if the string is null or empty; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNonEmptyStr</span> <span class="c">// Ok ()</span>
 <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNonEmptyStr</span> <span class="c">// Error ()</span>
</code></pre>



