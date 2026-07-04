---
title: "Flow.Flow.join"
linkTitle: "join"
weight: 2101
---

Waits for a fiber to complete and returns its successful value or typed failure.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.join&#32;<span>fiber</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fiber` | <code><span><a href="../../fiber/t-flow-fiber.md">Fiber</a>&lt;<span>'error,&#32;'value</span>&gt;</span></code> | The fiber to join. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that completes with the fiber&#39;s outcome. |

## Remarks


 Joining preserves the child workflow&#39;s error channel. If the child failed with
 <code>Cause.Fail</code>, the joined flow fails with the same typed error; interruption
 and defects remain interruption and defects.
