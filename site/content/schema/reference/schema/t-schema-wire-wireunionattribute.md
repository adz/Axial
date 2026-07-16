---
title: "Schema.Wire.WireUnionAttribute"
linkTitle: "WireUnionAttribute"
weight: 1402
type: docs
---

Marks a discriminated union as an internally tagged wire union. Every case must carry exactly
 one <code>[&lt;WireSchema&gt;]</code> record payload; the discriminator is the given wire field name.

## Signature

<div class="fsdocs-usage">
<code>type WireUnionAttribute</code>
</div>
