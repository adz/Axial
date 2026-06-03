---
title: "FileSystem.FileSystem.appendAllLinesWithEncoding"
linkTitle: "appendAllLinesWithEncoding"
weight: 2220
---

Appends all lines with the specified encoding through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>FileSystem.FileSystem.appendAllLinesWithEncoding&#32;<span>encoding&#32;path&#32;contents</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `encoding` | <code><a href="https://learn.microsoft.com/dotnet/api/system.text.encoding">Encoding</a></code> |  |
| `path` | <code>string</code> |  |
| `contents` | <code><span>string&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-services-filesystem-filesystemerror.html">FileSystemError</a>,&#32;unit</span>&gt;</span></code> |  |
