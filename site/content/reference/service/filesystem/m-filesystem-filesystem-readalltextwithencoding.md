---
title: "FileSystem.FileSystem.readAllTextWithEncoding"
linkTitle: "readAllTextWithEncoding"
weight: 2201
type: docs
---

Reads all text with the specified encoding through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>FileSystem.FileSystem.readAllTextWithEncoding&#32;<span>encoding&#32;path</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `encoding` | <code><a href="https://learn.microsoft.com/dotnet/api/system.text.encoding">Encoding</a></code> |  |
| `path` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-filesystem-filesystemerror.md">FileSystemError</a>,&#32;string</span>&gt;</span></code> |  |
