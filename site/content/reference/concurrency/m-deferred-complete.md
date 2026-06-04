---
title: "Deferred.complete"
linkTitle: "complete"
weight: 2003
type: docs
---

Attempts to complete the deferred value with a full outcome.

## Signature

<div class="fsdocs-usage">
<code><span>Deferred.complete&#32;<span>exit&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `exit` | <code><span><a href="../exit/t-exit.md">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |
| `arg1` | <code><span><a href="t-deferred.md">Deferred</a>&lt;<span>'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;'workflowError,&#32;bool</span>&gt;</span></code> |  |
