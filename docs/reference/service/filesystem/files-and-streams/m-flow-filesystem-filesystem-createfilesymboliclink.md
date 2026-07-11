---
title: "Flow.FileSystem.createFileSymbolicLink"
linkTitle: "createFileSymbolicLink"
weight: 2305
---

Creates a symbolic link to a file through an explicit file-system service.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FileSystem.FileSystem.createFileSymbolicLink&#32;<span>linkPath&#32;targetPath</span></span></code>
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

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">FileSystem</span><span class="pn">.</span><span class="id">createFileSymbolicLink</span> <span class="s">&quot;current.json&quot;</span> <span class="s">&quot;releases/v2.json&quot;</span>
</code></pre>
