---
title: "Flow.FlowStream.runForEachFlow"
linkTitle: "runForEachFlow"
weight: 2401
---

Runs an effectful action for every stream value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.runForEachFlow&#32;<span>action&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `action` | <code><span>'a&#32;->&#32;<span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">runForEachFlow</span> <span class="id">save</span>
</code></pre>
