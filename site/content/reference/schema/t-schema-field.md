---
title: "Schema.Field"
linkTitle: "Field<model, value>"
weight: 1002
type: docs
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
 name interpreters use for raw input lookup, diagnostic paths, codecs, generated documentation, and UI metadata.
 </p><p class='fsdocs-para'>
 Getters, constructor application, ordering, and public construction helpers are introduced by the schema operations
 that follow this core type.
 </p>
