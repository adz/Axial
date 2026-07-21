---
title: "Schema.parseWith"
linkTitle: "parseWith"
weight: 2102
---

Parses structured data after configuring parser options.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.parseWith&#32;<span>configure&#32;schema&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `configure` | <code><span><a href="t-schema-schemaparseoptions.md">SchemaParseOptions</a>&#32;->&#32;<a href="t-schema-schemaparseoptions.md">SchemaParseOptions</a></span></code> |  |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |
| `input` | <code><a href="../../data/t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;<span><a href="../../error-handling/diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;<a href="t-schema-schemaerror.md">SchemaError</a>&gt;</span></span>&gt;</span></code> |  |
