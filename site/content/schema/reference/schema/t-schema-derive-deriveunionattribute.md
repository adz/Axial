---
title: "Schema.Derive.DeriveUnionAttribute"
linkTitle: "DeriveUnionAttribute"
weight: 1502
type: docs
---

Marks a discriminated union as an internally tagged union in the derived schema. Every case
 must carry exactly one <code>[&lt;DeriveSchema&gt;]</code> record payload; the discriminator is the given
 external field name.

## Signature

<div class="fsdocs-usage">
<code>type DeriveUnionAttribute</code>
</div>
