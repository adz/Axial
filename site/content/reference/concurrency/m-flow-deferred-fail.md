---
title: "Flow.Deferred.fail"
linkTitle: "fail"
weight: 2005
type: docs
---

Attempts to complete the deferred value with a typed failure.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Deferred.fail&#32;<span>error&#32;deferred</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `error` | <code>'error</code> |  |
| `deferred` | <code><span><a href="t-flow-deferred.md">Deferred</a>&lt;<span>'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'workflowError,&#32;bool</span>&gt;</span></code> |  |
