---
title: "Flow.FiberMetadata"
linkTitle: "FiberMetadata"
weight: 1003
type: docs
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
