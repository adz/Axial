---
title: "Schema.validate"
linkTitle: "validate { }"
weight: 2110
---

Adds executable value validation to a schema.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.validate&#32;<span>validation&#32;schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `validation` | <code><span>'value&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="interpreters/t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></span></code> |  |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-schema.md">Schema</a>&lt;'value&gt;</span></code> |  |

## Remarks

The validation runs during parsing and when checking an existing value. It remains executable behavior and is not emitted as portable constraint metadata.
