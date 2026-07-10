---
title: "Schema.JsonLikeValue"
linkTitle: "JsonLikeValue"
weight: 1001
---

A small dependency-free value model for adapting JSON-shaped data into <a href="t-schema-rawinput.md">RawInput</a>.

## Signature

<div class="fsdocs-usage">
<code>type JsonLikeValue</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Null` | A JSON null value. |
| `String` | A JSON string value. |
| `Number` | A JSON number, preserved in its boundary-facing text form. |
| `Bool` | A JSON boolean value. |
| `Array` | A JSON array value. |
| `Object` | A JSON object value. |
