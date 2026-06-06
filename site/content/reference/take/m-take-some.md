---
title: "Take.some"
linkTitle: "some"
weight: 2001
type: docs
---

Takes the value from an option when it is <code>Some</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Take.some&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to unwrap. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../check/t-check.md">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> for <code>Some value</code>; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Some</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">some</span> <span class="c">// Ok 5</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">None</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">some</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Option.None: Option&lt;&#39;T&gt;</div>
