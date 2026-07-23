---
title: "Schema.union"
linkTitle: "union"
weight: 2111
---

Describes an externally tagged union.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.union&#32;<span>discriminator&#32;payload&#32;cases</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `discriminator` | <code>string</code> |  |
| `payload` | <code>string</code> |  |
| `cases` | <code><span><span><a href="t-schema-unioncase.md">UnionCase</a>&lt;'a&gt;</span>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |
