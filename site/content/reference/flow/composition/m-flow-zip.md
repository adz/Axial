---
title: "Flow.zip"
linkTitle: "zip"
weight: 2322
type: docs
---

Runs two flows sequentially and combines their successful values into a tuple.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.zip&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'left</span>&gt;</span></code> | The first flow to run. |
| `right` | <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'right</span>&gt;</span></code> | The second flow to run. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;<span>(<span>'left&#32;*&#32;'right</span>)</span></span>&gt;</span></code> | A flow that returns a tuple of both successful values. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">zip</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">1</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">2</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
