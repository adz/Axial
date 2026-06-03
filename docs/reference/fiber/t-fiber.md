---
title: "Fiber"
linkTitle: "Fiber<error, value>"
weight: 1000
---


 Represents a handle to a workflow that has already been started.


## Signature

<div class="fsdocs-usage">
<code>type Fiber<'error, 'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `error` |
| `value` |

## Record Fields

| Field | Description |
| --- | --- |
| `ExitTask` | The asynchronous operation that completes with the workflow's final exit outcome. |
| `InterruptSource` | The cancellation source used by <code>Flow.interrupt</code> to signal interruption. |

## Remarks


 A fiber is the hot counterpart to a cold <code>Flow</code>. It keeps the running
 work&#39;s typed failure and success channels available through <code>Flow.join</code>,
 and it carries an interruption source so parent workflows can ask the child
 to stop and then wait for cleanup to finish.
