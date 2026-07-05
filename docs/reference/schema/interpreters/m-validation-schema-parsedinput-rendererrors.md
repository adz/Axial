---
title: "Validation.Schema.ParsedInput.renderErrors"
linkTitle: "renderErrors"
weight: 2105
---

Renders a failed schema parse as default English display strings, preserving diagnostics paths.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.ParsedInput.renderErrors&#32;<span>parsed</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `parsed` | <code><span><a href="t-validation-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;<a href="t-validation-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;list</span></code> |  |

## Remarks


 This is the one-line display path for boundary parsing failures. Use <code>ParsedInput.mapErrors</code> when the same
 boundary failures should become an application-owned error type instead.
