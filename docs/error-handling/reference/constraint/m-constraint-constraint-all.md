---
title: "Constraint.all"
linkTitle: "all"
weight: 2300
---


 Requires every constraint to hold, evaluating each in declaration order and accumulating failures. The
 empty list is the satisfied identity.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.all&#32;<span>constraints</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraints` | <code><span><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Remarks


 F# visits list elements left to right, so annotate the binding when the first element is a type-directed
 value: <code>let requiredName : Constraint&lt;string&gt; = Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]</code>.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">requiredName</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span> <span class="o">=</span>
     <span class="id">Constraint</span><span class="pn">.</span><span class="id">all</span> <span class="pn">[</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">present</span><span class="pn">;</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">lengthBetween</span> <span class="n">2</span> <span class="n">40</span> <span class="pn">]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val requiredName: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L124-124)
