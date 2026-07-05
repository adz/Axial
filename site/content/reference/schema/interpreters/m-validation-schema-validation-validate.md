---
title: "Validation.Schema.Validation.validate"
linkTitle: "validate { }"
weight: 2300
type: docs
---

Validates an existing trusted model value through a built model schema.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.Validation.validate&#32;<span>schema&#32;model</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `model` | <code>'model</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../validation/t-validation-validation.md">Validation</a>&lt;<span>'model,&#32;<a href="t-validation-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |

## Remarks


 The validator reads values with schema getters, runs schema constraints through the same executable
 <a href="../../check/t-errorhandling-check.md">Check</a> lowering used by input parsing, and recursively validates nested
 models and collection items. Successful validation returns the original model value.
