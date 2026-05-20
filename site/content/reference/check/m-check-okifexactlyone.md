---
title: "Check.okIfExactlyOne"
linkTitle: "okIfExactlyOne"
weight: 2232
type: docs
---

Returns the single element when the sequence contains exactly one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfExactlyOne&#32;<span>coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `coll` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-cardinalityfailure.html">CardinalityFailure</a></span>&gt;</span></code> | A result containing the single element, or a cardinality failure. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="n">5</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfExactlyOne</span> <span class="c">// Ok 5</span>
 <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfExactlyOne</span> <span class="c">// Error (ExpectedExactlyOne 2)</span>
</code></pre>
