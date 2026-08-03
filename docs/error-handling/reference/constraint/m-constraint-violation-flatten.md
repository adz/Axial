---
title: "Constraint.Violation.flatten"
linkTitle: "flatten"
weight: 2903
---

Every leaf of a violation tree, in report order.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Violation.flatten&#32;<span>violation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `violation` | <code><a href="../result/errors/t-constraint-violation.md">Violation</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-atomicviolation.md">AtomicViolation</a>&#32;list</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">violation</span> <span class="o">|&gt;</span> <span class="id">Violation</span><span class="pn">.</span><span class="id">flatten</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="m">List</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">length</span>
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
<div popover class="fsdocs-tip" id="fs2">val length: list: &#39;T list -&gt; int</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L123-123)
