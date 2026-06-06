---
title: "Take.whenSome"
linkTitle: "whenSome"
weight: 2000
type: docs
---

Keeps the option when it is <code>Some</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Take.whenSome&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../check/t-check.md">Check</a>&lt;<span>'value&#32;option</span>&gt;</span></code> | <code>Ok option</code> for <code>Some</code>; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Some</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">whenSome</span> <span class="c">// Ok (Some 5)</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">None</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">whenSome</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Option.None: Option&lt;&#39;T&gt;</div>
