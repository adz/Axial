---
title: "Schema.UnionCase.create"
linkTitle: "create"
weight: 2110
type: docs
---


 Describes one tagged union case from a tag, a payload constructor, a payload extractor, and a payload schema.


## Signature

<div class="fsdocs-usage">
<code><span>Schema.UnionCase.create&#32;<span>tag&#32;construct&#32;tryPayload&#32;payload</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `tag` | <code>string</code> |  |
| `construct` | <code><span>'payload&#32;->&#32;'union</span></code> |  |
| `tryPayload` | <code><span>'union&#32;->&#32;<span>'payload&#32;option</span></span></code> |  |
| `payload` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'payload&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-unioncase.md">UnionCase</a>&lt;'union&gt;</span></code> |  |

## Remarks


 Union schemas are explicit and reflection-free. The constructor builds the union case after the payload parses,
 while the extractor lets validation and encoding-oriented interpreters identify the active case of an existing
 trusted union value.
