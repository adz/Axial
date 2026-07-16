---
title: "Flow.FiberDump"
linkTitle: "FiberDump"
weight: 1004
type: docs
---

Structured diagnostic snapshot of a fiber, taken at a single point in time.

## Signature

<div class="fsdocs-usage">
<code>type FiberDump</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Id` | The fiber id. |
| `Name` | The diagnostic name given at the fork site, if any. |
| `ParentId` | The parent fiber id, if available. |
| `Annotations` | The runtime annotations in scope at the fork site. |
| `StartedAt` | The UTC timestamp when the fiber started. |
| `SettledAt` | The UTC timestamp when the fiber settled, if it had settled when the snapshot was taken. |
| `Status` | The fiber status when the snapshot was taken. |
