---
title: "Schema.parseRetainingInput"
linkTitle: "parseRetainingInput"
weight: 2101
type: docs
---

Parses source-neutral raw input while retaining it for redisplay and error lookup.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.parseRetainingInput&#32;<span>schema&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |
| `input` | <code><a href="t-schema-rawinput.md">RawInput</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-retainedparseresult.md">RetainedParseResult</a>&lt;<span>'a,&#32;<a href="t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |
