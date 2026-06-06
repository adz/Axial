---
title: "Check.valueSome"
linkTitle: "valueSome"
weight: 2404
type: docs
---

Returns success when the value option is <code>ValueSome</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.valueSome&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;voption</span></code> | The value option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for <code>ValueSome</code>; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">ValueSome</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">valueSome</span> <span class="c">// Ok ()</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">ValueNone</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">valueSome</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case ValueOption.ValueSome: &#39;T -&gt; ValueOption&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case ValueOption.ValueNone: ValueOption&lt;&#39;T&gt;</div>
