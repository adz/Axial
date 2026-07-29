---
title: "Schema.Derive.DeriveSchemaAttribute"
linkTitle: "DeriveSchemaAttribute"
weight: 1500
type: docs
---

Marks a plain record for schema derivation: <code>schemagen</code> generates its permissive schema.
 The advice is to put this on wire DTOs — records that carry no invariants of their own. The attributes
 in this namespace are inert metadata: they are read from source text at generation time, never by
 runtime reflection.

## Signature

<div class="fsdocs-usage">
<code>type DeriveSchemaAttribute</code>
</div>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Derive.fs#L13-13)
