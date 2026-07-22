---
title: "Refined.Parse.intOrDefault"
linkTitle: "intOrDefault"
weight: 2115
---

Parses an optional integer, using the supplied fallback only when the input is absent.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Parse.intOrDefault&#32;<span>fallback&#32;text</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code>int</code> |  |
| `text` | <code><span>string&#32;option</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>int,&#32;<a href="../types/t-refined-parseerror.md">ParseError</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Parse</span><span class="pn">.</span><span class="id">intOrDefault</span> <span class="n">80</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">None</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Ok</span> <span class="n">80</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.None: Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
