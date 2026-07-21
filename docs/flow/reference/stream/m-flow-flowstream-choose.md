---
title: "Flow.FlowStream.choose"
linkTitle: "choose"
weight: 2203
---

Maps and filters values in one operation.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.choose&#32;<span>chooser&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `chooser` | <code><span>'a&#32;->&#32;<span>'b&#32;option</span></span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'c,&#32;'d,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'c,&#32;'d,&#32;'b</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">choose</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">id</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val id: x: &#39;T -&gt; &#39;T</div>
