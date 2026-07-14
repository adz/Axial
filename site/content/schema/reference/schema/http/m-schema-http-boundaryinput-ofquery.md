---
title: "Schema.Http.BoundaryInput.ofQuery"
linkTitle: "ofQuery"
weight: 2000
type: docs
---

Builds object-shaped raw input from query-string pairs, grouping repeated names into collections.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.BoundaryInput.ofQuery&#32;<span>pairs</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `pairs` | <code><span><span>(<span>string&#32;*&#32;string</span>)</span>&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../interpreters/t-schema-rawinput.md">RawInput</a></code> |  |

## Remarks

Names are used verbatim; query strings do not carry nesting.
