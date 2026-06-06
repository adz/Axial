---
title: "FileSystem.FileSystem.setDirectoryCreationTimeUtc"
linkTitle: "setDirectoryCreationTimeUtc"
weight: 2603
type: docs
---

Sets directory creation time in UTC through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>FileSystem.FileSystem.setDirectoryCreationTimeUtc&#32;<span>path&#32;time</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `time` | <code><a href="https://learn.microsoft.com/dotnet/api/system.datetime">DateTime</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="../t-filesystem-filesystemerror.md">FileSystemError</a>,&#32;unit</span>&gt;</span></code> |  |
