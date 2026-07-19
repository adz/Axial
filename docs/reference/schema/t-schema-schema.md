---
title: "Schema"
linkTitle: "Schema<model>"
weight: 1000
---


 Describes a typed value&#39;s portable structure and construction for schema interpreters.


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
 A schema records shape and construction metadata without tying that metadata to input parsing,
 diagnostics, validation, codecs, UI generation, or workflow execution.
 </p><p class='fsdocs-para'>
 Primitive, collection, optional, union, refined, and record declarations all produce <code>Schema&lt;&#39;value&gt;</code>.
 Object declarations start with <code>Schema.define</code>, add fields through <code>Syntax</code>, and finish with
 <code>Syntax.construct</code> or <code>Syntax.constructResult</code>.
 </p>
