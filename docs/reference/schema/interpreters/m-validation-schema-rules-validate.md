---
title: "Validation.Schema.Rules.validate"
linkTitle: "validate { }"
weight: 2406
---

Evaluates contextual rules over an already-trusted model.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.Rules.validate&#32;<span>ruleSet&#32;model</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `ruleSet` | <code><span><a href="t-validation-schema-ruleset.md">RuleSet</a>&lt;<span>'model,&#32;'error</span>&gt;</span></code> | The rule set to evaluate. |
| `model` | <code>'model</code> | The already-trusted model to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../validation/t-validation-validation.md">Validation</a>&lt;<span>'model,&#32;'error</span>&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 The supplied model is not constructed, parsed, or transformed. Every rule is evaluated against the same trusted
 instance and any diagnostics are accumulated.
 </p>
