---
title: "Capabilities.FileSystem.FileSystem.exists"
linkTitle: "exists"
---

Checks if a file exists using the file system environment.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.FileSystem.FileSystem.exists&#32;<span>path</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> | The path of the file to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'e,&#32;bool</span>&gt;</span></code> | A flow that produces true if the file exists, false otherwise. |

