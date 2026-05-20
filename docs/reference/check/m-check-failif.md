---
title: "Check.failIf"
linkTitle: "failIf"
weight: 2210
---

Returns success when the condition is false.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIf&#32;<span>cond</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `cond` | <code>bool</code> | The boolean condition to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds if <span class="fsdocs-param-name">cond</span> is false. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Check</span><span class="pn">.</span><span class="id">failIf</span> <span class="pn">(</span><span class="n">1</span> <span class="o">=</span> <span class="n">2</span><span class="pn">)</span> <span class="c">// Ok ()</span>
 <span class="id">Check</span><span class="pn">.</span><span class="id">failIf</span> <span class="pn">(</span><span class="n">1</span> <span class="o">=</span> <span class="n">1</span><span class="pn">)</span> <span class="c">// Error ()</span>
</code></pre>



