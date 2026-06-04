---
title: "Flow.fromOption"
linkTitle: "fromOption"
weight: 2306
type: docs
---

Lifts an option into a synchronous flow with the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.fromOption&#32;<span>error&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `error` | <code>'error</code> | The error to return if the option is <code>None</code>. |
| `value` | <code><span>'value&#32;option</span></code> | The option to lift. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that succeeds with the option&#39;s value or fails with the provided error. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">opt</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Some</span> <span class="s">&quot;value&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">fromOption</span> <span class="s">&quot;missing&quot;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="4" class="id">opt</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val opt: string option</div>
<div popover class="fsdocs-tip" id="fs2">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs3">val flow: obj</div>
