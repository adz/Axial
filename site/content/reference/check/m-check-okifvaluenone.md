---
title: "Check.okIfValueNone"
linkTitle: "okIfValueNone"
weight: 2216
type: docs
---

Returns success when the value option is <code>ValueNone</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfValueNone&#32;<span>opt</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `opt` | <code><span>'a&#32;voption</span></code> | The value option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds if the value option is <a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpvalueoption-1-valuenone">ValueNone</a>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">ValueNone</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfValueNone</span> <span class="c">// Ok ()</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">ValueSome</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfValueNone</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case ValueOption.ValueNone: ValueOption&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case ValueOption.ValueSome: &#39;T -&gt; ValueOption&lt;&#39;T&gt;</div>
