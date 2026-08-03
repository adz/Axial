---
title: "Constraint.pattern"
linkTitle: "pattern"
weight: 2504
---

Requires text to match the supplied .NET regular expression.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.pattern&#32;<span>expression</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expression` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;string&gt;</span></code> |  |

## Remarks


 The pattern is inspectable and portable as a string, but its <em>meaning</em> is the .NET dialect, which is
 not ECMA-262. Exporters therefore retain an arbitrary pattern as runtime-only metadata unless it is proven
 to lie in the common subset.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">reference</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">pattern</span> <span class="s">@&quot;^[A-Z]{3}-\d{4}$&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val reference: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L527-527)
