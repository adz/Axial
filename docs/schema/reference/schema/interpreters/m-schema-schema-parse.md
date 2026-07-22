---
title: "Schema.parse"
linkTitle: "parse"
weight: 2100
---

Parses source-neutral structured data, runs constraints and refinements, and invokes record constructors.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.parse&#32;<span>schema&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |
| `input` | <code><a href="../../data/t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;<span><a href="../../../../validation/reference/diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;<a href="t-schema-schemaerror.md">SchemaError</a>&gt;</span></span>&gt;</span></code> |  |
