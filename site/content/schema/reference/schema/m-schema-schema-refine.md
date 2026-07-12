---
title: "Schema.refine"
linkTitle: "refine"
weight: 2108
type: docs
---

Maps a schema through a fallible smart constructor and lowers its failures to schema errors.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.refine&#32;<span>construct&#32;mapError&#32;inspect&#32;schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `construct` | <code><span>'a&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'b,&#32;'c</span>&gt;</span></span></code> |  |
| `mapError` | <code><span>'c&#32;->&#32;<span><a href="interpreters/t-schema-schemaerror.md">SchemaError</a>&#32;list</span></span></code> |  |
| `inspect` | <code><span>'b&#32;->&#32;'a</span></code> |  |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-schema.md">Schema</a>&lt;'b&gt;</span></code> |  |

## Remarks

Use this for intrinsic domain constraints. <span class="fsdocs-param-name">inspect</span> supplies the raw representation to checking, encoding, and metadata interpreters.
