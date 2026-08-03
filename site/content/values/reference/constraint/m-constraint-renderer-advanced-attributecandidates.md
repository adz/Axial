---
title: "Constraint.Renderer.Advanced.attributeCandidates"
linkTitle: "attributeCandidates"
weight: 2922
type: docs
---

Every encoded attribute-noun key, most specific first.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.Advanced.attributeCandidates&#32;<span>renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;list</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Renderer</span><span class="pn">.</span><span class="id">Advanced</span><span class="pn">.</span><span class="id">attributeCandidates</span> <span class="id">renderer</span> <span class="c">// [ &quot;attribute.signup.postcode&quot;; &quot;attribute.postcode&quot; ]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L754-754)
