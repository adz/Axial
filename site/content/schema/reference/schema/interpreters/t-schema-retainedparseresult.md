---
title: "Schema.RetainedParseResult"
linkTitle: "RetainedParseResult<value, error>"
weight: 1104
type: docs
---


 A parse result that retains the original raw input for redisplay and error lookup.


## Signature

<div class="fsdocs-usage">
<code>type RetainedParseResult<'value, 'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |
| `error` |

## Record Fields

| Field | Description |
| --- | --- |
| `Input` | The raw boundary input that was parsed. |
| `Result` | The parsed model or path-aware parse diagnostics. |

## Remarks

<p class='fsdocs-para'><code>RetainedParseResult</code> is an opt-in handoff value for boundaries that need the source representation after
 parsing. Successful parses carry the trusted value in <a href="t-schema-retainedparseresult.md">RetainedParseResult</a>;
 failed parses carry path-aware diagnostics while the
 original <a href="t-schema-rawinput.md">RawInput</a> remains available for redisplay and error lookup.
 </p>
