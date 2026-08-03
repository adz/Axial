---
title: "Constraint.any"
linkTitle: "any"
weight: 2301
type: docs
---


 Requires at least one alternative to hold, evaluating left to right and stopping at the first success. When
 none succeeds, every rejected branch is reported.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.any&#32;<span>first&#32;rest</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `first` | <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |
| `rest` | <code><span><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Remarks


 Taking the first branch separately keeps an unsatisfiable empty disjunction unrepresentable, so this never
 throws. Alternatives among rules are what neither <code>oneOf</code> (alternatives among literals) nor a range
 (one contiguous region) can express — a valid set with a hole in it, such as a wire value that is either a
 sentinel or a duration.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">ttl</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">int</span><span class="pn">&gt;</span> <span class="o">=</span>
     <span class="id">Constraint</span><span class="pn">.</span><span class="id">any</span> <span class="pn">(</span><span class="id">Constraint</span><span class="pn">.</span><span class="id">equalTo</span> <span class="o">-</span><span class="n">1</span><span class="pn">)</span> <span class="pn">[</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">atLeast</span> <span class="n">1</span> <span class="pn">]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val ttl: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L161-161)
