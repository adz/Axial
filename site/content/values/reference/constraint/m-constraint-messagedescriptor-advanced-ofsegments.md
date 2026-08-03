---
title: "Constraint.MessageDescriptor.Advanced.ofSegments"
linkTitle: "ofSegments"
weight: 2811
type: docs
---

Builds a descriptor from already-parsed segments, skipping the parse.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.MessageDescriptor.Advanced.ofSegments&#32;<span>segments&#32;arguments</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `segments` | <code><span>string&#32;list</span></code> |  |
| `arguments` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>string,&#32;<a href="t-constraint-constraintvalue.md">ConstraintValue</a></span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-messagedescriptor.md">MessageDescriptor</a></code> |  |

## Remarks


 For a generated catalogue whose segments are known at build time, so a render does not reparse a key
 it has already validated. Segments are unencoded and may contain any character except that an empty
 segment, or no segments at all, is rejected.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">MessageDescriptor</span><span class="pn">.</span><span class="id">Advanced</span><span class="pn">.</span><span class="id">ofSegments</span> <span class="pn">[</span> <span class="s">&quot;schema&quot;</span><span class="pn">;</span> <span class="s">&quot;omitted&quot;</span> <span class="pn">]</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Map</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">empty</span>
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




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L171-171)
