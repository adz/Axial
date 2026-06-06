---
title: "Check.none"
linkTitle: "none"
weight: 2403
---

Returns success when the option is <code>None</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.none&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for <code>None</code>; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">None</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">none</span> <span class="c">// Ok ()</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Some</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">none</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.None: Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
