---
title: "Validation.Schema.RuleSet"
linkTitle: "RuleSet<model, error>"
weight: 1400
type: docs
---


 A collection of contextual rules evaluated over an already-trusted model.


## Signature

<div class="fsdocs-usage">
<code>type RuleSet<'model, 'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `model` |
| `error` |

## Record Fields

| Field | Description |
| --- | --- |
| `Rules` |  |

## Remarks

<p class='fsdocs-para'>
 Schema constraints describe field-local value requirements that can run during input parsing or intrinsic model
 validation. A <code>RuleSet</code> is reserved for contextual requirements that need the completed model and may attach
 failures anywhere in a diagnostics path.
 </p><p class='fsdocs-para'>
 Rule sets do not construct models. They evaluate the supplied model and either accept it or return path-aware
 diagnostics.
 </p>
