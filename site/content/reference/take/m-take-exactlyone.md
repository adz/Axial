---
title: "Take.exactlyOne"
linkTitle: "exactlyOne"
weight: 2104
type: docs
---

Takes the only item from a sequence when it contains exactly one item.

## Signature

<div class="fsdocs-usage">
<code><span>Take.exactlyOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<a href="../check/t-cardinalityfailure.md">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok value</code> when exactly one item is present; otherwise a cardinality failure. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="n">5</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">exactlyOne</span> <span class="c">// Ok 5</span>
 <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">exactlyOne</span> <span class="c">// Error (ExpectedExactlyOne 2)</span>
</code></pre>
