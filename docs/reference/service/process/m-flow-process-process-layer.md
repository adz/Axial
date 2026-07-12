---
title: "Flow.Process.layer"
linkTitle: "layer"
weight: 3001
---

 Builds a live process service from an explicit clock as a layer.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.layer&#32;<span>clock&#32;fileSystem&#32;console</span></span></code>
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
| <code><span><a href="../../layer/t-flow-layer.md">Layer</a>&lt;<span>unit,&#32;<a href="../../flow/t-flow-never.md">Never</a>,&#32;<a href="t-flow-process-iprocess.md">IProcess</a></span>&gt;</span></code> |  |
