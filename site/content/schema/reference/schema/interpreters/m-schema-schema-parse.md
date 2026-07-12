---
title: "Schema.parse"
linkTitle: "parse"
weight: 2101
type: docs
---

Parses source-neutral raw input, runs constraints and refinements, and invokes record constructors.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.parse&#32;<span>schema&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |
| `input` | <code><a href="t-schema-rawinput.md">RawInput</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-parsedinput.md">ParsedInput</a>&lt;<span>'a,&#32;<a href="t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |
