---
title: "Flow.FileSystem.FileSystem.enumerateDirectories"
linkTitle: "enumerateDirectories"
weight: 2506
type: docs
---

Enumerates directories through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FileSystem.FileSystem.enumerateDirectories&#32;<span>path&#32;searchPattern&#32;searchOption</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `searchPattern` | <code>string</code> |  |
| `searchOption` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.searchoption">SearchOption</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;<a href="/reference/Axial/axial-flow-filesystem-filesystemerror.html">FileSystemError</a>,&#32;<span>string&#32;seq</span></span>&gt;</span></code> |  |
