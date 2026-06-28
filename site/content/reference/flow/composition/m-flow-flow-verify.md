---
title: "Flow.Flow.verify"
linkTitle: "verify"
weight: 2314
type: docs
---

Runs an environment-aware policy against an input value inside a workflow.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.verify&#32;<span>policy&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `policy` | <code><span><a href="/reference/Axial/axial-flow-policy-4.html">Policy</a>&lt;<span>'env,&#32;'error,&#32;'input,&#32;'output</span>&gt;</span></code> | The policy to run. |
| `input` | <code>'input</code> | The input value supplied to the policy. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'output</span>&gt;</span></code> | A flow that succeeds or fails with the policy result. |
