---
title: "Flow.Ref.set"
linkTitle: "set"
weight: 2102
type: docs
---

Sets the value of the reference to the specified value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Ref.set&#32;<span>value&#32;reference</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'T</code> | The new value to set. |
| `reference` | <code><span><a href="/reference/Axial/axial-flow-ref-1.html">Ref</a>&lt;'T&gt;</span></code> | The <a href="https://learn.microsoft.com/dotnet/api/axial.ref-1">Ref</a> to update. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'none,&#32;unit</span>&gt;</span></code> | A flow that sets the value and returns unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Ref</span><span class="pn">.</span><span class="id">set</span> <span class="n">20</span> <span class="id">myRef</span>
</code></pre>
