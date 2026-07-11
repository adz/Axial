---
title: "Flow.FileSystem.getSymbolicLinkTarget"
linkTitle: "getSymbolicLinkTarget"
weight: 2307
type: docs
---

Returns the immediate target stored in a symbolic link.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FileSystem.FileSystem.getSymbolicLinkTarget&#32;<span>path</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="../t-flow-filesystem-filesystemerror.md">FileSystemError</a>,&#32;<span>string&#32;option</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">FileSystem</span><span class="pn">.</span><span class="id">getSymbolicLinkTarget</span> <span class="s">&quot;current&quot;</span>
</code></pre>
