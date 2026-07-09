---
title: "Flow.Runtime.retry"
linkTitle: "retry"
weight: 2111
type: docs
---

Retries typed failures according to the specified policy.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.Runtime.retry&#32;<span>policy&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `policy` | <code><span><a href="t-flow-retrypolicy.md">RetryPolicy</a>&lt;'error&gt;</span></code> | The retry policy. |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that retries <code>Cause.Fail</code> outcomes when the policy allows it. |

## Remarks

Defects and interruptions are not retried.
