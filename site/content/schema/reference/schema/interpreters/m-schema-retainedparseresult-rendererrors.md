---
title: "Schema.RetainedParseResult.renderErrors"
linkTitle: "renderErrors"
weight: 2107
type: docs
---

Renders a failed schema parse as default English display strings, preserving diagnostics paths.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.RetainedParseResult.renderErrors&#32;<span>parsed</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `parsed` | <code><span><a href="t-schema-retainedparseresult.md">RetainedParseResult</a>&lt;<span>'value,&#32;<a href="t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;list</span></code> |  |

## Remarks


 This is the one-line display path for boundary parsing failures. Use <code>RetainedParseResult.mapErrors</code> when the same
 boundary failures should become an application-owned error type instead.
