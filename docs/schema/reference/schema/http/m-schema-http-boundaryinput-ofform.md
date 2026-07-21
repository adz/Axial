---
title: "Schema.Http.BoundaryInput.ofForm"
linkTitle: "ofForm"
weight: 2001
---

Builds structured data from form pairs, where dotted names such as <code>address.street</code> nest.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.BoundaryInput.ofForm&#32;<span>pairs</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `pairs` | <code><span><span>(<span>string&#32;*&#32;string</span>)</span>&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../../data/t-data.md">Data</a></code> |  |

## Remarks


 Repeated names become collections, matching how HTML forms post multi-value fields, and sibling numeric
 segments such as <code>tags.0</code>/<code>tags.1</code> become ordered collections. The dot convention matches the flat
 field names produced when a form is rendered from a schema&#39;s inspection metadata. A name that appears once
 stays a scalar, so a list field submitted with a single selection should be posted as a repeated or indexed
 name; only the schema knows which fields are collections, and this builder deliberately does not.
