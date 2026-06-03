---
title: "Check.fromChoice"
linkTitle: "fromChoice"
weight: 2202
---

Converts an F# <code>Choice</code> into a <code>Result</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.fromChoice&#32;<span>choice</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `choice` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpchoice-2">Choice</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The choice value to convert. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | <code>Ok</code> for <code>Choice1Of2</code>; <code>Error</code> for <code>Choice2Of2</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Choice1Of2</span> <span class="n">42</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">fromChoice</span> <span class="c">// Ok 42</span>
 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Choice2Of2</span> <span class="s">&quot;missing&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">fromChoice</span> <span class="c">// Error &quot;missing&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Choice.Choice1Of2: &#39;T1 -&gt; Choice&lt;&#39;T1,&#39;T2&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Choice.Choice2Of2: &#39;T2 -&gt; Choice&lt;&#39;T1,&#39;T2&gt;</div>
