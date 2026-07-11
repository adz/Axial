---
title: "Schema.ContextRules.atField"
linkTitle: "atField"
weight: 2502
type: docs
---

Scopes a rule&#39;s diagnostics under a schema field reference when the rule fails.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.ContextRules.atField&#32;<span>field&#32;rule</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `field` | <code><span><a href="t-schema-fieldref.md">FieldRef</a>&lt;<span>'model,&#32;'field</span>&gt;</span></code> | The schema field reference whose path should scope the rule&#39;s diagnostics. |
| `rule` | <code><span>'model&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></span></code> | The rule whose failures should be scoped. |

## Returns

| Type | Description |
| --- | --- |
| <code><span>'model&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></span></code> |  |

## Remarks


 Unlike <code>ContextRules.name</code>, the path comes from a typed <a href="t-schema-fieldref.md">FieldRef</a>
 declared next to the schema, so the field name cannot silently drift from the schema&#39;s actual wire name.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">scoped</span> <span class="o">=</span> <span class="id">needsAssignee</span> <span class="o">|&gt;</span> <span class="id">ContextRules</span><span class="pn">.</span><span class="id">atField</span> <span class="id">Ticket</span><span class="pn">.</span><span class="id">Fields</span><span class="pn">.</span><span class="id">assignee</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val scoped: obj</div>
