---
title: "Ref.set"
linkTitle: "set"
weight: 2102
---

Sets the value of the reference to the specified value.

## Signature

<div class="fsdocs-usage">
<code><span>Ref.set&#32;<span>value&#32;reference</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'T</code> | The new value to set. |
| `reference` | <code><span><a href="t-ref.md">Ref</a>&lt;'T&gt;</span></code> | The <a href="t-ref.md">Ref</a> to update. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;'none,&#32;unit</span>&gt;</span></code> | A flow that sets the value and returns unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Ref</span><span class="pn">.</span><span class="id">set</span> <span class="n">20</span> <span class="id">myRef</span>
</code></pre>
