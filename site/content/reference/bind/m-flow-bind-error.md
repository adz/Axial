---
title: "Flow.Bind.error"
linkTitle: "error"
weight: 2100
type: docs
---

Assigns an error to a missing or unit-error source before <code>flow { }</code> binds it.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Bind.error&#32;<span>failure&#32;source</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> | The error to use if the source fails. |
| `source` | <code>^source</code> | The source to adapt. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-binderror-3.html">BindError</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A bind marker for the flow computation expression. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">flow</span> <span class="pn">{</span>
     <span class="k">let!</span> <span class="id">user</span> <span class="o">=</span> <span class="id">maybeUser</span> <span class="o">|&gt;</span> <span class="id">Bind</span><span class="pn">.</span><span class="id">error</span> <span class="id">InvalidUser</span>
     <span class="k">do!</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Result</span><span class="pn">.</span><span class="id">checkOr</span> <span class="pn">(</span><span class="pn">)</span> <span class="id">isValid</span> <span class="o">|&gt;</span> <span class="id">Bind</span><span class="pn">.</span><span class="id">error</span> <span class="id">InvalidInput</span>
 <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
