---
title: "Schema.Inspect.model"
linkTitle: "model"
weight: 2104
---

Describes a built model schema as inspectable field metadata.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Inspect.model&#32;<span>schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> | The built model schema to describe. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-schema-modeldescription.md">ModelDescription</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">description</span> <span class="o">=</span> <span class="id">Inspect</span><span class="pn">.</span><span class="id">model</span> <span class="id">customerSchema</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">names</span> <span class="o">=</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">description</span><span class="pn">.</span><span class="id">Fields</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="m">List</span><span class="pn">.</span><span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="5" class="id">map</span> <span class="id">_</span><span class="pn">.</span><span class="id">Name</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val description: obj</div>
<div popover class="fsdocs-tip" id="fs2">val names: obj list</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />module List

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
<div popover class="fsdocs-tip" id="fs4">val map: mapping: (&#39;T -&gt; &#39;U) -&gt; list: &#39;T list -&gt; &#39;U list</div>
