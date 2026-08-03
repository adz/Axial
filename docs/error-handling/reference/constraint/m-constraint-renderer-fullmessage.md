---
title: "Constraint.Renderer.fullMessage"
linkTitle: "fullMessage"
weight: 2916
---

Composes the attribute noun once around an already-rendered message.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.fullMessage&#32;<span>message&#32;renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `message` | <code>string</code> |  |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Remarks

<code>Violation.fullMessage</code> is this applied to <code>Violation.message</code>. It is public so another
 catalogue — Schema&#39;s, or your own — composes nouns through the same <code>constraint.fullMessage</code> entry
 rather than concatenating a noun itself.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">fullMessage</span> <span class="s">&quot;must be supplied&quot;</span> <span class="c">// &quot;Postcode must be supplied&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L662-662)
