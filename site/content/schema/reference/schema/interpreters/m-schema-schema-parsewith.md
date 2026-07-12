---
title: "Schema.parseWith"
linkTitle: "parseWith"
weight: 2102
type: docs
---

Parses raw input after configuring parser options.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.parseWith&#32;<span>configure&#32;schema&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `configure` | <code><span><a href="t-schema-schemaparseoptions.md">SchemaParseOptions</a>&#32;->&#32;<a href="t-schema-schemaparseoptions.md">SchemaParseOptions</a></span></code> |  |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |
| `input` | <code><a href="t-schema-rawinput.md">RawInput</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-parsedinput.md">ParsedInput</a>&lt;<span>'a,&#32;<a href="t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |
