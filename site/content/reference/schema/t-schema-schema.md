---
title: "Schema.Schema"
linkTitle: "Schema<model>"
weight: 1000
type: docs
---


 Describes the portable structure of a trusted model for schema interpreters.


## Signature

<div class="fsdocs-usage">
<code>type Schema<'model></code>
</div>

## Type Parameters

| Name |
| --- |
| `model` |

## Remarks

<p class='fsdocs-para'>
 A schema definition records model structure and construction metadata without tying that metadata to input parsing,
 diagnostics, validation, codecs, UI generation, or workflow execution.
 </p><p class='fsdocs-para'>
 The public construction API is intentionally introduced by the field and value-schema operations that follow this
 core type.
 </p>
