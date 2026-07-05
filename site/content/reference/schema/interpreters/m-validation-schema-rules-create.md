---
title: "Validation.Schema.Rules.create"
linkTitle: "create"
weight: 2501
type: docs
---

Creates a contextual rule set from one executable model rule.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.Rules.create&#32;<span>rule</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `rule` | <code><span>'model&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></span></code> | A rule that accepts the model or returns path-aware diagnostics. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-schema-ruleset.md">RuleSet</a>&lt;<span>'model,&#32;'error</span>&gt;</span></code> |  |
