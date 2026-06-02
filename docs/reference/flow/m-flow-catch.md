---
title: "Flow.catch"
linkTitle: "catch"
weight: 2316
---

Catches exceptions raised during execution and maps them to a typed error.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.catch&#32;<span>handler&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `handler` | <code><span>exn&#32;->&#32;'error</span></code> | A function of type <code>exn -&gt; &#39;error</code> to map the exception. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow of type <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> to monitor. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> that converts exceptions into success-path errors. |

## Remarks


 Exceptions that are not caught by this helper will bubble up to the caller of <a href="https://learn.microsoft.com/dotnet/api/run">run</a>.
 This ensures that known exceptions can be handled within the flow context.
 

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">die</span> <span class="pn">(</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">System</span><span class="pn">.</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">Exception</span><span class="pn">(</span><span class="s">&quot;boom&quot;</span><span class="pn">)</span><span class="pn">)</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">catch</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">ex</span> <span class="k">-&gt;</span> <span class="s">&quot;caught: &quot;</span> <span class="o">+</span> <span class="id">ex</span><span class="pn">.</span><span class="id">Message</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
<div popover class="fsdocs-tip" id="fs2">namespace System</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />type Exception =
  interface ISerializable
  new: unit -&gt; unit + 2 overloads
  member GetBaseException: unit -&gt; exn
  member GetObjectData: info: SerializationInfo * context: StreamingContext -&gt; unit
  member GetType: unit -&gt; Type
  member ToString: unit -&gt; string
  member Data: IDictionary
  member HResult: int
  member HelpLink: string
  member InnerException: exn
  ...<br /><em>&lt;summary&gt;Represents errors that occur during application execution.&lt;/summary&gt;</em><br /><br />--------------------<br />System.Exception() : System.Exception<br />System.Exception(message: string) : System.Exception<br />System.Exception(message: string, innerException: exn) : System.Exception</div>



