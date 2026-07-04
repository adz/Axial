---
title: "Flow.Flow.fork"
linkTitle: "fork"
weight: 2100
---

Starts a flow in a new fiber without waiting for it to complete.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.fork&#32;<span>flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The flow to fork. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'none,&#32;<span><a href="../../fiber/t-flow-fiber.md">Fiber</a>&lt;<span>'error,&#32;'value</span>&gt;</span></span>&gt;</span></code> | A flow that produces a <a href="https://learn.microsoft.com/dotnet/api/axial.fiber-2">Fiber</a> handle. |

## Remarks


 Forking turns a cold flow description into hot child work and returns a handle
 that can later be joined or interrupted. Prefer <code>zipPar</code> or <code>race</code>
 when the caller only needs a simple parallel composition.
