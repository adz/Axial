---
title: "Schema.RefinedSchemas.dateRange"
linkTitle: "dateRange"
weight: 2307
type: docs
---


 Builds a schema for a range of instants using <code>start</code> and <code>end</code> field
 names. The same <code>Interval</code> type as <code>interval</code> above — only the wire
 vocabulary differs, which is why no second type is needed. An inverted pair is
 reported rather than silently reordered, since at a boundary that is a caller error.


## Signature

<div class="fsdocs-usage">
<code><span>Schema.RefinedSchemas.dateRange&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-schema-schema.md">Schema</a>&lt;<a href="/reference/Axial/axial-refined-daterange.html">DateRange</a>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/RefinedSchemas.fs#L56-56)
