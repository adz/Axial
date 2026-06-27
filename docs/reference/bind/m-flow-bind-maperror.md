---
title: "Flow.Bind.mapError"
linkTitle: "mapError"
weight: 2101
---

Maps an existing source error before <code>flow { }</code> binds it.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Bind.mapError&#32;<span>mapper&#32;source</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'error1&#32;->&#32;'error2</span></code> | The error mapping function. |
| `source` | <code>^source</code> | The source to adapt. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-binderror-3.html">BindError</a>&lt;<span>'env,&#32;'error2,&#32;'value</span>&gt;</span></code> | A bind marker for the flow computation expression. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">flow</span> <span class="pn">{</span>
     <span class="k">do!</span> <span class="id">authorize</span> <span class="id">user</span> <span class="o">|&gt;</span> <span class="id">Bind</span><span class="pn">.</span><span class="id">mapError</span> <span class="id">Unauthorized</span>
 <span class="pn">}</span>
</code></pre>
