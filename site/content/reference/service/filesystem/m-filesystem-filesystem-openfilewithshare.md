---
title: "FileSystem.FileSystem.openFileWithShare"
linkTitle: "openFileWithShare"
weight: 2307
type: docs
---

Opens a file with the specified mode, access, and sharing behavior through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>FileSystem.FileSystem.openFileWithShare&#32;<span>mode&#32;access&#32;share&#32;path</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mode` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.filemode">FileMode</a></code> |  |
| `access` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.fileaccess">FileAccess</a></code> |  |
| `share` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.fileshare">FileShare</a></code> |  |
| `path` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-filesystem-filesystemerror.md">FileSystemError</a>,&#32;<a href="https://learn.microsoft.com/dotnet/api/system.io.filestream">FileStream</a></span>&gt;</span></code> |  |
