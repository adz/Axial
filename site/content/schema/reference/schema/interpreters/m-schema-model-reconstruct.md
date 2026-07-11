---
title: "Schema.Model.reconstruct"
linkTitle: "reconstruct"
weight: 2402
type: docs
---


 Rebuilds trust in an existing model value that did not come through <code>Model.parse</code> or <code>Model.construct</code>
 — for example a value deserialized directly into the model type, or read back from storage.


## Signature

<div class="fsdocs-usage">
<code><span>Schema.Model.reconstruct&#32;<span>schema&#32;model</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `model` | <code>'model</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'model,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;<a href="t-schema-schemaerror.md">SchemaError</a>&gt;</span></span>&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 Runs every field&#39;s schema constraints through the same executable checks <code>Model.parse</code> uses, then
 re-invokes the model&#39;s own constructor with the checked field values. This gives <code>Model.reconstruct</code> the
 same trust strength as <code>Model.parse</code> and <code>Model.construct</code> — including cross-field constructor
 invariants such as <code>DateRange.Create</code>&#39;s &quot;start must not be after end&quot; — rather than only re-checking
 individual field constraints.
 </p><p class='fsdocs-para'>
 Prefer <code>Model.construct</code> when you have the model&#39;s field values but not yet an assembled model; reach
 for <code>Model.reconstruct</code> only for values that already arrived as a fully-built model from outside schema-
 guarded construction.
 </p>
