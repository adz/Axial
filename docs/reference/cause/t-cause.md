---
title: "Cause"
linkTitle: "Cause<error>"
weight: 1000
---


 Represents the cause of a failed workflow.


## Signature

<div class="fsdocs-usage">
<code>type Cause<'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `error` |

## Union Cases

| Case | Description |
| --- | --- |
| `Fail` | An expected domain-specific failure. |
| `Die` | An unexpected defect or panic (e.g., an exception). |
| `Interrupt` | An administrative signal to stop the workflow (e.g., cancellation). |
| `Then` | Two causes happened sequentially; the left cause happened before the right cause. |
| `Both` | Two causes happened concurrently; neither cause is ordered before the other. |
| `Traced` | A cause annotated with diagnostic trace text. |
