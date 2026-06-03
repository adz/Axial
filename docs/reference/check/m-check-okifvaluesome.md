---
title: "Check.okIfValueSome"
linkTitle: "okIfValueSome"
weight: 2215
---

Returns the value when the value option is <code>ValueSome</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfValueSome&#32;<span>opt</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `opt` | <code><span>'a&#32;voption</span></code> | The <a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpvalueoption-1">voption</a> to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'a&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the value if present; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">ValueSome</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfValueSome</span> <span class="c">// Ok 5</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">ValueNone</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfValueSome</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case ValueOption.ValueSome: &#39;T -&gt; ValueOption&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case ValueOption.ValueNone: ValueOption&lt;&#39;T&gt;</div>
