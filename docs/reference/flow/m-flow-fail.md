---
title: "Flow.fail"
linkTitle: "fail"
weight: 2304
---

Alias for <code>error</code> that reads well in some call sites.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.fail&#32;<span>failure</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> | The error value to wrap in a failing flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that always fails with the provided error. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">fail</span> <span class="s">&quot;error&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">result</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">run</span> <span class="pn">(</span><span class="pn">)</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">flow</span>
 <span class="c">// result = Failure (Cause.Fail &quot;error&quot;)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
<div popover class="fsdocs-tip" id="fs2">val result: obj</div>
