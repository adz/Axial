---
title: "Schema.Derive.DeriveUnionAttribute"
linkTitle: "DeriveUnionAttribute"
weight: 1502
---

Marks a discriminated union as an internally tagged union in the derived schema. Every case
 must carry exactly one <code>[&lt;DeriveSchema&gt;]</code> record payload; the discriminator is the given
 external field name.

## Signature

<div class="fsdocs-usage">
<code>type DeriveUnionAttribute</code>
</div>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Derive.fs#L34-34)
