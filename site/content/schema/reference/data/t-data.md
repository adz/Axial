---
title: "Data"
linkTitle: "Data"
weight: 1000
type: docs
---

A portable tree representing the meaning and shape of unowned structured data.

## Signature

<div class="fsdocs-usage">
<code>type Data</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Null` | A null value. |
| `Text` | A text value. |
| `Number` | A number whose portable lexical token avoids narrowing it to one runtime numeric type. |
| `Bool` | A Boolean value. |
| `List` | An ordered collection of structured values. |
| `Object` | An ordered collection of named structured values. |

## Remarks

<p class='fsdocs-para'>
 Use <code>Data</code> between a source adapter and the code that assigns an application-owned type. It preserves null,
 text, number, Boolean, list, and object distinctions without depending on a serializer, schema system, or boundary
 source.
 </p><p class='fsdocs-para'><code>Data</code> is a structured-value model, not a source syntax tree. It does not model whitespace, comments, source
 locations, or other format-specific syntax. Number values currently retain a lexical token so adapters do not
 narrow arbitrary-size integers, decimal precision, or exponent notation to one runtime numeric type.
 </p>
