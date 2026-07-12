---
title: "Schema.check"
linkTitle: "check"
weight: 2400
type: docs
---

Checks an already assembled value and re-invokes its record constructor when present.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.check&#32;<span>schema&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |
| `value` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;<a href="t-schema-schemaerror.md">SchemaError</a>&gt;</span></span>&gt;</span></code> |  |
