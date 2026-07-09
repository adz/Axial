---
title: "Flow.FiberDump"
linkTitle: "FiberDump"
weight: 1004
---

Human-readable diagnostic dump for a fiber.

## Signature

<div class="fsdocs-usage">
<code>type FiberDump</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Id` | The fiber id. |
| `ParentId` | The parent fiber id, if available. |
| `StartedAt` | The UTC timestamp when the fiber started. |
| `Status` | The current fiber status. |
