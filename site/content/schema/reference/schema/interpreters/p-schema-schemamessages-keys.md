---
title: "Schema.SchemaMessages.keys"
linkTitle: "keys"
weight: 2218
type: docs
---

Every Schema message key, with the arguments its template may interpolate.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.SchemaMessages.keys&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;list</span></code> |  |

## Remarks

Use it the way <code>Catalogue.keys</code> is used: to test that a translation covers Schema too.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">SchemaMessages</span><span class="pn">.</span><span class="id">keys</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="m">List</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">filter</span> <span class="pn">(</span><span class="id">translations</span><span class="pn">.</span><span class="id">ContainsKey</span> <span class="o">&gt;</span><span class="pn">&gt;</span> <span class="fn">not</span><span class="pn">)</span>
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




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Messages.fs#L58-58)
