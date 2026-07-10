---
title: "Flow.Process.execute"
linkTitle: "execute"
weight: 2100
type: docs
---

Executes one command. Prefer <code>command</code>, <code>|&gt;&gt;</code>, and <code>run</code> for new code.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.execute&#32;<span>fileName&#32;arguments</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fileName` | <code>string</code> |  |
| `arguments` | <code><span>string&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="/reference/Axial/axial-flow-process-processerror.html">ProcessError</a>,&#32;<a href="t-flow-process-processresult.md">ProcessResult</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Process</span><span class="pn">.</span><span class="id">execute</span> <span class="s">&quot;dotnet&quot;</span> <span class="pn">[</span> <span class="s">&quot;--version&quot;</span> <span class="pn">]</span>
</code></pre>
