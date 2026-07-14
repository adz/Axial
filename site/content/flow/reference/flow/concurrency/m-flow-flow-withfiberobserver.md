---
title: "Flow.withFiberObserver"
linkTitle: "withFiberObserver"
weight: 2104
type: docs
---

Installs runtime fiber-lifecycle hooks for diagnostics and telemetry.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.withFiberObserver&#32;<span>observer&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `observer` | <code><a href="../../fiber/t-flow-fiberobserver.md">FiberObserver</a></code> | The lifecycle hooks. Start from <code>FiberObserver.none</code> and override what you need. |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that runs with the supplied observer in the ambient runtime context. |

## Remarks


 The observer is carried implicitly to every fiber forked inside <span class="fsdocs-param-name">flow</span>, so installing
 it once at the application edge covers all descendant background work. Hooks receive diagnostic data
 only and cannot alter any fiber&#39;s outcome; exceptions they throw are swallowed.
