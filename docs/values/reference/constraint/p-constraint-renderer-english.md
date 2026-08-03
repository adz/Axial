---
title: "Constraint.Renderer.english"
linkTitle: "english"
weight: 2906
---

A renderer that uses each catalogue&#39;s neutral English, with no resources at all.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.english&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks


 The default for tests, tools, and applications that never translate. It is not the same as
 <code>Violation.render</code>: this produces bare predicates that compose, while <code>render</code> keeps the legacy
 self-contained English exactly.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">violation</span> <span class="o">|&gt;</span> <span class="id">Violation</span><span class="pn">.</span><span class="id">fullMessage</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">english</span> <span class="c">// &quot;value must be present&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L498-498)
