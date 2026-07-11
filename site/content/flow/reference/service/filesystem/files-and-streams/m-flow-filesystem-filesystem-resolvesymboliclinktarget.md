---
title: "Flow.FileSystem.resolveSymbolicLinkTarget"
linkTitle: "resolveSymbolicLinkTarget"
weight: 2308
type: docs
---

Resolves a symbolic link target, optionally following the complete chain.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FileSystem.FileSystem.resolveSymbolicLinkTarget&#32;<span>returnFinalTarget&#32;path</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `returnFinalTarget` | <code>bool</code> |  |
| `path` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="../t-flow-filesystem-filesystemerror.md">FileSystemError</a>,&#32;<span>string&#32;option</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">FileSystem</span><span class="pn">.</span><span class="id">resolveSymbolicLinkTarget</span> <span class="k">true</span> <span class="s">&quot;current&quot;</span>
</code></pre>
