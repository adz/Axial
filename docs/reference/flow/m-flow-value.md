---
title: "Flow.value"
linkTitle: "value"
weight: 2303
---

Alias for <code>ok</code> that reads well in some call sites.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.value&#32;<span>item</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `item` | <code>'value</code> | The value to wrap in a successful flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that always succeeds with the provided value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">value</span> <span class="s">&quot;constant&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
