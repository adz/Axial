---
title: "Flow.App.start"
linkTitle: "start"
weight: 2100
type: docs
---

Starts a root workflow and returns a handle that owns its lifetime.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.App.start&#32;<span>environment&#32;application</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The explicit environment supplied to the root workflow. |
| `application` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The root workflow to start. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-apphandle.md">AppHandle</a>&lt;<span>'error,&#32;'value</span>&gt;</span></code> | A handle for observing completion or requesting coordinated stop. |
