---
title: "Flow.Process.live"
linkTitle: "live"
weight: 3000
type: docs
---

 Creates a live process service using an explicit clock for transcript timestamps and durations.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.live&#32;<span>clock&#32;fileSystem&#32;console</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `clock` | <code><a href="../core/t-flow-platformservice-iclock.md">IClock</a></code> |  |
| `fileSystem` | <code><a href="../filesystem/t-flow-filesystem-ifilesystem.md">IFileSystem</a></code> |  |
| `console` | <code><a href="../console/t-flow-console-iconsole.md">IConsole</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-iprocess.md">IProcess</a></code> |  |
