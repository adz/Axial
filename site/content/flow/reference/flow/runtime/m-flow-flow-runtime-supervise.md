---
title: "Flow.Runtime.supervise"
linkTitle: "supervise"
weight: 2112
type: docs
---

Restarts a flow that terminates with an unexpected defect, according to the specified policy.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.Runtime.supervise&#32;<span>policy&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `policy` | <code><a href="t-flow-supervisepolicy.md">SupervisePolicy</a></code> | The supervision policy. |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that re-evaluates <code>Cause.Die</code> outcomes when the policy allows it. |

## Remarks


 The defect-channel sibling of <code>retry</code>: <code>retry</code> re-runs typed <code>Cause.Fail</code> errors and
 never touches defects, while <code>supervise</code> re-runs <code>Cause.Die</code> defects and never touches typed
 errors or interruptions. Each attempt runs inside its own child scope that is closed before the next
 attempt starts, so finalizers registered by a failed attempt are released instead of accumulating
 until the enclosing scope closes. Re-evaluation only resets state that lives inside the flow itself;
 mutable state in the environment is not restored. When attempts are exhausted, the final defect
 propagates as the flow&#39;s exit.
