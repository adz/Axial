---
title: "Schema.Schema"
linkTitle: "Schema<model>"
weight: 1000
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
 The public construction path is the progressive typed builder: start with <code>Schema.record</code>, append
 <code>Schema.field</code> steps, and finish with <code>Schema.build</code>. Computation expressions and source generators can
 layer over that builder later, but they are not required for larger models.
 </p>
