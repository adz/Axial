---
title: "Schema.ParsedInput"
linkTitle: "ParsedInput<value, error>"
weight: 1100
---


 The result of parsing boundary input through a schema while retaining the original raw input.


## Signature

<div class="fsdocs-usage">
<code>type ParsedInput<'value, 'error></code>
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

<p class='fsdocs-para'><code>ParsedInput</code> is the stable handoff value for schema input parsing. Successful parses carry the trusted model in
 <a href="t-schema-parsedinput.md">ParsedInput</a>; failed parses carry path-aware diagnostics while the
 original <a href="t-schema-rawinput.md">RawInput</a> remains available for redisplay and error lookup.
 </p>
