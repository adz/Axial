---
title: "Flow.FlowStream.skipWhile"
linkTitle: "skipWhile"
weight: 2209
type: docs
---

Skips values while a predicate remains true.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.skipWhile&#32;<span>predicate&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `predicate` | <code><span>'a&#32;->&#32;bool</span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'a</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">skipWhile</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">String</span><span class="pn">.</span><span class="id">IsNullOrEmpty</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">module String

from Microsoft.FSharp.Core</div>
