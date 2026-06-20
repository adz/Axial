---
title: "Validation.Validation.collect"
linkTitle: "collect"
weight: 2115
type: docs
---

Collects a sequence of validations into a single validation of a list.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.collect&#32;<span>validations</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `validations` | <code><span><span><a href="/reference/Axial/axial-validation-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&#32;seq</span></code> | A sequence of type <code>seq&lt;Validation&lt;&#39;value, &#39;error&gt;&gt;</code>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-validation-validation-2.html">Validation</a>&lt;<span><span>'value&#32;list</span>,&#32;'error</span>&gt;</span></code> | A validation containing the list of values or accumulated diagnostics. |

## Remarks


 This operation is applicative: it will collect errors from ALL items in the sequence.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span><span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">1</span><span class="pn">;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">2</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">collect</span> <span class="c">// Validation (Ok [1; 2])</span>
</code></pre>
