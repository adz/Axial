---
title: "Check.failIfExactlyOne"
linkTitle: "failIfExactlyOne"
weight: 2233
type: docs
---

Returns the sequence when it does not contain exactly one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfExactlyOne&#32;<span>coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `coll` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span>'value&#32;seq</span>,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-cardinalityfailure.html">CardinalityFailure</a></span>&gt;</span></code> | A result containing the source sequence, or a cardinality failure when it contains exactly one item. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfExactlyOne</span> <span class="c">// Ok []</span>
 <span class="pn">[</span> <span class="n">5</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfExactlyOne</span> <span class="c">// Error ExpectedNotExactlyOne</span>
</code></pre>
