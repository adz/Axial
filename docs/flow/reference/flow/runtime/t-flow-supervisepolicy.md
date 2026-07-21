---
title: "Flow.SupervisePolicy"
linkTitle: "SupervisePolicy"
weight: 1001
---


 Defines how <code>Flow.Runtime.supervise</code> restarts flows that terminate with unexpected defects.


## Signature

<div class="fsdocs-usage">
<code>type SupervisePolicy</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `MaxAttempts` |  |
| `Delay` |  |
| `ShouldRestart` |  |

## Remarks


 The defect-channel sibling of <a href="t-flow-retrypolicy.md">RetryPolicy</a>: it decides restarts from the
 defect exception rather than the typed error, because defects are bugs that escaped the typed channel.
