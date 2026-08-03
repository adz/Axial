---
title: "Constraint.customLocalizedWith"
linkTitle: "customLocalizedWith"
weight: 2306
type: docs
---


 Runs an arbitrary predicate, reporting the supplied prose plus a catalogue key and named arguments a
 translation can interpolate.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.customLocalizedWith&#32;<span>key&#32;description&#32;arguments&#32;predicate</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `key` | <code>string</code> |  |
| `description` | <code>string</code> |  |
| `arguments` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>string,&#32;<a href="t-constraint-constraintvalue.md">ConstraintValue</a></span>&gt;</span></code> |  |
| `predicate` | <code><span>'value&#32;->&#32;bool</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 The arguments are fixed at construction, which is what a rule&#39;s own operands are: an ISBN rule&#39;s expected
 length does not vary per value. A failure that must report something computed from the value takes
 <code>customWith</code> and builds its own violation.
 </p><p class='fsdocs-para'>
 No plural operand is inferred. Ordinary <code>.one</code>/<code>.other</code> lookup applies only where a catalogue
 declares an operand, and guessing one from an argument&#39;s name or value would silently change which key a
 translator has to supply.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Constraint</span><span class="pn">.</span><span class="id">customLocalizedWith</span>
     <span class="s">&quot;books.isbn.invalid&quot;</span>
     <span class="s">&quot;must be a valid ISBN&quot;</span>
     <span class="pn">(</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Map</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">ofList</span> <span class="pn">[</span> <span class="s">&quot;expectedLength&quot;</span><span class="pn">,</span> <span class="id">ConstraintValue</span><span class="pn">.</span><span class="id">Integer</span> <span class="n">13L</span> <span class="pn">]</span><span class="pn">)</span>
     <span class="id">isValidIsbn</span>
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
<div popover class="fsdocs-tip" id="fs2">val ofList: elements: (&#39;Key * &#39;T) list -&gt; Map&lt;&#39;Key,&#39;T&gt; (requires comparison)</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L292-292)
