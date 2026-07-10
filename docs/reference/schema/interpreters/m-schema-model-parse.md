---
title: "Schema.Model.parse"
linkTitle: "parse"
weight: 2101
---

Parses raw boundary input through a trusted model schema.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Model.parse&#32;<span>schema&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `input` | <code><a href="t-schema-rawinput.md">RawInput</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;<a href="t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |
