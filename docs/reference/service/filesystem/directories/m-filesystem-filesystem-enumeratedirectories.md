---
title: "FileSystem.FileSystem.enumerateDirectories"
linkTitle: "enumerateDirectories"
weight: 2506
---

Enumerates directories through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>FileSystem.FileSystem.enumerateDirectories&#32;<span>path&#32;searchPattern&#32;searchOption</span></span></code>
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
| <code><span><a href="../../../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="../t-filesystem-filesystemerror.md">FileSystemError</a>,&#32;<span>string&#32;seq</span></span>&gt;</span></code> |  |
