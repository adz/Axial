---
title: "Ref.make"
linkTitle: "make"
weight: 2100
type: docs
---

Creates a new <a href="t-ref.md">Ref</a> with the initial value.

## Signature

<div class="fsdocs-usage">
<code><span>Ref.make&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'T</code> | The initial value of the reference. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;'none,&#32;<span><a href="t-ref.md">Ref</a>&lt;'T&gt;</span></span>&gt;</span></code> | A flow that creates and returns the reference. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">(</span><span class="id">Ref</span><span class="pn">.</span><span class="id">make</span> <span class="n">10</span><span class="pn">)</span><span class="pn">.</span><span class="id">RunSynchronously</span><span class="pn">(</span><span class="pn">(</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
