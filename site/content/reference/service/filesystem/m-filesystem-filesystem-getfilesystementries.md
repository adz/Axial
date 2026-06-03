---
title: "FileSystem.FileSystem.getFileSystemEntries"
linkTitle: "getFileSystemEntries"
weight: 2509
type: docs
---

Gets files and directories through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>FileSystem.FileSystem.getFileSystemEntries&#32;<span>path&#32;searchPattern&#32;searchOption</span></span></code>
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
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-services-filesystem-filesystemerror.html">FileSystemError</a>,&#32;<span>string&#32;array</span></span>&gt;</span></code> |  |
