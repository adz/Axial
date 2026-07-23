---
title: "Schema.Derive.SchemaConstructorAttribute"
linkTitle: "SchemaConstructorAttribute"
weight: 1503
---

Marks the static member the derived schema calls to assemble the record, instead of a
 record literal. Put it on one static member of a <code>[&lt;DeriveSchema&gt;]</code> record that takes the
 fields in declaration order and returns the record type; use it to normalise values on the way
 in.

## Signature

<div class="fsdocs-usage">
<code>type SchemaConstructorAttribute</code>
</div>
