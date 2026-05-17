---
title: "Capabilities.FileSystem.FileSystem.writeAllText"
linkTitle: "writeAllText"
type: docs
---

Writes all text to a file using the file system environment.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.FileSystem.FileSystem.writeAllText&#32;<span>path&#32;contents</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> | The path of the file to write to. |
| `contents` | <code>string</code> | The string contents to write. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'e,&#32;unit</span>&gt;</span></code> | A flow that performs the file write operation. |

