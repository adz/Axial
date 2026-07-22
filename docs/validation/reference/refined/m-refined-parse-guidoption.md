---
title: "Refined.Parse.guidOption"
linkTitle: "guidOption"
weight: 2114
---

Parses an optional GUID. Absence returns <code>Ok None</code>; malformed present text returns its parsing error.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Parse.guidOption&#32;<span>text</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `text` | <code><span>string&#32;option</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span><a href="https://learn.microsoft.com/dotnet/api/system.guid">Guid</a>&#32;option</span>,&#32;<a href="t-refined-parseerror.md">ParseError</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Parse</span><span class="pn">.</span><span class="id">guidOption</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">None</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Ok</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="uc">None</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.None: Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
