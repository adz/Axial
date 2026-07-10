---
title: "Schema.RawInput"
linkTitle: "RawInput"
weight: 1000
type: docs
---


 Source-agnostic raw input captured at a data boundary before schema parsing and diagnostics interpretation.


## Signature

<div class="fsdocs-usage">
<code>type RawInput</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Missing` | The source did not provide a value for the requested input. |
| `Scalar` | A single scalar value represented in its boundary-facing text form. |
| `Many` | An ordered collection of raw input items. |
| `Object` | A named collection of raw input fields. |

## Remarks

<p class='fsdocs-para'><code>RawInput</code> models the small set of shapes shared by form posts, command-line arguments, configuration, JSON-like
 values, and other boundary sources. It deliberately does not carry source-specific metadata, parsed model values, or
 diagnostics; those concerns belong to later input parsing and validation layers.
 </p>
