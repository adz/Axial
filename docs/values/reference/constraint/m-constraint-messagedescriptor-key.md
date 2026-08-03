---
title: "Constraint.MessageDescriptor.key"
linkTitle: "key"
weight: 2806
---

The canonical unencoded key, exactly as authored.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.MessageDescriptor.key&#32;<span>descriptor</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `descriptor` | <code><a href="t-constraint-messagedescriptor.md">MessageDescriptor</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Remarks


 This is identity, not a lookup key. The encoded contextual resource keys a lookup receives come from
 <code>Renderer.Advanced.lookupCandidates</code>.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">MessageDescriptor</span><span class="pn">.</span><span class="id">key</span> <span class="id">descriptor</span> <span class="c">// &quot;books.isbn.invalid&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L125-125)
