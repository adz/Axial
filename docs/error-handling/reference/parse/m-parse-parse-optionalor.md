---
title: "Parse.optionalOr"
linkTitle: "optionalOr"
weight: 2112
---

Parses an optional input, using the supplied fallback only when the input is absent.

## Signature

<div class="fsdocs-usage">
<code><span>Parse.Parse.optionalOr&#32;<span>fallback&#32;parser&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code>'value</code> |  |
| `parser` | <code><span>'raw&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></span></code> |  |
| `input` | <code><span>'raw&#32;option</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Parse</span><span class="pn">.</span><span class="id">optionalOr</span> <span class="n">80</span> <span class="id">Parse</span><span class="pn">.</span><span class="id">int</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">None</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Ok</span> <span class="n">80</span>
 <span class="id">Parse</span><span class="pn">.</span><span class="id">optionalOr</span> <span class="n">80</span> <span class="id">Parse</span><span class="pn">.</span><span class="id">int</span> <span class="pn">(</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">Some</span> <span class="s">&quot;443&quot;</span><span class="pn">)</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="4" class="uc">Ok</span> <span class="n">443</span>
 <span class="id">Parse</span><span class="pn">.</span><span class="id">optionalOr</span> <span class="n">80</span> <span class="id">Parse</span><span class="pn">.</span><span class="id">int</span> <span class="pn">(</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="5" class="id">Some</span> <span class="s">&quot;bad&quot;</span><span class="pn">)</span> <span class="o">=</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="6" class="uc">Error</span> <span class="pn">(</span><span class="id">ParseError</span><span class="pn">.</span><span class="id">InvalidFormat</span> <span class="pn">(</span><span class="s">&quot;int&quot;</span><span class="pn">,</span> <span class="s">&quot;bad&quot;</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.None: Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs3">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs4">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Parse/Parse.fs#L119-119)
