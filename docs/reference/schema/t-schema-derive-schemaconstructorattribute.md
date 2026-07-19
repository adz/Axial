---
title: "Schema.Derive.SchemaConstructorAttribute"
linkTitle: "SchemaConstructorAttribute"
weight: 1403
---

Names the function the derived schema calls to assemble the record, instead of a record
 literal. The function takes the fields in declaration order and returns the record type; use it to
 normalise values on the way in. The name is emitted verbatim into the generated code, so qualify it
 as the generated module would (e.g. <code>&quot;Order.create&quot;</code>). Declare it as a static member on the
 record: the generated module takes the record&#39;s name, so a user module of the same name would not
 compile.

## Signature

<div class="fsdocs-usage">
<code>type SchemaConstructorAttribute</code>
</div>
