---
title: "Constraint.MessageDescriptor.Advanced.tryCreate"
linkTitle: "tryCreate"
weight: 2810
type: docs
---

Parses a relative key, returning the parse failure rather than raising.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.MessageDescriptor.Advanced.tryCreate&#32;<span>key&#32;arguments</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `key` | <code>string</code> |  |
| `arguments` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>string,&#32;<a href="t-constraint-constraintvalue.md">ConstraintValue</a></span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><a href="t-constraint-messagedescriptor.md">MessageDescriptor</a>,&#32;<a href="t-constraint-messagekeyerror.md">MessageKeyError</a></span>&gt;</span></code> |  |

## Remarks

Use this for externally supplied configuration; it is total.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">MessageDescriptor</span><span class="pn">.</span><span class="id">Advanced</span><span class="pn">.</span><span class="id">tryCreate</span> <span class="s">&quot;books.isbn.invalid&quot;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Map</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">empty</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">Multiple items<br />module Map

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
<div popover class="fsdocs-tip" id="fs2">val empty&lt;&#39;Key,&#39;T (requires comparison)&gt; : Map&lt;&#39;Key,&#39;T&gt; (requires comparison)</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L149-149)
