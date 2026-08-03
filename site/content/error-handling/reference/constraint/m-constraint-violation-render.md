---
title: "Constraint.Violation.render"
linkTitle: "render"
weight: 2900
type: docs
---


 Renders a violation as an English sentence fragment with no trailing punctuation, keeping conjunction and
 alternative groups distinct.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Violation.render&#32;<span>violation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `violation` | <code><a href="../result/errors/t-constraint-violation.md">Violation</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Violation</span><span class="pn">.</span><span class="id">render</span> <span class="pn">(</span><span class="id">Atomic</span> <span class="pn">(</span><span class="id">Expected</span> <span class="pn">(</span><span class="id">PresenceAtom</span> <span class="id">Present</span><span class="pn">,</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">None</span><span class="pn">)</span><span class="pn">)</span><span class="pn">)</span>
 <span class="c">// &quot;value must be present&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.None: Option&lt;&#39;T&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L161-161)
