---
title: "Validation.Schema.Rules.custom"
linkTitle: "custom"
weight: 2405
type: docs
---

Creates a custom schema rule error with a stable code and display message.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.Rules.custom&#32;<span>code&#32;message</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `code` | <code>string</code> | The stable machine-readable rule code. |
| `message` | <code>string</code> | The human-readable rule message. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-validation-schema-schemaerror.md">SchemaError</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">error</span> <span class="o">=</span> <span class="id">Rules</span><span class="pn">.</span><span class="id">custom</span> <span class="s">&quot;ticket.assignee.required&quot;</span> <span class="s">&quot;High-priority tickets need an assignee.&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val error: obj</div>
