---
title: "Flow.Exit"
linkTitle: "Exit<value, error>"
weight: 1000
---


 Represents the final outcome of a workflow execution.


## Signature

<div class="fsdocs-usage">
<code>type Exit<'value, 'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |
| `error` |

## Union Cases

| Case | Description |
| --- | --- |
| `Success` | The workflow completed successfully. |
| `Failure` | The workflow failed due to a specific cause. |
