---
title: "Schema.RetainedParseResult"
linkTitle: "RetainedParseResult<value>"
weight: 1104
---

A schema parse result that retains its original structured input.

## Signature

<div class="fsdocs-usage">
<code>type RetainedParseResult<'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |

## Record Fields

| Field | Description |
| --- | --- |
| `Input` | The structured boundary data that was parsed. |
| `Result` | The parsed model or accumulated schema failures. |
