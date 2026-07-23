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
 Record declarations use the <code>schema&lt;&#39;value&gt; { }</code> computation expression. Each <code>field</code> may contain
 <code>withSchema</code>, <code>constrain</code>, <code>refine</code>, and <code>validate</code> operations before the declaration finishes
 with <code>construct</code> or <code>constructResult</code>.
 </p>
