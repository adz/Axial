---
title: "Check.okIfAtMostOne"
linkTitle: "okIfAtMostOne"
weight: 2234
type: docs
---

Returns an optional single element when the sequence contains at most one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfAtMostOne&#32;<span>coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `coll` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span>'value&#32;option</span>,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-cardinalityfailure.html">CardinalityFailure</a></span>&gt;</span></code> | A result containing <code>Some</code> single element or <code>None</code> for an empty sequence, or a cardinality failure. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="n">5</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfAtMostOne</span> <span class="c">// Ok (Some 5)</span>
 <span class="pn">[</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfAtMostOne</span> <span class="c">// Ok None</span>
 <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfAtMostOne</span> <span class="c">// Error (ExpectedAtMostOne 2)</span>
</code></pre>
