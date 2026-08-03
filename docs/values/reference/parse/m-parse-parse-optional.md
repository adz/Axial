---
title: "Parse.optional"
linkTitle: "optional"
weight: 2111
---

Parses an optional input, preserving a present input&#39;s parsing failure.

## Signature

<div class="fsdocs-usage">
<code><span>Parse.Parse.optional&#32;<span>parser&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `parser` | <code><span>'raw&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></span></code> |  |
| `input` | <code><span>'raw&#32;option</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span>'value&#32;option</span>,&#32;'error</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Parse</span><span class="pn">.</span><span class="id">optional</span> <span class="id">Parse</span><span class="pn">.</span><span class="id">int</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">None</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Ok</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="uc">None</span>
 <span class="id">Parse</span><span class="pn">.</span><span class="id">optional</span> <span class="id">Parse</span><span class="pn">.</span><span class="id">int</span> <span class="pn">(</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="id">Some</span> <span class="s">&quot;42&quot;</span><span class="pn">)</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="5" class="uc">Ok</span> <span class="pn">(</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="6" class="uc">Some</span> <span class="n">42</span><span class="pn">)</span>
 <span class="id">Parse</span><span class="pn">.</span><span class="id">optional</span> <span class="id">Parse</span><span class="pn">.</span><span class="id">int</span> <span class="pn">(</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="7" class="id">Some</span> <span class="s">&quot;bad&quot;</span><span class="pn">)</span> <span class="o">=</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="8" class="uc">Error</span> <span class="pn">(</span><span class="id">ParseError</span><span class="pn">.</span><span class="id">InvalidFormat</span> <span class="pn">(</span><span class="s">&quot;int&quot;</span><span class="pn">,</span> <span class="s">&quot;bad&quot;</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.None: Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs3">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs4">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Parse/Parse.fs#L103-103)
