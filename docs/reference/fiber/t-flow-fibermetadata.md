---
title: "Flow.FiberMetadata"
linkTitle: "FiberMetadata"
weight: 1003
---

Diagnostic metadata for a running fiber.

## Signature

<div class="fsdocs-usage">
<code>type FiberMetadata</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Id` | The unique fiber id. |
| `ParentId` | The parent fiber id, if the fiber was forked from another fiber. |
| `StartedAt` | The UTC timestamp when the fiber started. |
| `Status` | The current fiber status. |
| `Observed` |
 Whether the fiber&#39;s outcome was consumed (<code>Flow.join</code>, <code>Flow.interrupt</code>) or explicitly
 detached at birth (<code>Flow.forkDetached</code>). A fiber that dies with a defect while unobserved is
 reported through the runtime&#39;s fiber observer once no observation can happen anymore.
  |
