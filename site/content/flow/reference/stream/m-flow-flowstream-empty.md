---
title: "Flow.FlowStream.empty"
linkTitle: "empty"
weight: 2100
type: docs
---

Creates an empty stream.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.empty&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">FlowStream</span><span class="pn">.</span><span class="id">empty</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">unit</span><span class="pn">,</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">,</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">int</span><span class="pn">&gt;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">type unit = Unit</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>
