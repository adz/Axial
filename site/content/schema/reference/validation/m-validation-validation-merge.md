---
title: "Validation.Validation.merge"
linkTitle: "merge"
weight: 2118
type: docs
---

Merges two validations into a validation of a tuple.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.merge&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The first validation. |
| `right` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'next,&#32;'error</span>&gt;</span></code> | The second validation. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span><span>(<span>'value&#32;*&#32;'next</span>)</span>,&#32;'error</span>&gt;</span></code> | A validation containing a tuple of the results. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Validation</span><span class="pn">.</span><span class="id">merge</span> <span class="pn">(</span><span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">1</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="s">&quot;a&quot;</span><span class="pn">)</span> <span class="c">// Validation (Ok (1, &quot;a&quot;))</span>
</code></pre>
