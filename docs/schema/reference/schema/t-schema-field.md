---
title: "Schema.Field"
linkTitle: "Field<model, value>"
weight: 1001
---


 Describes one typed field of a trusted model for schema interpreters.


## Signature

<div class="fsdocs-usage">
<code>type Field<'model, 'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `model` |
| `value` |

## Remarks

<p class='fsdocs-para'>
 A field definition records typed field metadata without tying that metadata to input parsing, diagnostics,
 validation, codecs, UI generation, or workflow execution. The field&#39;s external name is the portable boundary-facing
 name interpreters use for structured data lookup, diagnostic paths, codecs, generated documentation, and UI metadata.
 Its getter reads the field value from an already trusted model so inspection interpreters can observe existing
 values without using reflection.
 </p><p class='fsdocs-para'>
 Constructor application, ordering, and public construction helpers are introduced by the schema operations that
 follow this core type.
 </p>
