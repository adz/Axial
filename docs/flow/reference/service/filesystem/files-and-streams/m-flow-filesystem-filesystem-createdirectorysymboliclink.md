---
title: "Flow.FileSystem.createDirectorySymbolicLink"
linkTitle: "createDirectorySymbolicLink"
weight: 2306
---

Creates a symbolic link to a directory through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FileSystem.FileSystem.createDirectorySymbolicLink&#32;<span>linkPath&#32;targetPath</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `linkPath` | <code>string</code> |  |
| `targetPath` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="../t-flow-filesystem-filesystemerror.md">FileSystemError</a>,&#32;unit</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">FileSystem</span><span class="pn">.</span><span class="id">createDirectorySymbolicLink</span> <span class="s">&quot;current&quot;</span> <span class="s">&quot;releases/v2&quot;</span>
</code></pre>
