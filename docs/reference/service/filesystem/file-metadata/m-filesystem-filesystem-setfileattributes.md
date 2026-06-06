---
title: "FileSystem.FileSystem.setFileAttributes"
linkTitle: "setFileAttributes"
weight: 2401
---

Sets file attributes through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>FileSystem.FileSystem.setFileAttributes&#32;<span>path&#32;attributes</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `attributes` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.fileattributes">FileAttributes</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="../t-filesystem-filesystemerror.md">FileSystemError</a>,&#32;unit</span>&gt;</span></code> |  |
