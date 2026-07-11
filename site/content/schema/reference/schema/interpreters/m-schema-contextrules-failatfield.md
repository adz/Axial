---
title: "Schema.ContextRules.failAtField"
linkTitle: "failAtField"
weight: 2504
type: docs
---

Creates a rule failure attached to a schema field reference&#39;s diagnostics path.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.ContextRules.failAtField&#32;<span>field&#32;error</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `field` | <code><span><a href="t-schema-fieldref.md">FieldRef</a>&lt;<span>'model,&#32;'field</span>&gt;</span></code> | The schema field reference that should receive the failure. |
| `error` | <code>'error</code> | The rule error to attach. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">result</span> <span class="o">=</span> <span class="id">ContextRules</span><span class="pn">.</span><span class="id">failAtField</span> <span class="id">Ticket</span><span class="pn">.</span><span class="id">Fields</span><span class="pn">.</span><span class="id">assignee</span> <span class="id">HighPriorityNeedsAssignee</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val result: obj</div>
