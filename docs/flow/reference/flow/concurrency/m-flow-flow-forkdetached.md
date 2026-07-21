---
title: "Flow.forkDetached"
linkTitle: "forkDetached"
weight: 2101
---

Starts a flow in a new fiber that is deliberately never awaited.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.forkDetached&#32;<span>flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The flow to fork. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'none,&#32;<span><a href="../../fiber/t-flow-fiber.md">Fiber</a>&lt;<span>'error,&#32;'value</span>&gt;</span></span>&gt;</span></code> | A flow that produces a <a href="https://learn.microsoft.com/dotnet/api/axial.fiber-2">Fiber</a> handle that can still be joined or interrupted. |

## Remarks


 The explicit fire-and-forget: the fiber counts as observed from birth, so a defect it dies with is
 never reported as an unobserved defect through the runtime&#39;s fiber observer. Use this instead of
 discarding a <code>Flow.fork</code> handle when silence is intended; a discarded <code>fork</code> handle whose
 fiber dies of a defect is reported.
