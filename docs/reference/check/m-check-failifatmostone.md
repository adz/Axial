---
title: "Check.failIfAtMostOne"
linkTitle: "failIfAtMostOne"
weight: 2235
---

Returns the sequence when it contains more than one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfAtMostOne&#32;<span>coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `coll` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span>'value&#32;seq</span>,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-cardinalityfailure.html">CardinalityFailure</a></span>&gt;</span></code> | A result containing the source sequence, or a cardinality failure when it contains at most one item. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfAtMostOne</span> <span class="c">// Ok [1; 2]</span>
 <span class="pn">[</span> <span class="n">5</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfAtMostOne</span> <span class="c">// Error (ExpectedMoreThanOne 1)</span>
</code></pre>



