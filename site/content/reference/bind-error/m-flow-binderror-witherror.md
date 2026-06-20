---
title: "Flow.BindError.withError"
linkTitle: "withError"
weight: 2100
type: docs
---

Assigns an error to a missing or unit-error source before <code>flow { }</code> binds it.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.BindError.withError&#32;<span>error&#32;source</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `error` | <code>'error</code> | The error to use if the source fails. |
| `source` | <code>^source</code> | The source to adapt. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-binderror-3.html">BindError</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A bind marker for the flow computation expression. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">flow</span> <span class="pn">{</span>
     <span class="k">let!</span> <span class="id">user</span> <span class="o">=</span> <span class="id">maybeUser</span> <span class="o">|&gt;</span> <span class="id">BindError</span><span class="pn">.</span><span class="id">withError</span> <span class="id">InvalidUser</span>
     <span class="k">do!</span> <span class="id">isValid</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="o">|&gt;</span> <span class="id">BindError</span><span class="pn">.</span><span class="id">withError</span> <span class="id">InvalidInput</span>
 <span class="pn">}</span>
</code></pre>
