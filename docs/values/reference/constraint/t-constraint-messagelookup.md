---
title: "MessageLookup"
linkTitle: "MessageLookup"
weight: 1901
---

The ordinary resource lookup: an encoded resource key in, a translated template out.

## Signature

<div class="fsdocs-usage">
<code>type MessageLookup</code>
</div>

## Remarks


 This is the whole portable integration surface. A dictionary&#39;s <code>TryFind</code>, a JSON bundle, or a resource
 manager wrapper all satisfy it. Axial owns the candidate order, so a lookup only ever answers &quot;do you have this
 exact key&quot;.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L14-14)
