---
title: "Flow.FiberObserver.compose"
linkTitle: "compose"
weight: 2102
---

Combines two observers so every hook runs both, each guarded independently.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FiberObserver.compose&#32;<span>first&#32;second</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `first` | <code><a href="t-flow-fiberobserver.md">FiberObserver</a></code> |  |
| `second` | <code><a href="t-flow-fiberobserver.md">FiberObserver</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-fiberobserver.md">FiberObserver</a></code> |  |

## Remarks

Use this to stack integrations — for example telemetry spans plus logging — from one edge-level install.
