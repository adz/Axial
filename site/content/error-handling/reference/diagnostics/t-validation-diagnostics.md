---
title: "Validation.Diagnostics"
linkTitle: "Diagnostics<error>"
weight: 1003
type: docs
---


 A mergeable validation graph that carries local errors and nested child branches.


## Signature

<div class="fsdocs-usage">
<code>type Diagnostics<'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `error` |

## Record Fields

| Field | Description |
| --- | --- |
| `Errors` | Errors that occurred exactly at this node in the graph. |
| `Children` | Nested diagnostic branches, keyed by <a href="t-validation-pathsegment.md">PathSegment</a>. |

## Remarks

<p class='fsdocs-para'><code>Errors</code> holds the application errors attached exactly to the current node, while
 <code>Children</code> holds nested branches keyed by <a href="t-validation-pathsegment.md">PathSegment</a>.
 </p><p class='fsdocs-para'>
 This structure allows hierarchical validation to stay navigable before flattening.
 Use <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics.flatten">flatten</a> to convert it into a linear list.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">diag</span> <span class="o">=</span> <span class="pn">{</span> <span class="id">Errors</span> <span class="o">=</span> <span class="pn">[</span><span class="s">&quot;Root error&quot;</span><span class="pn">]</span><span class="pn">;</span> <span class="id">Children</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">Map</span><span class="pn">.</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">empty</span> <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val diag: &#39;a</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />module Map

from Microsoft.FSharp.Collections<br /><br />--------------------<br />type Map&lt;&#39;Key,&#39;Value (requires comparison)&gt; =
  interface IReadOnlyDictionary&lt;&#39;Key,&#39;Value&gt;
  interface IReadOnlyCollection&lt;KeyValuePair&lt;&#39;Key,&#39;Value&gt;&gt;
  interface IEnumerable
  interface IStructuralEquatable
  interface IComparable
  interface IEnumerable&lt;KeyValuePair&lt;&#39;Key,&#39;Value&gt;&gt;
  interface ICollection&lt;KeyValuePair&lt;&#39;Key,&#39;Value&gt;&gt;
  interface IDictionary&lt;&#39;Key,&#39;Value&gt;
  new: elements: (&#39;Key * &#39;Value) seq -&gt; Map&lt;&#39;Key,&#39;Value&gt;
  member Add: key: &#39;Key * value: &#39;Value -&gt; Map&lt;&#39;Key,&#39;Value&gt;
  ...<br /><br />--------------------<br />new: elements: (&#39;Key * &#39;Value) seq -&gt; Map&lt;&#39;Key,&#39;Value&gt;</div>
<div popover class="fsdocs-tip" id="fs3">val empty&lt;&#39;Key,&#39;T (requires comparison)&gt; : Map&lt;&#39;Key,&#39;T&gt; (requires comparison)</div>
