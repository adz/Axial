---
title: "Schema.Value.union"
linkTitle: "union"
weight: 2108
---


 Describes a tagged union value using explicit cases and object input with discriminator and payload fields.


## Signature

<div class="fsdocs-usage">
<code><span>Schema.Value.union&#32;<span>discriminatorField&#32;payloadField&#32;cases</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `discriminatorField` | <code>string</code> |  |
| `payloadField` | <code>string</code> |  |
| `cases` | <code><span><span><a href="t-schema-unioncase.md">UnionCase</a>&lt;'union&gt;</span>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-valueschema.md">ValueSchema</a>&lt;'union&gt;</span></code> |  |

## Remarks


 Input interpreters expect an object with <span class="fsdocs-param-name">discriminatorField</span> containing the case tag and
 <span class="fsdocs-param-name">payloadField</span> containing the case payload, such as
 <code>{ type = &quot;card&quot;; value = { ... } }</code>. Payload schemas may be primitive, refined, nested model,
 collection, or another union value schema.
