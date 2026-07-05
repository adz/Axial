---
title: "Validation.Schema.Input.parseWith"
linkTitle: "parseWith"
weight: 2102
---

Parses raw boundary input through a trusted model schema using custom input parser options.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.Input.parseWith&#32;<span>configure&#32;schema&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `configure` | <code><span><a href="t-validation-schema-input-options.md">Options</a>&#32;->&#32;<a href="t-validation-schema-input-options.md">Options</a></span></code> |  |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `input` | <code><a href="t-validation-schema-rawinput.md">RawInput</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;<a href="t-validation-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |
