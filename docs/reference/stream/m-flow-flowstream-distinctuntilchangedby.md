---
title: "Flow.FlowStream.distinctUntilChangedBy"
linkTitle: "distinctUntilChangedBy"
weight: 2212
---

Suppresses consecutive duplicate values according to a projection.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.distinctUntilChangedBy&#32;<span>projection&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `projection` | <code><span>'a&#32;->&#32;'b</span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'c,&#32;'d,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'c,&#32;'d,&#32;'a</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">distinctUntilChangedBy</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">id</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val id: x: &#39;T -&gt; &#39;T</div>
