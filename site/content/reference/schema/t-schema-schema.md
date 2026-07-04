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
 The public construction path is the progressive typed builder: start with <code>Schema.recordFor&lt;&#39;model, _&gt;</code>,
 append primitive field steps such as <code>Schema.text &quot;name&quot; _.Name</code>, and finish with <code>Schema.build</code>. The
 model-type anchor lets field getters use shorthand member access such as <code>_.Name</code>. <code>Schema.field</code> remains
 available for explicit or custom value schemas, and <code>Schema.record</code> remains available when the model type is
 already clear or getters are annotated explicitly. Computation expressions and source generators can layer over that
 builder later, but they are not required for larger models.
 </p>
