---
title: "FileSystem.FileSystem.openFile"
linkTitle: "openFile"
weight: 2305
---

Opens a file with the specified mode through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>FileSystem.FileSystem.openFile&#32;<span>mode&#32;path</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mode` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.filemode">FileMode</a></code> |  |
| `path` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-services-filesystem-filesystemerror.html">FileSystemError</a>,&#32;<a href="https://learn.microsoft.com/dotnet/api/system.io.filestream">FileStream</a></span>&gt;</span></code> |  |
