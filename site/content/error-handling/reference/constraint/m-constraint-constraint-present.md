---
title: "Constraint.present"
linkTitle: "present"
weight: 2400
type: docs
---

Requires a value to be inhabited according to its shape.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.present&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;^value&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 Whitespace-only text is blank, as are null text, a null or empty collection or map, <code>None</code>,
 <code>ValueNone</code>, and an empty <code>Nullable</code>. Blankness means .NET&#39;s whitespace set plus U+FEFF, which
 is what lets the rule be exported; see <code>nonBlankPattern</code>.
 </p><p class='fsdocs-para'>
 The shape is selected from the return type, so a reusable binding needs its annotation:
 <code>let requiredName : Constraint&lt;string&gt; = Constraint.present</code>. Applied where the type is already
 known — inside an annotated rule, or to a schema — no annotation is needed.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">requiredName</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">present</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val requiredName: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L380-380)
