---
title: "Check.failIfNone"
linkTitle: "failIfNone"
weight: 2214
type: docs
---

Returns the value when the option is <code>Some</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfNone&#32;<span>opt</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `opt` | <code><span>'a&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'a&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the value if present; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Some</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNone</span> <span class="c">// Ok 5</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">None</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNone</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Option.None: Option&lt;&#39;T&gt;</div>



