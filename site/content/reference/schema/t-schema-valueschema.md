---
title: "Schema.ValueSchema"
linkTitle: "ValueSchema<value>"
weight: 1001
type: docs
---


 Describes the portable shape of a trusted value for schema interpreters.


## Signature

<div class="fsdocs-usage">
<code>type ValueSchema<'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |

## Remarks

<p class='fsdocs-para'>
 A value schema definition records primitive, refined, collection, optionality, and constraint metadata without tying
 that metadata to input parsing, diagnostics, validation, codecs, UI generation, or workflow execution.
 </p><p class='fsdocs-para'>
 The public construction API is intentionally introduced by the primitive and constraint operations that follow this
 core type.
 </p>
