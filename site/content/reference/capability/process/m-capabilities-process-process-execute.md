---
title: "Capabilities.Process.Process.execute"
linkTitle: "execute"
type: docs
---

Executes a process using the process environment and returns the result.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.Process.Process.execute&#32;<span>fileName&#32;arguments</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fileName` | <code>string</code> | The path to the executable file. |
| `arguments` | <code>string</code> | The command-line arguments. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'e,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-capabilities-process-processresult.html">ProcessResult</a></span>&gt;</span></code> | A flow that returns the process execution result. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">result</span> <span class="o">=</span> <span class="id">Process</span><span class="pn">.</span><span class="id">execute</span> <span class="s">&quot;git&quot;</span> <span class="s">&quot;status&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val result: obj</div>



