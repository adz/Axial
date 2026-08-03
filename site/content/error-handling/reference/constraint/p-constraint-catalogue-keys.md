---
title: "Constraint.Catalogue.keys"
linkTitle: "keys"
weight: 3000
type: docs
---

Every message key Axial can produce, including the composition and joining entries.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Catalogue.keys&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;list</span></code> |  |

## Remarks

Enumerate this to test that a translation covers the base catalogue.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Catalogue</span><span class="pn">.</span><span class="id">keys</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="m">List</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">filter</span> <span class="pn">(</span><span class="k">fun</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="fn">key</span> <span class="k">-&gt;</span> <span class="fn">not</span> <span class="pn">(</span><span class="id">translations</span><span class="pn">.</span><span class="id">ContainsKey</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="id">key</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">Multiple items<br />module List

from Microsoft.FSharp.Collections<br /><br />--------------------<br />type List&lt;&#39;T&gt; =
  | op_Nil
  | op_ColonColon of Head: &#39;T * Tail: &#39;T list
  interface IReadOnlyList&lt;&#39;T&gt;
  interface IReadOnlyCollection&lt;&#39;T&gt;
  interface IEnumerable
  interface IEnumerable&lt;&#39;T&gt;
  member GetReverseIndex: rank: int * offset: int -&gt; int
  member GetSlice: startIndex: int option * endIndex: int option -&gt; &#39;T list
  static member Cons: head: &#39;T * tail: &#39;T list -&gt; &#39;T list
  member Head: &#39;T
  member IsEmpty: bool
  member Item: index: int -&gt; &#39;T with get
  ...</div>
<div popover class="fsdocs-tip" id="fs2">val filter: predicate: (&#39;T -&gt; bool) -&gt; list: &#39;T list -&gt; &#39;T list</div>
<div popover class="fsdocs-tip" id="fs3">val key: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Catalogue.fs#L124-124)
