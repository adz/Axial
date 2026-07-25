---
title: "Flow.RetryPolicy"
linkTitle: "RetryPolicy<error>"
weight: 1000
---

 Defines how runtime retry helpers repeat typed failures in a controlled way.

## Signature

<div class="fsdocs-usage">
<code>type RetryPolicy<'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `error` |

## Record Fields

| Field | Description |
| --- | --- |
| `MaxAttempts` |  |
| `Delay` |  |
| `ShouldRetry` |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Flow/Core.fs#L781-781)
