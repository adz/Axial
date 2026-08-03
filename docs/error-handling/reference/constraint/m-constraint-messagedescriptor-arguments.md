---
title: "Constraint.MessageDescriptor.arguments"
linkTitle: "arguments"
weight: 2807
---

The operands the message interpolates, named for the template.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.MessageDescriptor.arguments&#32;<span>descriptor</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `descriptor` | <code><a href="t-constraint-messagedescriptor.md">MessageDescriptor</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>string,&#32;<a href="t-constraint-constraintvalue.md">ConstraintValue</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">MessageDescriptor</span><span class="pn">.</span><span class="id">arguments</span> <span class="id">descriptor</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="m">Map</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">tryFind</span> <span class="s">&quot;expectedLength&quot;</span>
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
<div popover class="fsdocs-tip" id="fs2">val tryFind: key: &#39;Key -&gt; table: Map&lt;&#39;Key,&#39;T&gt; -&gt; &#39;T option (requires comparison)</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L129-129)
