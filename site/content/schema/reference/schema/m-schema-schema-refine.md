---
title: "Schema.refine"
linkTitle: "refine"
weight: 2109
type: docs
---

Maps a raw schema through a reusable bidirectional refinement.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.refine&#32;<span>refinement&#32;schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `refinement` | <code><span><a href="/reference/Axial/axial-refined-refinement-2.html">Refinement</a>&lt;<span>'raw,&#32;'value</span>&gt;</span></code> |  |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'raw&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-schema.md">Schema</a>&lt;'value&gt;</span></code> |  |

## Remarks

The smart constructor runs during parsing. Inspection supplies the raw representation during checking and encoding.
