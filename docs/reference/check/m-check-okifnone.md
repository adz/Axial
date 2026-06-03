---
title: "Check.okIfNone"
linkTitle: "okIfNone"
weight: 2212
---

Returns success when the option is <code>None</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfNone&#32;<span>opt</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `opt` | <code><span>'a&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds if the option is <a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpoption-1-none">None</a>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">None</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNone</span> <span class="c">// Ok ()</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Some</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNone</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.None: Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
